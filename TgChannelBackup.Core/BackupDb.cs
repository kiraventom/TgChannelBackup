using LiteDB;

namespace TgChannelBackup.Core;

public class BackupDb : IDisposable
{
    private readonly LiteDatabase _db;
    private readonly ILiteCollection<ChannelRecord> _channels;
    private readonly ILiteCollection<StateRecord> _state;

    public long? LastDownloadedId
    {
        get => _state.FindAll().SingleOrDefault()?.LastMessageId;
        set
        {
            if (value is {} lastDownloadedId)
                _state.Upsert(new StateRecord() { StateRecordId = 999, LastMessageId = lastDownloadedId });
            else
                _state.DeleteAll();
        }
    }

    public BackupDb(string path)
    {
        _db = new(path);
        _channels = _db.GetCollection<ChannelRecord>();
        _channels.EnsureIndex(r => r.ChannelId);
        _state = _db.GetCollection<StateRecord>();
        _state.EnsureIndex(r => r.StateRecordId);
    }

    public void Upsert(long channelId, DateTime dateTime, int messageId, long? fileId, string hash, string metadataFileName)
    {
        var channel = _channels.FindById(channelId);
        if (channel is null)
            _channels.Insert(channel = new ChannelRecord() { ChannelId = channelId, Days = [] });

        var day = channel.Days.FirstOrDefault(d => d.Date == DateOnly.FromDateTime(dateTime));
        if (day is null)
            channel.Days.Add(day = new DayRecord() { Date = DateOnly.FromDateTime(dateTime), Posts = []});

        var post = day.Posts.FirstOrDefault(p => p.MessageId == messageId);
        if (post is null)
            day.Posts.Add(post = new PostRecord() { MessageId = messageId, Time = TimeOnly.FromDateTime(dateTime), MetadataFileName = metadataFileName, Files = [] });

        if (fileId != null)
            post.Files.Add(new FileRecord() { FileId = fileId.Value, Hash = hash });
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}

public record ChannelRecord
{
    public long ChannelId { get; init; }
    public List<DayRecord> Days { get; init; }
}

public record DayRecord
{
    public DateOnly Date { get; init; }
    public List<PostRecord> Posts { get; init; }
}

public record PostRecord
{
    public int MessageId { get; init; }
    public string MetadataFileName { get; init; }
    public TimeOnly Time { get; init; }
    public List<FileRecord> Files { get; init; }
}

public record CommentRecord
{
    public string Text { get; init; }
    public long SenderId { get; init; }
    public long? ReplyTo { get; init; }
    public List<FileRecord> Media { get; init; }
}

public record FileRecord
{
    public long FileId { get; init; }
    public string Hash { get; init; }
}

public record StateRecord
{
    public long StateRecordId { get; init; }
    public long LastMessageId { get; init; }
}
