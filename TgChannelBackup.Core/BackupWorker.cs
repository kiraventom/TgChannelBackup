using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TL;

namespace TgChannelBackup.Core;

public class BackupWorker : BackgroundService
{
    private readonly TelegramService _telegramService;
    private readonly BackupDb _backupDb;
    private readonly IRunOptions _options;
    private readonly MessageProcessor _processor;
    private readonly ILogger<BackupWorker> _logger;
    private readonly IHostApplicationLifetime _lifetime;

    public BackupWorker(TelegramService telegramService, BackupDb archiveIndex, IRunOptions options, MessageProcessor processor, ILogger<BackupWorker> logger, IHostApplicationLifetime lifetime)
    {
        _telegramService = telegramService;
        _backupDb = archiveIndex;
        _options = options;
        _processor = processor;
        _logger = logger;
        _lifetime = lifetime;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await _telegramService.LogIn();

        var channelId = _options.ChannelId;
        var channel = await _telegramService.GetChannelById(channelId);
        var commentGroup = await _telegramService.GetCommentsGroup(channel);
        var commentGroupId = commentGroup?.ID;

        var startId = _options.StartId ?? _backupDb.GetLastPostId(channel.ID) ?? 0;

        _logger.LogInformation("Processing {channelId} posts", channel.ID);
        await ProcessMessages(channel, startId, m => _backupDb.SetLastPostId(channel.ID, m, commentGroupId), m => true, ct);
        _logger.LogInformation("Processed {channelId} posts", channel.ID);

        if (commentGroup is not null)
        {
            var commentsStartId = _backupDb.GetLastCommentId(commentGroup.ID) ?? 0;
            _logger.LogInformation("Processing {channelId} comments {commentsGroupId}", channelId, commentGroup.ID);
            await ProcessMessages(commentGroup, commentsStartId, 
                m => _backupDb.SetLastCommentId(commentGroup.ID, m), 
                m => m.from_id is not TL.PeerChannel,
                ct);
            _logger.LogInformation("Processed {channelId} comments {commentsGroupId}", channelId, commentGroup.ID);
        }

        _lifetime.StopApplication();
    }

    private async Task ProcessMessages(InputPeerChannel channel, long startId, Action<long> onSetLastId, Func<Message, bool> shouldSave, CancellationToken ct)
    {
        _logger.LogInformation("Starting at message_id={message_id}", startId);

        int totalWrites = 0, totalMismatches = 0, totalErrors = 0, totalCount = 0;

        await foreach (var messageBase in _telegramService.ScrollHistory(channel, (int)startId))
        {
            ct.ThrowIfCancellationRequested();

            if (messageBase is not Message message)
                continue;

            if (!shouldSave(message))
            {
                _logger.LogDebug("Skipped message_id={message_id} due to condition", message.ID);
                await Task.Delay(20);
                continue;
            }

            DownloadResult result;
            try
            {
                var postPath = MessageProcessor.BuildPath(_options.TargetDir, message);
                result = await _processor.ProcessMessage(message, postPath, _options.DryRun, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to archive {message_id}", message.ID);
                result = new DownloadResult() { Success = false };
            }
            
            onSetLastId(message.ID);

            if (result.HashMismatch)
                totalMismatches++;

            if (!result.Success)
                totalErrors++;

            if (result.Write)
                totalWrites++;

            totalCount++;
        }

        _logger.LogInformation("Result: {cou} posts processed, {wri} files written, {mis} hash mismatches, {err} errors", totalCount, totalWrites, totalMismatches, totalErrors);
    }
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
