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
        "verification_code" => GetVerificationCode(),
        "password" => Environment.GetEnvironmentVariable("TG_PASSWORD"),
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
    
    public async Task<InputPeerChannel> GetChannelById(long channelId)
    {
        var chats = await _client.Messages_GetAllChats();
        var channel = chats.chats[channelId] as TL.Channel;
        var inputPeerChannel = (InputPeerChannel)channel.ToInputPeer();
        return inputPeerChannel;
    }

    public async IAsyncEnumerable<MessageBase> ScrollHistory(InputPeerChannel channel, int minId = 0)
    {
        int offset = 0;
        const int limit = 100;
        while (true)
        {
            var history = await _client.Messages_GetHistory(
                    channel,
                    offset_id: offset,
                    add_offset: 0,
                    limit: limit,
                    max_id: 0,
                    min_id: minId);

            if (history.Messages.Length == 0)
                break;

            foreach (var message in history.Messages.OrderBy(m => m.ID))
                yield return message;

            offset = history.Messages[^1].ID;
        }
    }
}
