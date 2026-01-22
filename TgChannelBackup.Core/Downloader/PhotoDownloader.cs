using Microsoft.Extensions.Logging;
using TL;

namespace TgChannelBackup.Core.Downloader;

public class PhotoDownloader : MediaDownloader<MessageMediaPhoto>
{
    public PhotoDownloader(ILogger<MediaDownloader<MessageMediaPhoto>> logger, IDownloadOptions downloadOptions, TelegramService telegramService) : base(logger, downloadOptions, telegramService)
    {
    }

    protected override Task DownloadMedia(MessageMediaPhoto media, string path) => _telegramService.DownloadFile(media, path);

    protected override long GetFileId(MessageMediaPhoto media) => media.photo.ID;

    protected override string GetFileName(MessageMediaPhoto media) => Path.ChangeExtension(GetFileId(media).ToString(), "png");
}

