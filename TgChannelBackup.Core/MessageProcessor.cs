using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using TgChannelBackup.Core.Downloader;
using TL;

namespace TgChannelBackup.Core;

public class MessageProcessor(ILogger<MessageProcessor> logger, PhotoDownloader photoDownloader, DocumentDownloader documentDownloader)
{
    public async Task<DownloadResult> ProcessMessage(Message message, string postPath, bool dryRun, CancellationToken ct)
    {
        if (!dryRun)
            Directory.CreateDirectory(postPath);

        DownloadResult downloadResult = null;
        if (message.media is MessageMediaPhoto photo)
        {
            downloadResult = await photoDownloader.Download(postPath, photo);
        }
        else if (message.media is MessageMediaDocument document)
        {
            downloadResult = await documentDownloader.Download(postPath, document);
        }

        if (downloadResult != null && !downloadResult.Success)
        {
            logger.LogError("Error during downloading {fileId}: {error}", downloadResult.FileId, downloadResult.ErrorMessage);
            return downloadResult;
        }
        
        var fileId = downloadResult?.FileId;
        string metadataFilePath = Path.Combine(postPath, $"metadata_{message.ID}{(fileId != null ? $"_{fileId.Value}" : string.Empty)}.json");

        if (!dryRun)
        {
            var metadata = JsonConvert.SerializeObject(message, AppOptions.JsonSettings);
            await File.WriteAllTextAsync(metadataFilePath, metadata, ct);
        }

        logger.LogInformation("Processed post {messageID} from {date}", message.ID, message.Date);
        
        return downloadResult;
    }

    public static string BuildPath(string basePath, Message message)
    {
        var date = message.Date.ToLocalTime();
        var postId = message.grouped_id;
        if (postId == 0)
            postId = message.ID;

        return Path.Combine(basePath, $"channel_{message.Peer.ID}", date.ToString("yyyy-MM-dd"), $"post_{postId}_{date.ToString("HH-mm-ss")}");
    }
}

