using TgChannelBackup.Core;

namespace TgChannelBackup.Cli;

public record RunOptions : IRunOptions
{
    public long ChannelId { get; init; }
    public string TargetDir { get; init; }
    public long? StartId { get; init; }
    public string IdFile { get; init; }
    public bool DryRun { get; init; }
    public bool Reconcile { get; init; }
}

