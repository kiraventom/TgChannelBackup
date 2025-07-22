using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TgChannelBackup.Core.Downloader;
using TL;

namespace TgChannelBackup.Core;

public class BackupWorker : BackgroundService
{
    private readonly TelegramService _telegramService;
    private readonly BackupDb _backupDb;
    private readonly RunOptions _options;
    private readonly PhotoDownloader _photoDownloader;
    private readonly DocumentDownloader _documentDownloader;
    private readonly ILogger<BackupWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public BackupWorker(TelegramService telegramService, BackupDb archiveIndex, RunOptions options, PhotoDownloader photoDownloader, DocumentDownloader documentDownloader, ILogger<BackupWorker> logger, IHostApplicationLifetime lifetime)
    {
        _telegramService = telegramService;
        _backupDb = archiveIndex;
        _options = options;
        _photoDownloader = photoDownloader;
        _documentDownloader = documentDownloader;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _telegramService.LogIn();

        var channel = await _telegramService.GetChannelById(_options.ChannelId);

        var startId = _options.StartId ?? _backupDb.GetLastDownloadedId(channel.ID) ?? 0;
        _logger.LogInformation("Starting at message_id={message_id}", startId);

        int totalWrites = 0, totalMismatches = 0, totalErrors = 0, totalCount = 0;

        await foreach (var messageBase in _telegramService.ScrollHistory(channel, (int)startId))
        {
            ct.ThrowIfCancellationRequested();

            if (messageBase is not Message message)
                continue;

            ProcessingResult result;
            try
            {
                result = await ProcessMessage(message, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to archive {message_id}", message.ID);
                result = new ProcessingResult() { Success = false };
            }
            
            _backupDb.SetLastDownloadedId(channel.ID, message.ID);

            if (result.HashMismatch)
                totalMismatches++;

            if (!result.Success)
                totalErrors++;

            if (result.Write)
                totalWrites++;

            totalCount++;
        }

        _logger.LogInformation("Result: {cou} messages processed, {wri} files written, {mis} hash mismatches, {err} errors", totalCount, totalWrites, totalMismatches, totalErrors);
        _lifetime.StopApplication();
    }

    private async Task<ProcessingResult> ProcessMessage(Message message, CancellationToken ct)
    {
        var processingResult = new ProcessingResult() { Success = true };

        var postPath = BuildPath(_options.TargetDir, message);
        if (!_options.DryRun)
            Directory.CreateDirectory(postPath);

        DownloadResult downloadResult = null;
        if (message.media is MessageMediaPhoto photo)
        {
            downloadResult = await _photoDownloader.Download(postPath, photo);
        }
        else if (message.media is MessageMediaDocument document)
        {
            downloadResult = await _documentDownloader.Download(postPath, document);
        }

        if (downloadResult != null && !downloadResult.Success)
        {
            _logger.LogError("Error during downloading {fileId}: {error}", downloadResult.FileId, downloadResult.ErrorMessage);
            return processingResult with { Success = false };
        }
        
        if (downloadResult != null)
            processingResult = processingResult with { HashMismatch = downloadResult.HashMismatch, Write = downloadResult.Write };

        var fileId = downloadResult?.FileId;
        string metadataFilePath = Path.Combine(postPath, $"metadata_{message.ID}{(fileId != null ? $"_{fileId.Value}" : string.Empty)}.json");

        if (!_options.DryRun)
        {
            var metadata = JsonConvert.SerializeObject(message, AppOptions.JsonSettings);
            await File.WriteAllTextAsync(metadataFilePath, metadata, ct);
        }

        _logger.LogInformation("Processed post {messageID} from {date}", message.ID, message.Date);
        
        return processingResult;
    }

    private static string BuildPath(string basePath, Message message)
    {
        var date = message.Date.ToLocalTime();
        var postId = message.grouped_id;
        if (postId == 0)
            postId = message.ID;

        return Path.Combine(basePath, $"channel_{message.Peer.ID}", date.ToString("yyyy-MM-dd"), $"post_{postId}_{date.ToString("HH-mm-ss")}");
    }
}

public record ProcessingResult
{
    public bool Success { get; init; }
    public bool Write { get; init; }
    public bool HashMismatch { get; init; }
}

public record DownloadResult
{
    public bool Success { get; init; }
    public string Path { get; init; }
    public long FileId { get; init; }
    public string Hash { get; init; }
    public string ErrorMessage { get; init; }
    public bool Write { get; init; }
    public bool HashMismatch { get; init; }
}
