using Microsoft.Extensions.Logging;
using TL;

namespace TgChannelBackup.Core.Downloader;

public class DocumentDownloader : MediaDownloader<MessageMediaDocument>
{
    public DocumentDownloader(ILogger<MediaDownloader<MessageMediaDocument>> logger, RunOptions runOptions, TelegramService telegramService) : base(logger, runOptions, telegramService)
    {
    }

    protected override Task DownloadMedia(MessageMediaDocument media, string path) => _telegramService.DownloadFile(media, path);

    protected override long GetFileId(MessageMediaDocument media) => media.document.ID;

    protected override string GetFileName(MessageMediaDocument media)
    {
        var doc = (Document)media.document;
        var filename = doc.attributes?.OfType<DocumentAttributeFilename>()?.FirstOrDefault()?.file_name;
        var extension = filename != null ? Path.GetExtension(filename) : string.Empty;
        return Path.ChangeExtension(GetFileId(media).ToString(), extension);
    }
}

