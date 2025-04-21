using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Globalization;
using Application.Services.Interfaces;
using Domain.Entities.Transaction;
using Domain.Enum;
using Telegram.Bot.Types.ReplyMarkups;

namespace Console;

public class TelegramBotService(
    ITelegramBotClient client,
    IStateService stateService,
    ILogger<TelegramBotService> logger,
    IUserService userService,
    ITransactionService transactionService)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        client.StartReceiving(
            HandleUpdateAsync,
            HandleErrorAsync,
            receiverOptions,
            stoppingToken
        );

        var me = await client.GetMeAsync(stoppingToken);
        logger.LogInformation("🤖 Bot {Username} started!", me.Username);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message && update.Message?.Type == MessageType.Text)
        {
            var chatId = update.Message.Chat.Id;
            var messageTexts = update.Message.Text;
            var currentState = stateService.GetState(chatId);

            if (currentState == "awaiting_price")
            {
                if (int.TryParse(messageTexts, out var price))
                {
                    stateService.SetTempData(chatId, "price", price);
                    stateService.SetState(chatId, "awaiting_description");

                    await botClient.SendTextMessageAsync(chatId,
                        "📝 لطفاً توضیح تراکنش را وارد کنید.",
                        cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.SendTextMessageAsync(chatId,
                        "❌ مبلغ وارد شده معتبر نیست. لطفاً فقط عدد وارد کنید.",
                        cancellationToken: cancellationToken);
                }

                return;
            }

            if (currentState == "awaiting_description")
            {
                var price = stateService.GetTempData<int>(chatId, "price");
                var transactionType = stateService.GetTempData<string>(chatId, "transaction_type");

                var transaction = new UserTransaction
                {
                    ChatId = chatId,
                    Price = price,
                    Description = messageTexts,
                    CreateDate = DateTime.UtcNow,
                    TransactionType = transactionType == "Decrease"
                        ? TransactionType.Decrease
                        : TransactionType.Increase,
                    Status = TransactionStatus.Success
                };

                await transactionService.CreateTransactionAsync(transaction);
                stateService.ClearState(chatId);
                stateService.ClearTempData(chatId);

                await botClient.SendTextMessageAsync(chatId,
                    "✅ تراکنش با موفقیت ثبت شد!",
                    cancellationToken: cancellationToken);

                await botClient.SendTextMessageAsync(chatId, "📋 دستور بعدی چیه؟",
                    replyMarkup: GetMainMenuKeyboard(), cancellationToken: cancellationToken);

                return;
            }

            // مدیریت دستور /start
            if (messageTexts != null && messageTexts.StartsWith("/start"))
            {
                var username = update.Message.From?.Username;
                if (username != null)
                {
                    var user = await userService.GetUserByUsernameAsync(username);

                    if (user == null)
                    {
                        user = new User
                        {
                            ChatId = chatId,
                            FirstName = update.Message?.From?.FirstName,
                            LastName = update.Message?.From?.LastName,
                            CreateDate = DateTime.UtcNow,
                            AccountCode = null,
                            UserName = username,
                        };

                        await userService.CreateUserAsync(user);
                    }
                }

                var iranTimeString = GetIranTimeString(DateTime.UtcNow);

                var firstName = update.Message?.From?.FirstName;
                var welcomeMessage = $"سلام {firstName} عزیز! به ربات خوش اومدی 🎉\n" +
                                     $"🗓 تاریخ ورود: {iranTimeString.Split('\n')[0]}\n" +
                                     $"⏰ ساعت ورود: {iranTimeString.Split('\n')[1]}\n" +
                                     $"چه کاری می‌تونم برات انجام بدم؟ 🤖";

                await botClient.SendTextMessageAsync(chatId, welcomeMessage, cancellationToken: cancellationToken);

                var inlineKeyboard = GetMainMenuKeyboard();

                await botClient.SendTextMessageAsync(chatId, "لطفاً یکی از گزینه‌های زیر را انتخاب کن:",
                    replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
            }
        }

        // مدیریت CallbackQuery ها
        if (update.Type == UpdateType.CallbackQuery)
        {
            var callbackQuery = update.CallbackQuery;
            if (callbackQuery == null || callbackQuery.Message == null) return;

            var chatId = callbackQuery.Message.Chat.Id;
            var callbackData = callbackQuery.Data ?? string.Empty;

            switch (callbackData)
            {
                case "view_transactions":
                    var filterKeyboard = new InlineKeyboardMarkup(
                        new[]
                        {
                            new[]
                            {
                                InlineKeyboardButton.WithCallbackData("📥 واریزها", "filter_increase"),
                                InlineKeyboardButton.WithCallbackData("📤 برداشت‌ها", "filter_decrease"),
                            }
                        });

                    await botClient.EditMessageTextAsync(chatId, callbackQuery.Message.MessageId,
                        "🔍 لطفاً نوع تراکنش‌هایی که می‌خوای ببینی رو انتخاب کن:", replyMarkup: filterKeyboard,
                        cancellationToken: cancellationToken);
                    break;

                case "add_transactions":
                    await AddTransaction(botClient, callbackQuery, cancellationToken);
                    break;

                case "view_balance":
                    var balance = await userService.GetUserBalanceAsync(chatId);
                    var balanceMsg = $"💰 موجودی شما: {balance:#,0} تومان";
                    await EditOrSendMenuAsync(botClient, callbackQuery, balanceMsg, cancellationToken);
                    break;

                case "Increase":
                case "Decrease":
                    await HandleTransactionType(botClient, chatId, callbackData, callbackQuery.Message.MessageId,
                        cancellationToken);
                    break;

                case "filter_increase":
                    await FilterTransactionsByType(botClient, callbackQuery, TransactionType.Increase,
                        cancellationToken);
                    break;

                case "filter_decrease":
                    await FilterTransactionsByType(botClient, callbackQuery, TransactionType.Decrease,
                        cancellationToken);
                    break;

                case "back_to_menu":
                    await botClient.EditMessageTextAsync(chatId, callbackQuery.Message.MessageId,
                        "📋 دستور بعدی چیه؟", replyMarkup: GetMainMenuKeyboard(),
                        cancellationToken: cancellationToken);
                    break;

                default:
                    await botClient.EditMessageTextAsync(chatId, callbackQuery.Message.MessageId,
                        "❓ دکمه ناشناخته.", cancellationToken: cancellationToken);
                    break;
            }

            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
        }
    }

    private async Task AddTransaction(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        var transactionTypeKeyboard = new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("➕ واریز", "Increase"),
                    InlineKeyboardButton.WithCallbackData("➖ برداشت", "Decrease")
                }
            });

        if (callbackQuery.Message != null)
        {
            await botClient.EditMessageTextAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId,
                "لطفاً نوع تراکنش را انتخاب کنید:", replyMarkup: transactionTypeKeyboard,
                cancellationToken: cancellationToken);

            stateService.SetState(callbackQuery.Message.Chat.Id, "awaiting_transaction_type");
        }
    }

    private async Task HandleTransactionType(ITelegramBotClient botClient, long chatId, string transactionType,
        int messageId, CancellationToken cancellationToken)
    {
        if (stateService.GetState(chatId) == "awaiting_transaction_type")
        {
            stateService.SetState(chatId, "awaiting_price");
            stateService.SetTempData(chatId, "transaction_type", transactionType);

            await botClient.EditMessageTextAsync(chatId, messageId,
                "💰 لطفاً مبلغ تراکنش را وارد کنید (فقط عدد):",
                cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.EditMessageTextAsync(chatId, messageId,
                "❌ لطفاً ابتدا گزینه «افزودن تراکنش» را انتخاب کنید.", cancellationToken: cancellationToken);
        }
    }

    private async Task FilterTransactionsByType(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        TransactionType type, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message != null)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var transactions = await transactionService.GetTransactionsAsync(chatId);

            var filtered = transactions.Where(t => t.TransactionType == type).ToList();

            string message;
            if (!filtered.Any())
            {
                message = "❌ تراکنشی از این نوع پیدا نشد.";
            }
            else
            {
                message = "📄 تراکنش‌های فیلتر شده:\n\n";
                foreach (var item in filtered)
                {
                    var iranTimeString = GetIranTimeString(item.CreateDate);
                    string status = item.Status == TransactionStatus.Success ? "موفق" : "ناموفق";
                    string transactionTypeString =
                        item.TransactionType == TransactionType.Increase ? "واریز" : "برداشت";

                    message +=
                        $"📅 {iranTimeString.Split('\n')[0]}\n" +
                        $"⏰ {iranTimeString.Split('\n')[1]}\n" +
                        $"💸 مبلغ: {item.Price:#,0} تومان\n" +
                        $"📊 نوع: {transactionTypeString}\n" +
                        $"📝 توضیح: {item.Description}\n" +
                        $"📌 وضعیت: {status}\n\n" +
                        "🟰🟰🟰🟰🟰\n";
                }
            }

            await EditOrSendMenuAsync(botClient, callbackQuery, message, cancellationToken);
        }
    }

    private async Task EditOrSendMenuAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        string messageText, CancellationToken cancellationToken)
    {
        var keyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🔙 بازگشت به منو", "back_to_menu") }
        });

        try
        {
            if (callbackQuery.Message != null)
                await botClient.EditMessageTextAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.MessageId,
                    text: messageText,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
        }
        catch
        {
            if (callbackQuery.Message != null)
                await botClient.SendTextMessageAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    text: messageText,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
        }
    }

    private string GetIranTimeString(DateTime utcDateTime)
    {
        var iranTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
        var iranDateTime = TimeZoneInfo.ConvertTimeFromUtc(utcDateTime, iranTimeZone);
        var pc = new PersianCalendar();
        var date =
            $"{pc.GetYear(iranDateTime):0000}/{pc.GetMonth(iranDateTime):00}/{pc.GetDayOfMonth(iranDateTime):00}";
        var time = $"{pc.GetHour(iranDateTime):00}:{pc.GetMinute(iranDateTime):00}";
        return $"📅 تاریخ: {date}\n⏰ ساعت: {time}";
    }

    private InlineKeyboardMarkup GetMainMenuKeyboard()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("مشاهده تراکنشات", "view_transactions") },
            new[] { InlineKeyboardButton.WithCallbackData("افزودن تراکنش", "add_transactions") },
            new[] { InlineKeyboardButton.WithCallbackData("مشاهده موجودی", "view_balance") }
        });
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}