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
    private InlineKeyboardMarkup GetBackToMenuKeyboard()
    {
        return new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("🔙 بازگشت به منو", "back_to_menu") }
        });
    }

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
            var messageText = update.Message.Text;
            var messageId = update.Message.MessageId;
            var currentState = stateService.GetState(chatId);

            if (currentState == "awaiting_price")
            {
                if (messageText != null)
                    await HandlePriceInput(botClient, chatId, messageText, messageId, cancellationToken);
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
                    Description = messageText,
                    CreateDate = DateTime.UtcNow,
                    TransactionType = transactionType == "Decrease"
                        ? TransactionType.Decrease
                        : TransactionType.Increase,
                    Status = TransactionStatus.Success
                };
                var descriptionPromptId = stateService.GetTempData<int>(chatId, "description_prompt_message_id");
                await botClient.DeleteMessageAsync(chatId, descriptionPromptId, cancellationToken);
                await transactionService.CreateTransactionAsync(transaction);
                stateService.ClearState(chatId);
                stateService.ClearTempData(chatId);

                await botClient.DeleteMessageAsync(chatId, messageId, cancellationToken);

                var balance = await userService.GetUserBalanceAsync(chatId);
                var keyboard = GetBackToMenuKeyboard();


                await botClient.SendTextMessageAsync(chatId,
                    $"\n✅ تراکنش با موفقیت ثبت شد!\n💰 موجودی شما: {balance:#,0} تومان",
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);

                return;
            }

            if (messageText != null && messageText.StartsWith("/start"))
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
                                     "چه کاری می‌تونم برات انجام بدم؟ 🤖";

                await botClient.SendTextMessageAsync(chatId, welcomeMessage, cancellationToken: cancellationToken);
                await botClient.SendTextMessageAsync(chatId, "لطفاً یکی از گزینه‌های زیر را انتخاب کن:",
                    replyMarkup: GetMainMenuKeyboard(), cancellationToken: cancellationToken);
            }
        }

        if (update.Type == UpdateType.CallbackQuery)
        {
            var callbackQuery = update.CallbackQuery;
            if (callbackQuery == null || callbackQuery.Message == null) return;

            var chatId = callbackQuery.Message.Chat.Id;
            var callbackData = callbackQuery.Data ?? string.Empty;

            switch (callbackData)
            {
                case "view_transactions":
                    var filterKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("📥 واریزها", "filter_increase"),
                            InlineKeyboardButton.WithCallbackData("📤 برداشت‌ها", "filter_decrease"), 
                            InlineKeyboardButton.WithCallbackData("حذف تراکنش", "delete_transaction")
                        },
                        new[] { InlineKeyboardButton.WithCallbackData("🔙 بازگشت به منو", "back_to_menu") },
                    });
                    await botClient.EditMessageTextAsync(chatId, callbackQuery.Message.MessageId,
                        "🔍 لطفاً نوع تراکنش‌هایی که می‌خوای ببینی رو انتخاب کن:", replyMarkup: filterKeyboard,
                        cancellationToken: cancellationToken);
                    break;

                case "add_transactions":
                    await AddTransaction(botClient, callbackQuery, cancellationToken);
                    break;
                case "view_total_increase":
                    await ShowTotalIncrease(botClient, callbackQuery, cancellationToken);
                    break;

                case "view_total_decrease":
                    await ShowTotalDecrease(botClient, callbackQuery, cancellationToken);
                    break;

                case "view_balance":
                    var balance = await userService.GetUserBalanceAsync(chatId);
                    var balanceMsg = $"💰 موجودی شما: {balance:#,0} تومان";
                    var balanceKeyboard = new InlineKeyboardMarkup(new[]
                    {
                        new[]
                        {
                            InlineKeyboardButton.WithCallbackData("💸 مجموع برداشت‌ها", "view_total_decrease"),
                            InlineKeyboardButton.WithCallbackData("💵 مجموع واریزی‌ها", "view_total_increase")
                        },

                        new[] { InlineKeyboardButton.WithCallbackData("🔙 بازگشت به منو", "back_to_menu") },
                    });
                    await EditOrSendMenuAsync(botClient, callbackQuery, balanceMsg, balanceKeyboard, cancellationToken);
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

    private async Task HandleTransactionType(ITelegramBotClient botClient, long chatId, string transactionType,
        int messageId, CancellationToken cancellationToken)
    {
        if (stateService.GetState(chatId) != "awaiting_transaction_type")
        {
            await botClient.EditMessageTextAsync(chatId, messageId,
                "❌ لطفاً ابتدا گزینه «افزودن تراکنش» را انتخاب کنید.",
                cancellationToken: cancellationToken);
            return;
        }

        await botClient.DeleteMessageAsync(chatId, messageId, cancellationToken);

        stateService.SetState(chatId, "awaiting_price");
        stateService.SetTempData(chatId, "transaction_type", transactionType);

        var typeText = transactionType == "Increase" ? "واریز" : "برداشت";
        var sentMessage = await botClient.SendTextMessageAsync(chatId,
            $"✅ نوع تراکنش: {typeText} \n💰 لطفاً مبلغ را به تومان وارد کنید:",
            cancellationToken: cancellationToken);

        stateService.SetTempData(chatId, "price_prompt_message_id", sentMessage.MessageId);
    }

    private async Task HandlePriceInput(ITelegramBotClient botClient, long chatId, string messageText,
        int userMessageId,
        CancellationToken cancellationToken)
    {
        var promptMessageId = stateService.GetTempData<int>(chatId, "price_prompt_message_id");
        await botClient.DeleteMessageAsync(chatId, promptMessageId, cancellationToken);
        await botClient.DeleteMessageAsync(chatId, userMessageId, cancellationToken);

        if (!int.TryParse(messageText, out var price))
        {
            var retryMessage = await botClient.SendTextMessageAsync(chatId,
                "❌ مقدار وارد شده معتبر نیست. لطفاً فقط عدد وارد کنید.",
                cancellationToken: cancellationToken);

            stateService.SetTempData(chatId, "price_prompt_message_id", retryMessage.MessageId);
            return;
        }

        stateService.SetTempData(chatId, "price", price);
        stateService.SetState(chatId, "awaiting_description");

        var descriptionPrompt = await botClient.SendTextMessageAsync(chatId,
            "📝 لطفاً توضیح تراکنش را وارد کنید.",
            cancellationToken: cancellationToken);
        stateService.SetTempData(chatId, "description_prompt_message_id", descriptionPrompt.MessageId);
    }

    private async Task AddTransaction(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        var transactionTypeKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("➕ واریز", "Increase"),
                InlineKeyboardButton.WithCallbackData("➖ برداشت", "Decrease"),
            },
            new[] { InlineKeyboardButton.WithCallbackData("🔙 بازگشت به منو", "back_to_menu") },
        });

        if (callbackQuery.Message != null)
        {
            await botClient.EditMessageTextAsync(callbackQuery.Message.Chat.Id, callbackQuery.Message.MessageId,
                "لطفاً نوع تراکنش را انتخاب کنید:", replyMarkup: transactionTypeKeyboard,
                cancellationToken: cancellationToken);

            stateService.SetState(callbackQuery.Message.Chat.Id, "awaiting_transaction_type");
        }
    }

    private async Task FilterTransactionsByType(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        TransactionType type, CancellationToken cancellationToken)
    {
        var keyboard = GetBackToMenuKeyboard();

        if (callbackQuery.Message != null)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var transactions = await transactionService.GetTransactionsAsync(chatId);
            var filtered = transactions.Where(t => t.TransactionType == type).ToList();

            string message = !filtered.Any()
                ? "❌ تراکنشی از این نوع پیدا نشد."
                : "📄 تراکنش‌های فیلتر شده:\n\n" +
                  string.Join("\n", filtered.Select(t =>
                  {
                      var iranTimeString = GetIranTimeString(t.CreateDate);
                      string status = t.Status == TransactionStatus.Success ? "موفق" : "ناموفق";
                      string transactionTypeString = t.TransactionType == TransactionType.Increase ? "واریز" : "برداشت";

                      return
                          $"📅 {iranTimeString.Split('\n')[0]}\n⏰ {iranTimeString.Split('\n')[1]}\n💸 " +
                          $"مبلغ: {t.Price:#,0}" +
                          $" تومان\n📊 نوع: {transactionTypeString}\n📝" +
                          $" توضیح: {t.Description}\n📌 " +
                          $"وضعیت: {status}\n🟰🟰🟰🟰🟰";
                  }));

            await EditOrSendMenuAsync(botClient, callbackQuery, message, keyboard, cancellationToken);
        }
    }

    private async Task EditOrSendMenuAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        string messageText, InlineKeyboardMarkup keyboard, CancellationToken cancellationToken)
    {
        try
        {
            if (callbackQuery.Message != null)
            {
                await botClient.EditMessageTextAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    messageId: callbackQuery.Message.MessageId,
                    text: messageText,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }
        }
        catch
        {
            if (callbackQuery.Message != null)
            {
                await botClient.SendTextMessageAsync(
                    chatId: callbackQuery.Message.Chat.Id,
                    text: messageText,
                    replyMarkup: keyboard,
                    cancellationToken: cancellationToken);
            }
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

    private async Task ShowTotalIncrease(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        if (callbackQuery.Message != null)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var transactions = await transactionService.GetTransactionsAsync(chatId);
            var totalIncrease = transactions
                .Where(t => t.TransactionType == TransactionType.Increase)
                .Sum(t => t.Price);

            var message = totalIncrease > 0
                ? $"💵 مجموع واریزی‌ها: {totalIncrease:#,0} تومان"
                : "❌ هیچ واریزی‌ای پیدا نشد.";

            var keyboard = GetBackToMenuKeyboard();

            await EditOrSendMenuAsync(botClient, callbackQuery, message, keyboard, cancellationToken);
        }
    }


    private async Task ShowTotalDecrease(ITelegramBotClient botClient, CallbackQuery callbackQuery,
        CancellationToken cancellationToken)
    {
        if (callbackQuery.Message != null)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var transactions = await transactionService.GetTransactionsAsync(chatId);
            var totalDecrease = transactions
                .Where(t => t.TransactionType == TransactionType.Decrease)
                .Sum(t => t.Price);

            var message = totalDecrease > 0
                ? $"💸 مجموع برداشت‌ها: {totalDecrease:#,0} تومان"
                : "❌ هیچ برداشتی‌ای پیدا نشد.";

            var keyboard = GetBackToMenuKeyboard();

            await EditOrSendMenuAsync(botClient, callbackQuery, message, keyboard, cancellationToken);
        }
    }
    private async Task HandleDeleteTransaction(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var transactions = await transactionService.GetTransactionsAsync(chatId);
    
        if (!transactions.Any())
        {
            await botClient.SendTextMessageAsync(chatId, "❌ هیچ تراکنشی برای حذف وجود ندارد.", cancellationToken: cancellationToken);
            return;
        }
        var transactionButtons = transactions.Select(t =>
            InlineKeyboardButton.WithCallbackData($"حذف تراکنش {t.Price:#,0} تومان", $"delete_{t.Id}")
        ).ToArray();

        var keyboard = new InlineKeyboardMarkup(transactionButtons);

        await botClient.SendTextMessageAsync(chatId, "لطفاً تراکنشی که می‌خواهید حذف کنید انتخاب کنید:", replyMarkup: keyboard, cancellationToken: cancellationToken);
    }
    private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message != null)
        {
            var chatId = callbackQuery.Message.Chat.Id;
            var callbackData = callbackQuery.Data ?? string.Empty;

            if (callbackData.StartsWith("delete_"))
            {
                var transactionId = callbackData.Substring(7); // Extract the transaction ID

                if (string.IsNullOrEmpty(transactionId))
                {
                    await botClient.EditMessageTextAsync(chatId, callbackQuery.Message.MessageId, 
                        "❌ ID تراکنش یافت نشد.", cancellationToken: cancellationToken);
                    return;
                }

                var transaction = await transactionService.GetTransactionByIdAsync(transactionId);
                if (transaction != null)
                {
                    await transactionService.DeleteTransactionAsync(transactionId);  // Delete the transaction
                    await botClient.EditMessageTextAsync(chatId, callbackQuery.Message.MessageId, 
                        $"✅ تراکنش {transaction.Price:#,0} تومان با موفقیت حذف شد.", cancellationToken: cancellationToken);
                }
                else
                {
                    await botClient.EditMessageTextAsync(chatId, callbackQuery.Message.MessageId, 
                        "❌ تراکنش مورد نظر پیدا نشد.", cancellationToken: cancellationToken);
                }
            }
        }

        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
    }



}
