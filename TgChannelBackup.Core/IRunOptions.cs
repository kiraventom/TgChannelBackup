namespace TgChannelBackup.Core;

public interface IRunOptions : IDownloadOptions
{
    long ChannelId { get; }
    string TargetDir { get; }
    long? StartId { get; }
    string IdFile { get; }
}
