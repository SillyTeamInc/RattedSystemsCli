using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using EmniProgress.Backends;
using EmniProgress.Backends.KDE;
using EmniProgress.Factory;
using RattedSystemsCli.Utilities;
using RattedSystemsCli.Utilities.Config;

namespace RattedSystemsCli.HostAPI.SocketUploader;

public class SocketUploader
{
    public string UploadEndpoint { get; set; } = "/api/v1/discord/socket";
    public string Domain { get; set; } = "ratted.systems";
    public WebSocket WebSocketClient { get; set; }


    public async Task ConnectAsync()
    {
        var client = new ClientWebSocket();
        //Emi.Info("Connecting to socket uploader...");
        client.Options.SetRequestHeader("User-Agent", Utils.GetUserAgent());
        var uri = new Uri($"wss://{Domain}{UploadEndpoint}");
        await client.ConnectAsync(uri, CancellationToken.None);
        WebSocketClient = client;
        //Emi.Info("Connected to socket uploader.");
    }

    public async Task AuthenticateAsync(string token)
    {
        //Emi.Info("Authenticating socket uploader...");
        await SendOp("auth", token);
        var nextOp = await ReceiveNextOp(5000);
        if (!nextOp.GetDataProperty<bool>("success"))
        {
            string message = nextOp.GetDataProperty<string>("message") ?? "Unknown error";
            await CloseAsync();
            throw new Exception("Socket authentication failed: " + message);
        }

        //Emi.Debug("AuthResult: " + nextOp.GetDataProperty<string>("message"));
        //Emi.Info("Socket uploader authenticated.");
    }

    public async Task<long> SolveProofOfWorkAsync(string challenge, int difficulty,
        CancellationToken cancellationToken = default)
    {
        return await Task.Run(() =>
        {
            long nonce = 0;
            var targetPrefix = new string('0', difficulty);

            using var sha256 = System.Security.Cryptography.SHA256.Create();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var inputBytes = Encoding.UTF8.GetBytes(challenge + nonce.ToString());
                var hashBytes = sha256.ComputeHash(inputBytes);
                var hashHex = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

                if (hashHex.StartsWith(targetPrefix))
                    return nonce;

                nonce++;
            }
        }, cancellationToken);
    }

    public static async Task<string?> UploadFileAsync(string filePath)
    {
        Emi.Debug($"Uploading file via socket: {filePath}");
        var uploader = new SocketUploader();
        CancellationTokenSource cts = new CancellationTokenSource();
        await using var progress = (CompositeProgressBackend)EmniFactory.Create();
        progress.GetBackend<KdeProgressBackend>()?.OnCancel(() =>
        {
            cts.Cancel();
            return Task.CompletedTask;
        });
        await progress.StartAsync("Uploading", Path.GetFileName(filePath), "ratted.systems", "document-send");

        try
        {
            await progress.UpdateAsync(0, "Connecting...");
            await uploader.ConnectAsync();
            await progress.UpdateAsync(0, "Authenticating...");
            await uploader.AuthenticateAsync(UploadToken.GetToken() ?? "");
            cts.Token.ThrowIfCancellationRequested();

            string fileName = Path.GetFileName(filePath);
            long fileSize = new FileInfo(filePath).Length;
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536,
                useAsync: true);

            await progress.UpdateAsync(0, "Starting upload...");
            await uploader.SendOp("start_upload", new { fileName, fileSize });

            var powChallenge = await uploader.ReceiveOp("pow_challenge", 10000)
                               ?? throw new Exception("Did not receive proof-of-work challenge.");
            string challenge = powChallenge.GetDataProperty<string>("challenge") ?? "";
            int difficulty = powChallenge.GetDataProperty<int>("difficulty");

            long nonce = await uploader.SolveProofOfWorkAsync(challenge, difficulty, cts.Token);
            await uploader.SendOp("pow_solution", new { nonce });

            var uploadStart = await uploader.ReceiveOp("start_upload", 10000);
            if (uploadStart?.GetDataProperty<bool>("success") != true)
            {
                string msg = uploadStart?.GetDataProperty<string>("message") ?? "Unknown error";
                await progress.FinishAsync(false, msg);
                throw new Exception("Upload initiation failed: " + msg);
            }

            string oneTimeUploadToken = uploadStart.GetDataProperty<string>("oneTimeUploadToken") ?? "";
            int chunkSize = uploadStart.GetDataProperty<int>("chunkSize") is int cs and > 0 ? cs : 1024 * 1024 * 2;

            await uploader.SendRaw(Encoding.UTF8.GetBytes($"FILEUPLOAD_{oneTimeUploadToken}||no>>"));

            const int windowSize = 4;
            var windowSlots = new SemaphoreSlim(windowSize, windowSize);
            Exception? sendError = null;
            // this is so peak....
            var ackLoop = Task.Run(async () =>
            {
                long totalReceived = 0;
                while (totalReceived < fileSize)
                {
                    var ack = await uploader.ReceiveOp("request_next_chunk", 120000);
                    if (ack == null) throw new Exception("Chunk ack timeout.");

                    totalReceived = ack.GetDataProperty<long>("totalReceived");
                    double pct = ack.GetDataProperty<double>("percentage");
                    double mbps = ack.GetDataProperty<double>("uploadSpeedMbps");
                    ulong speedBps = mbps > 0 ? (ulong)(mbps * 1_000_000 / 8) : 0;

                    if (pct < 100)
                        await progress.UpdateAsync((float)pct, "Uploading");

                    KdeProgressBackend? kde = progress.GetBackend<KdeProgressBackend>();
                    if (kde != null)
                    {
                        await kde.SetDestUrlAsync($"https://{uploader.Domain}/");
                        await kde.UpdateSpeedAsync(speedBps);
                        await kde.UpdateAmountAsync((ulong)fileSize, (ulong)totalReceived, KdeJobUnit.Bytes);
                        await kde.UpdateDescriptionFieldAsync(1, "Filename", fileName);
                    }

                    windowSlots.Release();
                    cts.Token.ThrowIfCancellationRequested();
                }
            }, cts.Token);

            byte[] buffer = new byte[chunkSize];
            int bytesRead;
            while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length, cts.Token)) > 0)
            {
                await windowSlots.WaitAsync(cts.Token);
                if (sendError != null) throw sendError;

                byte[] chunk = new byte[bytesRead];
                Buffer.BlockCopy(buffer, 0, chunk, 0, bytesRead);
                await uploader.SendRaw(chunk);
            }

            await ackLoop;

            var uploadComplete = await uploader.ReceiveOp("upload_complete", 30000);
            if (uploadComplete?.GetDataProperty<bool>("success") != true)
            {
                string msg = uploadComplete?.GetDataProperty<string>("message") ?? "Unknown error";
                await progress.FinishAsync(false, msg);
                throw new Exception("File upload failed: " + msg);
            }

            string uploadLink = uploadComplete.GetDataProperty<string>("uploadLink") ?? "";
            await progress.UpdateAsync(100, "Upload complete!");
            await progress.FinishAsync(true, "File uploaded successfully!");
            Emi.Info("File uploaded successfully: " + uploadLink);
            await uploader.CloseAsync();
            return uploadLink;
        }
        catch (OperationCanceledException)
        {
            await progress.FinishAsync(false, "Upload cancelled!");
            Emi.Info("Upload cancelled by user.");
            return null;
        }
    }

    public async Task SendJson(object message)
    {
        string json = JsonSerializer.Serialize(message);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        await WebSocketClient.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true,
            CancellationToken.None);
    }

    public async Task SendString(string data)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(data);
        await WebSocketClient.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, true,
            CancellationToken.None);
    }


    public async Task<string?> ReceiveJson(int timeoutMs = -1)
    {
        var buffer = new byte[8192];
        var result = await WebSocketClient.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            await CloseAsync();
            return null;
        }

        return Encoding.UTF8.GetString(buffer, 0, result.Count);
    }

    public async Task SendOp(string op, object? data = null)
    {
        await SendJson(new SocketMessage
        {
            Op = op,
            Data = data
        });
    }

    public async Task<SocketMessage?> ReceiveOp(string expectedOp, int timeoutMs = -1)
    {
        byte[] buffer = new byte[1024 * 4];
        WebSocketReceiveResult result;
        using (var cts = new CancellationTokenSource())
        {
            if (timeoutMs > 0) cts.CancelAfter(timeoutMs);

            while (true)
            {
                result = await WebSocketClient.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) return null;
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var socketMessage = JsonSerializer.Deserialize<SocketMessage>(message);
                    if (socketMessage != null && socketMessage.Op == expectedOp)
                        return socketMessage;
                }

                if (cts.Token.IsCancellationRequested) return null;
            }
        }
    }

    public async Task<SocketMessage?> ReceiveNextOp(int timeoutMs = -1)
    {
        byte[] buffer = new byte[1024 * 4];
        WebSocketReceiveResult result;
        using (var cts = new CancellationTokenSource())
        {
            if (timeoutMs > 0) cts.CancelAfter(timeoutMs);

            while (true)
            {
                result = await WebSocketClient.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) return null;
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var socketMessage = JsonSerializer.Deserialize<SocketMessage>(message);
                    if (socketMessage != null)
                        return socketMessage;
                }

                if (cts.Token.IsCancellationRequested) return null;
            }
        }
    }

    public async Task CloseAsync()
    {
        await WebSocketClient.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
    }

    public async Task SendRaw(byte[] data)
    {
        await WebSocketClient.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true,
            CancellationToken.None);
    }

    public async Task<byte[]> ReceiveRaw(int timeoutMs = -1)
    {
        var buffer = new byte[8192];
        var result = await WebSocketClient.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
        if (result.MessageType == WebSocketMessageType.Close)
        {
            await CloseAsync();
            return [];
        }

        var receivedData = new byte[result.Count];
        Array.Copy(buffer, receivedData, result.Count);
        return receivedData;
    }
}