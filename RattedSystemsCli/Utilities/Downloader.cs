using System.Collections.Concurrent;

namespace RattedSystemsCli.Utilities;

// Stolen from another project i made and not even changed lmao
public class Downloader
{
    public static string FormatBytes(long bytes, bool useWords = false)
    {
        string[] suffixes = useWords ? new string[] { "bytes", "kilobytes", "megabytes", "gigabytes", "terabytes" } : new string[] { "B", "KB", "MB", "GB", "TB" };
        int i = 0;
        double dblSByte = bytes;
        while (Math.Abs(dblSByte) >= 1024 && i < suffixes.Length - 1)
        {
            dblSByte /= 1024;
            i++;
        }
        return $"{dblSByte:0.##} {suffixes[i]}";
    }
    
    public long BytesDownloaded { get; private set; }
    public long TotalBytes { get; private set; }
    
    public string FileName { get; private set; }
    public string Url { get; private set; }
    
    private bool _isMultithreaded = false;
    
    private ConcurrentDictionary<int, long> _threadBytesDownloaded = new ConcurrentDictionary<int, long>();
    
    private long GetBytesDownloaded()
    {
        if (!_isMultithreaded)
        {
            return BytesDownloaded;
        }
         
        long bytesDownloaded = 0;
        foreach (long threadBytesDownloaded in _threadBytesDownloaded.Values)
        {
            bytesDownloaded += threadBytesDownloaded;
        }
        return bytesDownloaded;
    }
    
    private List<(long, DateTimeOffset)> _downloadSpeeds = new List<(long, DateTimeOffset)>();
    
    private void AddDownloadSpeed()
    {
        long bytesDownloaded = GetBytesDownloaded();
        DateTimeOffset now = DateTimeOffset.Now;
        
        _downloadSpeeds.Add((bytesDownloaded, now));
        
        _downloadSpeeds.RemoveAll(s => (now - s.Item2).TotalSeconds > 1);
    }
    
    private double GetDownloadSpeed()
    {
        if (_downloadSpeeds.Count < 2)
        {
            return 0;
        }
        
        long bytesDownloaded = GetBytesDownloaded();
        DateTimeOffset now = DateTimeOffset.Now;
        
        var speeds = _downloadSpeeds
            .Where(s => (now - s.Item2).TotalSeconds <= 1)
            .ToList();
        
        if (speeds.Count < 2)
        {
            return 0;
        }
        
        long bytesDiff = bytesDownloaded - speeds.First().Item1;
        TimeSpan timeDiff = now - speeds.First().Item2;
        
        return bytesDiff / timeDiff.TotalSeconds;
    }
    
    private string GetProgressString()
    {
        int progressBarLength = 50;
        double progress = (double)GetBytesDownloaded() / TotalBytes * 100;
        progress = Math.Round(progress, 1);
        int progressLength = (int)(progress / 100 * progressBarLength);
        
        // format:
        // Downloading {filename}...
        // [=============>    50%                  ]
        string downloadSpeed = FormatBytes((long)GetDownloadSpeed(), false);
        string progressString1 = $"Downloading {FileName}...";
        string progressString2 = $"[{new string('=', progressLength)}>{new string(' ', progressBarLength - progressLength)}]";
        
        string percentStr = $"{progress}%";
        int percentageIndex = (progressBarLength - percentStr.Length) / 2;
        progressString2 = progressString2.Remove(percentageIndex, percentStr.Length).Insert(percentageIndex, percentStr);
        return progressString1 + "\n" + progressString2 + $"\n[{downloadSpeed}/s]  [{FormatBytes(GetBytesDownloaded(), false)}/{FormatBytes(TotalBytes, false)}]        ";
    }
    
    private bool _hasClearBeenCalled = false;
    
    private void UpdateProgress()
    {
        if (!_hasClearBeenCalled)
        {
            _hasClearBeenCalled = true;
            Console.Clear();
        }
    
        AddDownloadSpeed();
    
        int top = Console.CursorTop;
        int left = Console.CursorLeft;
    
        Console.WriteLine(GetProgressString());
    
        if (GetBytesDownloaded() != TotalBytes)
        {
            Console.SetCursorPosition(left, top);
        }
    }
    
    private void UpdateFileSize()
    {
        using HttpClient client = new HttpClient();
        using HttpResponseMessage response = client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead).Result;
        response.EnsureSuccessStatusCode();
        TotalBytes = response.Content.Headers.ContentLength ?? -1;
    }
    
    public async Task DownloadFileMultithreaded(int taskCount = 4)
    {
        UpdateFileSize();
        
        long totalBytes = TotalBytes;
        long bytesPerThread = totalBytes / taskCount;
        long remainingBytes = totalBytes % taskCount;
        
        
        List<Task> tasks = new List<Task>();
        
        int threads = taskCount;
        
        int numberOfChunks = threads;
        int bufferSize = 81920;
        
        for (int i = 0; i < threads; i++)
        {
            _threadBytesDownloaded[i] = 0;
            long start = i * bytesPerThread;
            long end = (i == threads - 1) ? (start + bytesPerThread + remainingBytes - 1) : (start + bytesPerThread - 1);
            
            
            int threadId = i;
            Task task = Task.Run(() => DownloadChunk(Url, FileName, start, end, bufferSize, threadId));
            tasks.Add(task);
        }
        
        while (tasks.Any(t => !t.IsCompleted))
        {
            UpdateProgress();
            Thread.Sleep(100);
        }
        
        UpdateProgress();
        
    }
    
    private async Task DownloadChunk(string url, string filePath, long start, long end, int bufferSize, int threadId)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(start, end);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RattedSystemsCli/1.0 (+https://ratted.systems/)");

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Write);

            fileStream.Seek(start, SeekOrigin.Begin);

            var buffer = new byte[bufferSize];
            int bytesRead;

            while ((bytesRead = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));

                _threadBytesDownloaded[threadId] += bytesRead;
            }
        } catch (Exception ex)
        {
            Console.WriteLine($"Error in thread {threadId}: {ex}");
        }

    }
    
    public Downloader(string url, string fileName, bool isMultithreaded = false)
    {
        Url = url;
        FileName = fileName;
        _isMultithreaded = isMultithreaded;
    }
}