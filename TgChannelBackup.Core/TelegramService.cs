using Microsoft.Extensions.Logging;
using TL;
using WTelegram;

namespace TgChannelBackup.Core;

public class TelegramService : IAsyncDisposable
{
    private readonly Client _client;
    private readonly ILogger<TelegramService> _logger;

    public TelegramService(ILogger<TelegramService> logger)
    {
        _logger = logger;
        _client = new Client(Config);
    }

    private static string Config(string key) => key switch
    {
        "api_id" => Environment.GetEnvironmentVariable("TG_API_ID"),
        "api_hash" => Environment.GetEnvironmentVariable("TG_API_HASH"),
        "phone_number" => Environment.GetEnvironmentVariable("TG_PHONE"),
        "password" => Environment.GetEnvironmentVariable("TG_PASSWORD"),
        "verification_code" => GetVerificationCode(),
        _ => null
    };

    private static string GetVerificationCode()
    {
        Console.Write("\nCode: ");
        return Console.ReadLine();
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }

    public async Task LogIn()
    {
        await _client.LoginUserIfNeeded();
        _logger.LogInformation("Logged in as {user}", _client.User);
    }

    public async Task DownloadFile(MessageMediaPhoto photo, string path)
    {
        await using var fs = File.OpenWrite(path);
        await _client.DownloadFileAsync((Photo)photo.photo, fs);
    }

    public async Task DownloadFile(MessageMediaDocument document, string path)
    {
        await using var fs = File.OpenWrite(path);
        await _client.DownloadFileAsync((Document)document.document, fs);
    }
    
    public async Task<InputPeerChannel> GetChannelById(long channelId)
    {
        var chats = await _client.Messages_GetAllChats();
        var channel = chats.chats[channelId] as TL.Channel;
        var inputPeerChannel = (InputPeerChannel)channel.ToInputPeer();
        return inputPeerChannel;
    }

    public async IAsyncEnumerable<MessageBase> ScrollHistory(InputPeerChannel channel, int start)
    {
        int offset = start != 0 ? start : await GetFirstMessageId(channel);

        const int limit = 100;
        while (true)
        {
            var history = await _client.Messages_GetHistory(
                    channel,
                    offset_id: offset,
                    add_offset: -limit,
                    limit: limit);

            foreach (var message in history.Messages.OrderBy(m => m.ID))
                yield return message;

            if (history.Messages.Length <= 1)
                break;

            offset = history.Messages.Max(m => m.ID);
        }
    }

    public async Task<InputPeerChannel> GetCommentsGroup(InputPeerChannel channel)
    {
        var fullChannel = await _client.GetFullChat(channel);
        var key = fullChannel.chats.Keys.FirstOrDefault(k => k != channel.channel_id);
        if (key == default)
            return null;

        var groupChat = fullChannel.chats[key];
        return (InputPeerChannel)groupChat.ToInputPeer();
    }

    private async Task<int> GetFirstMessageId(InputPeerChannel channel)
    {
        var history = await _client.Messages_GetHistory(channel);
        if (history.Messages.Length == 0)
            return 0; // channel is empty

        const int count = 100;
        for (int i = 1; ; i += count)
        {
            var ids = Enumerable.Range(i, count);
            var messages = await _client.GetMessages(channel, i);
            if (messages.Messages.Length != 0)
            {
                var minId = messages.Messages.Min(m => m.ID);
                _logger.LogInformation("First message ID found: {id}", minId);
                return minId;
            }
        }
    }
}
