namespace TgChannelBackup.Core;

public record RunOptions
{
    public long ChannelId { get; init; }
    public string TargetDir { get; init; }
    public long? StartId { get; init; }
    public string IdFile { get; init; }
    public bool DryRun { get; init; }
    public bool Reconcile { get; init; }
}
