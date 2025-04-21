using Telegram.Bot;
using Telegram.Bot.Types;
using System.Threading;
using System.Threading.Tasks;
using Application.Services.Interfaces;
using Domain.Entities.Transaction;

namespace Console;

public class AddTransaction(ITelegramBotClient botClient, IStateService StateService)
{
    public async Task HandleAsync(CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var message = "➕ لطفاً اطلاعات تراکنش رو ارسال کن.\n\nمثال: `150000 - خرید لباس`";
        await botClient.SendTextMessageAsync(
            callbackQuery.Message.Chat.Id,
            message,
            cancellationToken: cancellationToken
        );

        StateService.SetState(callbackQuery.Message.Chat.Id, "awaiting_transaction");

        await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, cancellationToken: cancellationToken);
    }
}