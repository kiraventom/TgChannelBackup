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

    public BackupWorker(TelegramService telegramService, BackupDb archiveIndex, RunOptions options, PhotoDownloader photoDownloader, DocumentDownloader documentDownloader, ILogger<BackupWorker> logger)
    {
        _telegramService = telegramService;
        _backupDb = archiveIndex;
        _options = options;
        _photoDownloader = photoDownloader;
        _documentDownloader = documentDownloader;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _telegramService.LogIn();

        var lastId = _options.StartId ?? _backupDb.LastDownloadedId ?? 0;
        _logger.LogInformation("Starting at message_id={message_id}", lastId);

        var channel = await _telegramService.GetChannelById(_options.ChannelId);

        await foreach (var messageBase in _telegramService.ScrollHistory(channel, minId: (int)lastId))
        {
            if (messageBase is not Message message)
                continue;

            try
            {
                await ProcessMessage(message, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to archive {message_id}", message.ID);
            }
            
            _backupDb.LastDownloadedId = messageBase.ID;
        }
    }

    private async Task ProcessMessage(Message message, CancellationToken ct)
    {
        var postPath = BuildPath(_options.TargetDir, message);
        if (!_options.DryRun)
            Directory.CreateDirectory(postPath);

        DownloadResult result = null;
        if (message.media is MessageMediaPhoto photo)
        {
            result = await _photoDownloader.Download(postPath, photo);
        }
        else if (message.media is MessageMediaDocument document)
        {
            result = await _documentDownloader.Download(postPath, document);
        }

        if (result != null && !result.Success)
        {
            _logger.LogError("Error during downloading {fileId}: {error}", result.FileId, result.ErrorMessage);
            return;
        }

        var fileId = result?.FileId;
        string metadataFilePath = Path.Combine(postPath, $"metadata_{message.ID}{(fileId != null ? $"_{fileId.Value}" : string.Empty)}.json");

        if (!_options.DryRun)
        {
            var metadata = JsonConvert.SerializeObject(message, AppOptions.JsonSettings);
            await File.WriteAllTextAsync(metadataFilePath, metadata, ct);
        }

        /* _backupDb.Upsert(message.Peer.ID, message.Date.ToLocalTime(), message.ID, fileId, hash, metadataFilePath); */
        _logger.LogInformation("Processed post {messageID} from {date}", message.ID, message.Date);
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

public record DownloadResult
{
    public bool Success { get; init; }
    public string Path { get; init; }
    public long FileId { get; init; }
    public string Hash { get; init; }
    public string ErrorMessage { get; init; }
}
