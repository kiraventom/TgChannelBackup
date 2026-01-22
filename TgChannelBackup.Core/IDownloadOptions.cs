namespace TgChannelBackup.Core;

public interface IDownloadOptions
{
    bool DryRun { get; }
    bool Reconcile { get; }
}

