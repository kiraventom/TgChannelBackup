using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using TL;

namespace TgChannelBackup.Core.Downloader;

public abstract class MediaDownloader<T> where T : MessageMedia
{
    protected readonly ILogger<MediaDownloader<T>> _logger;
    protected readonly RunOptions _runOptions;
    protected readonly TelegramService _telegramService;

    protected MediaDownloader(ILogger<MediaDownloader<T>> logger, RunOptions runOptions, TelegramService telegramService)
    {
        _logger = logger;
        _runOptions = runOptions;
        _telegramService = telegramService;
    }

    public async Task<DownloadResult> Download(string postPath, T media)
    {
        try
        {
            return await DownloadInternal(postPath, media);
        }
        catch (Exception ex)
        {
            return new DownloadResult()
            {
                Success = false,
                ErrorMessage = ex.Message,
                FileId = GetFileId(media)
            };
        }
    }

    protected abstract long GetFileId(T media);
    protected abstract string GetFileName(T media);
    protected abstract Task DownloadMedia(T media, string path);

    private async Task<DownloadResult> DownloadInternal(string postPath, T media)
    {
        var filePath = Path.Combine(postPath, GetFileName(media));
        bool fileExists = File.Exists(filePath);

        var tempPath = Path.GetTempFileName();
        await DownloadMedia(media, tempPath);

        var newHash = GetMD5(tempPath);

        bool writeFile = true;
        bool hashMismatch = false;
        if (fileExists)
        {
            writeFile = false;

            var existingHash = GetMD5(filePath); // TODO Database
            hashMismatch = !newHash.Equals(existingHash, StringComparison.OrdinalIgnoreCase);
            if (hashMismatch)
            {
                _logger.LogWarning("File {path} hash mismatch, {existingHash} != {newHash}", filePath, existingHash, newHash);
                if (_runOptions.Reconcile)
                    writeFile = true;
            }
            else
            {
                _logger.LogInformation("Hashes match at '{path}'", filePath);
            }
        }

        if (_runOptions.DryRun)
            writeFile = false;

        if (writeFile)
        {
            if (fileExists)
                File.Delete(filePath);

            File.Copy(tempPath, filePath);
            _logger.LogInformation("Write file at '{path}'", filePath);
        }

        File.Delete(tempPath);

        return new DownloadResult()
        {
            Success = true,
            FileId = GetFileId(media),
            Path = filePath,
            Hash = newHash,
            Write = writeFile,
            HashMismatch = hashMismatch
        };
    }

    protected static string GetMD5(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        return GetMD5(stream);
    }

    protected static string GetMD5(Stream stream)
    {
        using var md5 = MD5.Create();
        var hashArr = md5.ComputeHash(stream);
        return BitConverter.ToString(hashArr).Replace("-", "").ToLowerInvariant();
    }
}

