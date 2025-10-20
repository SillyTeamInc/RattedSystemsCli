using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
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
        Emi.Info("Connecting to socket uploader...");
        client.Options.SetRequestHeader("User-Agent", Utils.GetUserAgent());
        var uri = new Uri($"wss://{Domain}{UploadEndpoint}");
        await client.ConnectAsync(uri, CancellationToken.None);
        WebSocketClient = client;
        Emi.Info("Connected to socket uploader.");
    }
    
    public async Task AuthenticateAsync(string token)
    {
        Emi.Info("Authenticating socket uploader...");
        await SendOp("auth", token);
        var nextOp = await ReceiveNextOp(5000); 
        if (!nextOp.GetDataProperty<bool>("success"))
        {
            string message = nextOp.GetDataProperty<string>("message") ?? "Unknown error";
            await CloseAsync();
            throw new Exception("Socket authentication failed: " + message);
        }
        
        Emi.Debug("AuthResult: " + nextOp.GetDataProperty<string>("message"));
        Emi.Info("Socket uploader authenticated.");
    }
    
    public async Task<long> SolveProofOfWorkAsync(string challenge, int difficulty, CancellationToken cancellationToken = default)
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
        
        
        await uploader.ConnectAsync();
        await uploader.AuthenticateAsync(UploadToken.GetToken() ?? "");
        
        string fileName = Path.GetFileName(filePath);
        long fileSize = new FileInfo(filePath).Length;
        await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        
        await uploader.SendOp("start_upload", new {
            fileName = fileName,
            fileSize = fileSize
        });
        
        var response = await uploader.ReceiveOp("pow_challenge", 10000);
        if (response == null)
        {
            throw new Exception("Did not receive proof-of-work challenge from server.");
        }
        
        string challenge = response.GetDataProperty<string>("challenge") ?? "";
        int difficulty = response.GetDataProperty<int>("difficulty");
        Emi.Debug($"Received proof-of-work challenge: {challenge} with difficulty {difficulty}");
        
        long nonce = await uploader.SolveProofOfWorkAsync(challenge, difficulty);
        Emi.Debug($"Solved proof-of-work with nonce: {nonce}");
        
        await uploader.SendOp("pow_solution", new {
            nonce = nonce
        });
        
        var uploadStart = await uploader.ReceiveOp("start_upload", 10000);
        bool success = uploadStart?.GetDataProperty<bool>("success") ?? false;
        string message = uploadStart?.GetDataProperty<string>("message") ?? "Unknown error";
        if (!success)
        {
            throw new Exception("Upload initiation failed: " + message);
        }
        
        Emi.Debug("Upload started: " + message);
        string oneTimeUploadToken = uploadStart?.GetDataProperty<string>("oneTimeUploadToken") ?? "";
        int chunkSize = uploadStart?.GetDataProperty<int>("chunkSize") ?? 1024 * 1024;
        
        Emi.Debug($"Uploading file in chunks of size: {chunkSize} bytes");

        string fileHash = "no"; // TODO: Implement if needed later
        
        string header = $"FILEUPLOAD_{oneTimeUploadToken}||{fileHash}>>";
        byte[] headerBytes = Encoding.UTF8.GetBytes(header);
        byte[] buffer = new byte[chunkSize];
        int bytesRead;
        
        await uploader.SendRaw(headerBytes);
        
        while ((bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            byte[] chunkData = new byte[bytesRead];
            Array.Copy(buffer, chunkData, bytesRead);
            await uploader.SendRaw(chunkData);
            
            var chunkResponse = await uploader.ReceiveOp("request_next_chunk", -1);
            if (chunkResponse == null)
            {
                throw new Exception("Did not receive chunk request from server.");
            }

            await Console.Error.WriteAsync($"\rUploaded {chunkResponse.GetDataProperty<long>("totalReceived")} / {chunkResponse.GetDataProperty<long>("totalSize")} bytes " +
                                           $"({chunkResponse.GetDataProperty<double>("percentage"):0.00}%) " +
                                           $"at {chunkResponse.GetDataProperty<double>("uploadSpeedMbps"):0.00} Mbps " +
                                           $"ETA: {chunkResponse.GetDataProperty<string>("estimatedTimeStr")}");
        }
        
        await Console.Error.WriteLineAsync();
        
        var uploadComplete = await uploader.ReceiveOp("upload_complete", 30000);
        bool uploadSuccess = uploadComplete?.GetDataProperty<bool>("success") ?? false;
        string uploadMessage = uploadComplete?.GetDataProperty<string>("message") ?? "Unknown error";
        
        if (!uploadSuccess)
        {
            throw new Exception("File upload failed: " + uploadMessage);
        }
        
        string uploadLink = uploadComplete?.GetDataProperty<string>("uploadLink") ?? "";
        
        Emi.Info("File uploaded successfully! Download link: " + uploadLink);
        await uploader.CloseAsync();
        return uploadLink;
    }
    
    public async Task SendJson(object message)
    {
        string json = JsonSerializer.Serialize(message);
        byte[] buffer = Encoding.UTF8.GetBytes(json);
        await WebSocketClient.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
    }
    
    public async Task SendString(string data)
    {
        byte[] buffer = Encoding.UTF8.GetBytes(data);
        await WebSocketClient.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Binary, true, CancellationToken.None);
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
        await WebSocketClient.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);
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