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

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        if (update.Type == UpdateType.Message && update.Message?.Type == MessageType.Text)
        {
            #region AddTracsaction

            var chatId = update.Message.Chat.Id;
            var messageTexts = update.Message.Text;
            if (update.Type == UpdateType.Message && update.Message?.Type == MessageType.Text)
            {
                var messageText = update.Message.Text;


                if (stateService.GetState(chatId) == "Increase" || stateService.GetState(chatId) == "Decrease")
                {
                    var parts = messageText.Split("-", 2, StringSplitOptions.TrimEntries);

                    if (parts.Length != 2 || !int.TryParse(parts[0], out var amount))
                    {
                        await botClient.SendTextMessageAsync(chatId,
                            "❌ لطفاً فرمت صحیح وارد کنید. مثال: `70000 - توضیحات`",
                            cancellationToken: cancellationToken);
                        return;
                    }

                    string description = parts[1];
                    string transactionType = stateService.GetState(chatId);


                    var transaction = new UserTransaction
                    {
                        ChatId = chatId,
                        Price = amount,
                        Description = description,
                        CreateDate = DateTime.UtcNow,
                        TransactionType = transactionType == "Decrease"
                            ? TransactionType.Decrease
                            : TransactionType.Increase,
                        Status = TransactionStatus.Success
                    };

                    await transactionService.CreateTransactionAsync(transaction);
                    stateService.ClearState(chatId);

                    await botClient.SendTextMessageAsync(chatId, "✅ تراکنش با موفقیت ذخیره شد!",
                        cancellationToken: cancellationToken);
                }
            }

            #endregion

            if (messageTexts.StartsWith("/start"))
            {
                #region Add User

                var username = update.Message.From?.Username;
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

                #endregion

                #region Time set

                var iranTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
                var iranTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, iranTimeZone);
                var pc = new PersianCalendar();
                var date = $"{pc.GetYear(iranTime):0000}/{pc.GetMonth(iranTime):00}/{pc.GetDayOfMonth(iranTime):00}";
                var time = $"{pc.GetHour(iranTime):00}:{pc.GetMinute(iranTime):00}";

                #endregion

                var firstName = update.Message?.From?.FirstName;
                var welcomeMessage = $"سلام {firstName} عزیز! به ربات خوش اومدی 🎉\n" +
                                     $"🗓 تاریخ ورود: {date}\n" +
                                     $"⏰ ساعت ورود: {time}\n" +
                                     $"چه کاری می‌تونم برات انجام بدم نانا؟ 🤖";

                await botClient.SendTextMessageAsync(
                    chatId,
                    welcomeMessage,
                    cancellationToken: cancellationToken
                );

                #region KeyBord

                var inlineKeyboard = new InlineKeyboardMarkup(
                    new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("مشاهده تراکنشات",
                                "view_transactions")
                        },
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("افزودن تراکنش",
                                "add_transactions")
                        },
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("مشاهده موجودی",
                                "view_balance")
                        }
                    });

                #endregion

                await botClient.SendTextMessageAsync(
                    chatId,
                    "لطفاً یکی از گزینه‌های زیر را انتخاب کن:",
                    replyMarkup: inlineKeyboard,
                    cancellationToken: cancellationToken
                );
            }
        }

        if (update.Type == UpdateType.CallbackQuery)
        {
            var chatId = update.CallbackQuery?.Message?.Chat?.Id ?? 0;
            var callbackQuery = update.CallbackQuery;
            var callbackdata = callbackQuery.Data;
            switch (callbackdata)
            {
                case "view_transactions":
                    var transactions = await transactionService.GetTransactionsAsync(callbackQuery.Message.Chat.Id);

                    if (transactions.Any())
                    {
                        var transactionListMessage = "📒 لیست تراکنش‌ها:\n\n";
                        var iranTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
                        foreach (var item in transactions)
                        {
                            string status = item.Status == TransactionStatus.Success ? "موفق" : "ناموفق";
                            string transactionTypeString =
                                item.TransactionType == TransactionType.Increase ? "واریز" : "برداشت";
                            var iranDateTime = TimeZoneInfo.ConvertTimeFromUtc(item.CreateDate, iranTimeZone);
                            var pc = new PersianCalendar();
                            var date =
                                $"{pc.GetYear(iranDateTime):0000}/{pc.GetMonth(iranDateTime):00}/{pc.GetDayOfMonth(iranDateTime):00}";
                            var time = $"{pc.GetHour(iranDateTime):00}:{pc.GetMinute(iranDateTime):00}";

                            transactionListMessage +=
                                $"📅 تاریخ: {date}\n" +
                                $"⏰ ساعت: {time}\n" +
                                $"💸 مبلغ: {item.Price.ToString("#,0")} تومان\n" +
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
                        await botClient.SendTextMessageAsync(callbackQuery.Message.Chat.Id,
                            "❌ شما هیچ تراکنشی ثبت نکرده‌اید.", cancellationToken: cancellationToken);
                    }

                    var inlineKeyboard = new InlineKeyboardMarkup(
                        new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("مشاهده تراکنشات", "view_transactions") },
                            new[] { InlineKeyboardButton.WithCallbackData("افزودن تراکنش", "add_transactions") },
                            new[] { InlineKeyboardButton.WithCallbackData("مشاهده موجودی", "view_balance") }
                        });

                    await botClient.SendTextMessageAsync(
                        callbackQuery.Message.Chat.Id,
                        "📋 لطفاً یکی از گزینه‌های زیر رو انتخاب کن:",
                        replyMarkup: inlineKeyboard,
                        cancellationToken: cancellationToken
                    );

                    break;

                case "add_transactions":
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

                    await botClient.SendTextMessageAsync(
                        chatId,
                        "لطفاً نوع تراکنش را انتخاب کنید:",
                        replyMarkup: transactionTypeKeyboard,
                        cancellationToken: cancellationToken
                    );

                    stateService.SetState(chatId, "awaiting_transaction_type");
                    break;
                }

                case "view_balance":
                    var balance = await userService.GetUserBalanceAsync(callbackQuery.Message.Chat.Id);
                    await botClient.SendTextMessageAsync(
                        callbackQuery.Message.Chat.Id,
                        $"💰 موجودی شما: {balance.ToString("#,0")} تومان",
                        cancellationToken: cancellationToken
                    );
                    var inlineKeyboardBalance = new InlineKeyboardMarkup(
                        new[]
                        {
                            new[] { InlineKeyboardButton.WithCallbackData("مشاهده تراکنشات", "view_transactions") },
                            new[] { InlineKeyboardButton.WithCallbackData("افزودن تراکنش", "add_transactions") },
                            new[] { InlineKeyboardButton.WithCallbackData("مشاهده موجودی", "view_balance") }
                        });

                    await botClient.SendTextMessageAsync(
                        callbackQuery.Message.Chat.Id,
                        "📋 لطفاً یکی از گزینه‌های زیر رو انتخاب کن:",
                        replyMarkup: inlineKeyboardBalance,
                        cancellationToken: cancellationToken
                    );

                    break;
                case "Increase":
                case "Decrease":
                {
                    if (stateService.GetState(chatId) == "awaiting_transaction_type")
                    {
                        stateService.SetState(chatId, callbackdata);

                        await botClient.SendTextMessageAsync(
                            chatId,
                            "لطفاً مبلغ و توضیح تراکنش را وارد کنید.\n\n📌 مثال: `70000 - حقوق ماهانه`",
                            cancellationToken: cancellationToken
                        );
                    }
                    else
                    {
                        await botClient.SendTextMessageAsync(
                            chatId,
                            "❌ لطفاً ابتدا گزینه «افزودن تراکنش» را انتخاب کنید.",
                            cancellationToken: cancellationToken
                        );
                    }


                    break;
                }

                default:
                    var unknownMessage = "❓ دکمه ناشناخته.";
                    await botClient.SendTextMessageAsync(
                        callbackQuery.Message.Chat.Id,
                        unknownMessage,
                        cancellationToken: cancellationToken
                    );
                    break;
            }

            await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
        }
    }


    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "❌ خطا هنگام اجرای بات");
        return Task.CompletedTask;
    }
}