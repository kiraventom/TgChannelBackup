using LiteDB;

namespace TgChannelBackup.Core;

public class BackupDb : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<ChannelRecord> _channels;

    public long? GetLastDownloadedId(long channelId)
    {
        var channel = _channels.FindOne(c => c.ChannelId == channelId);
        return channel?.LastMessageId;
    }

    public void SetLastDownloadedId(long channelId, long lastDownloadedId)
    {
        var channel = new ChannelRecord()
        {
            ChannelId = channelId,
            LastMessageId = lastDownloadedId
        };

        _channels.Upsert(channel);
    }

    public BackupDb(string path)
    {
        _db = new(path);
        _channels = _db.GetCollection<ChannelRecord>();
        _channels.EnsureIndex(r => r.ChannelId, unique: true);
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}

public record ChannelRecord
{
    [BsonId]
    public long ChannelId { get; init; }
    public long? LastMessageId { get; init; }
}
