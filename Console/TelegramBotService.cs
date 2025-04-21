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
            #region AddTransaction

            var chatId = update.Message.Chat.Id;
            var messageTexts = update.Message.Text;

            if (stateService.GetState(chatId) == "Increase" || stateService.GetState(chatId) == "Decrease")
            {
                if (messageTexts != null)
                {
                    var parts = messageTexts.Split("-", 2, StringSplitOptions.TrimEntries);

                    if (parts.Length != 2 || !int.TryParse(parts[0], out var price))
                    {
                        await botClient.SendTextMessageAsync(chatId,
                            "❌ لطفاً فرمت صحیح وارد کنید. مثال: `70000 - توضیحات`",
                            cancellationToken: cancellationToken);
                        return;
                    }

                    string description = parts[1];
                    string? transactionType = stateService.GetState(chatId);

                    var transaction = new UserTransaction
                    {
                        ChatId = chatId,
                        Price = price,
                        Description = description,
                        CreateDate = DateTime.UtcNow,
                        TransactionType = transactionType == "Decrease"
                            ? TransactionType.Decrease
                            : TransactionType.Increase,
                        Status = TransactionStatus.Success
                    };

                    await transactionService.CreateTransactionAsync(transaction);
                }

                stateService.ClearState(chatId);

                await botClient.SendTextMessageAsync(chatId, "✅ تراکنش با موفقیت ذخیره شد!",
                    cancellationToken: cancellationToken);
            }

            #endregion

            if (messageTexts != null && messageTexts.StartsWith("/start"))
            {
                #region Add User

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

                #endregion

                #region Time Set

                var iranTimeString = GetIranTimeString(DateTime.UtcNow);

                #endregion

                var firstName = update.Message?.From?.FirstName;
                var welcomeMessage = $"سلام {firstName} عزیز! به ربات خوش اومدی 🎉\n" +
                                     $"🗓 تاریخ ورود: {iranTimeString.Split('\n')[0]}\n" +
                                     $"⏰ ساعت ورود: {iranTimeString.Split('\n')[1]}\n" +
                                     $"چه کاری می‌تونم برات انجام بدم نانا؟ 🤖";

                await botClient.SendTextMessageAsync(chatId, welcomeMessage, cancellationToken: cancellationToken);

                #region KeyBord

                var inlineKeyboard = GetMainMenuKeyboard();

                #endregion

                await botClient.SendTextMessageAsync(chatId, "لطفاً یکی از گزینه‌های زیر را انتخاب کن:",
                    replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
            }
        }

        if (update.Type == UpdateType.CallbackQuery)
        {
            var chatId = update.CallbackQuery?.Message?.Chat.Id ?? 0;
            var callbackQuery = update.CallbackQuery;
            if (callbackQuery != null)
            {
                var callbackdata = callbackQuery.Data ?? string.Empty;

                switch (callbackdata)
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

                        await botClient.SendTextMessageAsync(chatId,
                            "🔍 لطفاً نوع تراکنش‌هایی که می‌خوای ببینی رو انتخاب کن:",
                            replyMarkup: filterKeyboard,
                            cancellationToken: cancellationToken);

                        await ViewTransactions(botClient, callbackQuery, cancellationToken);
                        break;

                    case "add_transactions":
                        await AddTransaction(botClient, chatId, cancellationToken);
                        break;

                    case "view_balance":
                        await ViewBalance(botClient, callbackQuery, cancellationToken);
                        break;

                    case "Increase":
                    case "Decrease":
                        await HandleTransactionType(botClient, chatId, callbackdata, cancellationToken);
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
                        var menu = GetMainMenuKeyboard();
                        await botClient.SendTextMessageAsync(chatId, "📋 لطفاً یکی از گزینه‌ها رو انتخاب کن:",
                            replyMarkup: menu, cancellationToken: cancellationToken);
                        break;

                    default:
                        var unknownMessage = "❓ دکمه ناشناخته.";
                        if (callbackQuery.Message?.Chat != null)
                            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, unknownMessage,
                                cancellationToken: cancellationToken);
                        break;
                }
            }

            if (callbackQuery != null)
                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
        }
    }

    private async Task ViewTransactions(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        if (callbackQuery.Message != null)
        {
            var transactions = await transactionService.GetTransactionsAsync(callbackQuery.Message.Chat.Id);

            if (transactions.Any())
            {
                var transactionListMessage = "📒 لیست تراکنش‌ها:\n\n";
                foreach (var item in transactions)
                {
                    string status = item.Status == TransactionStatus.Success ? "موفق" : "ناموفق";
                    string transactionTypeString =
                        item.TransactionType == TransactionType.Increase ? "واریز" : "برداشت";
                    var iranTimeString = GetIranTimeString(item.CreateDate);

                    transactionListMessage +=
                        $"📅 تاریخ: {iranTimeString.Split('\n')[0]}\n" +
                        $"⏰ ساعت: {iranTimeString.Split('\n')[1]}\n" +
                        $"💸 مبلغ: {item.Price:#,0} تومان\n" +
                        $"📊 نوع تراکنش: {transactionTypeString}\n" +
                        $"📝 توضیحات: {item.Description}\n" +
                        $"📊 وضعیت: {status}\n\n" +
                        $"🟰🟰🟰🟰🟰\n";
                }

                await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, transactionListMessage,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id, "❌ شما هیچ تراکنشی ثبت نکرده‌اید.",
                    cancellationToken: cancellationToken);
            }
        }

        var inlineKeyboard = GetMainMenuKeyboard();
        if (callbackQuery.Message != null)
            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id,
                "📋 لطفاً یکی از گزینه‌های زیر رو انتخاب کن:", replyMarkup: inlineKeyboard,
                cancellationToken: cancellationToken);
    }

    private async Task AddTransaction(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var transactionTypeKeyboard = new InlineKeyboardMarkup(
            new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData("➕ واریز", "Increase"),
                    InlineKeyboardButton.WithCallbackData("➖ برداشت", "Decrease")
                }
            }
        );

        await botClient.SendTextMessageAsync(chatId, "لطفاً نوع تراکنش را انتخاب کنید:",
            replyMarkup: transactionTypeKeyboard, cancellationToken: cancellationToken);
        stateService.SetState(chatId, "awaiting_transaction_type");
    }

    private async Task ViewBalance(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        if (callbackQuery.Message != null)
        {
            var balance = await userService.GetUserBalanceAsync(callbackQuery.Message.Chat.Id);
            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id,
                $"💰 موجودی شما: {balance.ToString("#,0")} تومان", cancellationToken: cancellationToken);
        }

        var inlineKeyboardBalance = GetMainMenuKeyboard();
        if (callbackQuery.Message != null)
            await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id,
                "📋 لطفاً یکی از گزینه‌های زیر رو انتخاب کن:", replyMarkup: inlineKeyboardBalance,
                cancellationToken: cancellationToken);
    }

    private async Task HandleTransactionType(ITelegramBotClient botClient, long chatId, string transactionType,
        CancellationToken cancellationToken)
    {
        if (stateService.GetState(chatId) == "awaiting_transaction_type")
        {
            stateService.SetState(chatId, transactionType);
            await botClient.SendTextMessageAsync(chatId,
                "لطفاً مبلغ و توضیح تراکنش را وارد کنید.\n\n📌 مثال: `70000 - حقوق ماهانه`",
                cancellationToken: cancellationToken);
        }
        else
        {
            await botClient.SendTextMessageAsync(chatId, "❌ لطفاً ابتدا گزینه «افزودن تراکنش» را انتخاب کنید.",
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
        return new InlineKeyboardMarkup(
            new[]
            {
                new[] { InlineKeyboardButton.WithCallbackData("مشاهده تراکنشات", "view_transactions") },
                new[] { InlineKeyboardButton.WithCallbackData("افزودن تراکنش", "add_transactions") },
                new[] { InlineKeyboardButton.WithCallbackData("مشاهده موجودی", "view_balance") }
            });
    }

    private async Task FilterTransactionsByType(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        TransactionType type, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message == null) return;

        var chatId = callbackQuery.Message.Chat.Id;
        var transactions = await transactionService.GetTransactionsAsync(chatId);

        var filtered = transactions.Where(t => t.TransactionType == type).ToList();

        if (!filtered.Any())
        {
            await botClient.SendTextMessageAsync(chatId, "❌ تراکنشی از این نوع پیدا نشد.",
                cancellationToken: cancellationToken);
        }
        else
        {
            var message = "📄 تراکنش‌های فیلتر شده:\n\n";

            foreach (var item in filtered)
            {
                var iranTimeString = GetIranTimeString(item.CreateDate);
                string status = item.Status == TransactionStatus.Success ? "موفق" : "ناموفق";
                string transactionTypeString = item.TransactionType == TransactionType.Increase ? "واریز" : "برداشت";

                message +=
                    $"📅 {iranTimeString.Split('\n')[0]}\n" +
                    $"⏰ {iranTimeString.Split('\n')[1]}\n" +
                    $"💸 مبلغ: {item.Price:#,0} تومان\n" +
                    $"📊 نوع: {transactionTypeString}\n" +
                    $"📝 توضیح: {item.Description}\n" +
                    $"📌 وضعیت: {status}\n\n" +
                    $"🟰🟰🟰🟰🟰\n";
            }

            await botClient.SendTextMessageAsync(chatId, message, cancellationToken: cancellationToken);
        }

        await botClient.SendTextMessageAsync(chatId, "📋 لطفاً یکی از گزینه‌های زیر رو انتخاب کن:",
            replyMarkup: GetMainMenuKeyboard(), cancellationToken: cancellationToken);
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}