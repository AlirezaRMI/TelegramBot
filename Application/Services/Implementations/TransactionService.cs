using Microsoft.EntityFrameworkCore;
using Application.Services.Interfaces;
using Data.Context;
using Domain.Entities.Transaction;
using Domain.IRipository;
using Telegram.Bot;

namespace Application.Services.Implementations;

public class TransactionService(
    IBaseRepository<UserTransaction> transactionRepository,
    ITelegramBotClient botClient,
    BotContext context) : ITransactionService
{
    public async Task<bool> CreateTransactionAsync(UserTransaction transaction)
    {
        try
        {
            context.Transactions.Add(transaction);
            await context.SaveChangesAsync();
            return true;
        }
        catch (Exception e)
        {
            return false;
        }
    }

    public async Task<List<UserTransaction>> GetTransactionsAsync(long chatId)
    {
        return await context.Transactions
            .Where(t => t.ChatId == chatId)
            .OrderByDescending(t => t.CreateDate.Date)
            .ThenByDescending(t => t.CreateDate.TimeOfDay)
            .ToListAsync();
    }
}