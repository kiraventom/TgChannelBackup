using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Events;
using TL;

namespace TgChannelBackup.Core;

public class BackupWorker : BackgroundService
{
    private readonly TelegramService _telegramService;
    private readonly BackupDb _backupDb;
    private readonly RunOptions _options;
    private readonly ILogger<BackupWorker> _logger;

    public BackupWorker(TelegramService telegramService, BackupDb archiveIndex, RunOptions options, ILogger<BackupWorker> logger)
    {
        _telegramService = telegramService;
        _backupDb = archiveIndex;
        _options = options;
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

        long fileId;
        string hash;
        if (message.media is MessageMediaPhoto photo)
        {
            fileId = await DownloadPhotoIfNeeded(photo, postPath, ct);
            hash = "TODO";
        }
        else if (message.media is MessageMediaDocument document)
        {
            await DownloadDocumentIfNeeded(document, postPath, ct);
            fileId = 2;
            hash = "TODO";
        }
        else
        {
            return;
        }

        string metadataFilePath = Path.Combine(postPath, "metadata.json");
        if (!_options.DryRun)
        {
            var metadata = JsonSerializer.Serialize(message, AppOptions.Serializer);
            await File.WriteAllTextAsync(metadataFilePath, metadata, ct);
        }

        _backupDb.Upsert(message.Peer.ID, message.Date.ToLocalTime(), message.ID, fileId, hash, metadataFilePath);
        _logger.LogInformation("Processed post {messageID} from {date}", message.ID, message.Date);
    }

    private async Task<long> DownloadPhotoIfNeeded(MessageMediaPhoto photo, string postPath, CancellationToken ct)
    {
        var filePath = Path.ChangeExtension(Path.Combine(postPath, photo.photo.ID.ToString()), "png");
        bool fileExists = File.Exists(filePath);

        var tempPath = Path.GetTempFileName();
        using (var s = File.OpenWrite(tempPath))
        {
            using var binaryWriter = new BinaryWriter(s);
            photo.photo.WriteTL(binaryWriter);
        }

        bool writeFile = true;
        if (fileExists)
        {
            writeFile = false;

            var newHash = GetMD5(tempPath);
            var existingHash = GetMD5(filePath); // TODO Database
            if (!newHash.Equals(existingHash, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("File {path} hash mismatch, {existingHash} != {newHash}", filePath, existingHash, newHash);
                if (_options.Reconcile)
                    writeFile = true;
            }
        }

        if (writeFile && !_options.DryRun)
        {
            if (fileExists)
                File.Delete(filePath);

            File.Copy(tempPath, filePath);
        }

        File.Delete(tempPath);

        var logLevel = _options.DryRun ? LogLevel.Information : LogLevel.Debug;
        _logger.Log(logLevel, "Write file at '{path}'", filePath);

        return photo.photo.ID;
    }

    private async Task DownloadDocumentIfNeeded(MessageMediaDocument doc, string path, CancellationToken ct)
    {
        // TODO
    }

    private static string BuildPath(string basePath, Message message)
    {
        var date = message.Date.ToLocalTime();
        var postId = message.grouped_id;
        if (postId == 0)
            postId = message.ID;

        return Path.Combine(basePath, $"channel_{message.Peer.ID}", date.ToString("yyyy-MM-dd"), $"post_{postId}_{date.ToString("HH-mm-ss")}");
    }

    private static string GetMD5(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return GetMD5(stream);
    }

    private static string GetMD5(Stream stream)
    {
        using var md5 = MD5.Create();
        var hashArr = md5.ComputeHash(stream);
        return BitConverter.ToString(hashArr).Replace("-", "").ToLowerInvariant();
    }
}
