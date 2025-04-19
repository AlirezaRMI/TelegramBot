using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

public class TelegramBotService(ITelegramBotClient client) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Console.WriteLine("Bot is up and running...");

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        client.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            cancellationToken: stoppingToken
        );

        await Task.CompletedTask;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { Text: { } messageText })
            return;

        var user = update.Message.From;
        var datetime = DateTime.Now;
        var chatId = update.Message.Chat.Id;
        Console.WriteLine($"📩 پیام جدید از {chatId}: {messageText}");


        var response = $"سلام @{user?.FirstName}! خوش اومدی ✌️\n" +
                       $"🕒 زمان: {datetime:HH:mm:ss}\n" +
                       $"📅 تاریخ: {datetime:yyyy/MM/dd}\n" +
                       $"چه کاری میتونم برات انجام بدم{user?.FirstName}\nعزیز";


        await bot.SendMessage(
            chatId,
            response,
            cancellationToken: cancellationToken
        );
    }

    private Task HandleErrorAsync(ITelegramBotClient bot, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"❌ خطا: {exception.Message}");
        return Task.CompletedTask;
    }
}