using LiteDB;

namespace TgChannelBackup.Core;

public class BackupDb : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<ChannelRecord> _channels;
    private readonly ILiteCollection<CommentGroupRecord> _commentGroup;

    public long? GetLastPostId(long channelId)
    {
        var channel = _channels.FindOne(c => c.ChannelId == channelId);
        return channel?.LastPostId;
    }

    public void SetLastPostId(long channelId, long lastPostId, long? commentGroupId)
    {
        var channel = new ChannelRecord()
        {
            ChannelId = channelId,
            LastPostId = lastPostId,
            CommentGroupId = commentGroupId
        };

        _channels.Upsert(channel);
    }

    public long? GetLastCommentId(long commentGroupId)
    {
        var commentGroup = _commentGroup.FindOne(c => c.CommentGroupId == commentGroupId);
        return commentGroup?.LastCommentId;
    }

    public void SetLastCommentId(long commentGroupId, long lastCommentId)
    {
        var commentGroup = new CommentGroupRecord()
        {
            CommentGroupId = commentGroupId,
            LastCommentId = lastCommentId
        };

        _commentGroup.Upsert(commentGroup);
    }

    public BackupDb(string path)
    {
        _db = new(path);
        _channels = _db.GetCollection<ChannelRecord>();
        _channels.EnsureIndex(r => r.ChannelId, unique: true);

        _commentGroup = _db.GetCollection<CommentGroupRecord>();
        _commentGroup.EnsureIndex(r => r.CommentGroupId, unique: true);
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
    public long? LastPostId { get; init; }
    public long? CommentGroupId { get; init; }
}

public record CommentGroupRecord
{
    [BsonId]
    public long CommentGroupId { get; init; }
    public long? LastCommentId { get; init; }
}
