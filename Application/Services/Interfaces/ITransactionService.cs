using Domain.Entities.Transaction;

namespace Application.Services.Interfaces;

public interface ITransactionService
{
    Task<bool> CreateTransactionAsync(UserTransaction transaction);
    Task<List<UserTransaction>> GetTransactionsAsync(long chatId);
    Task DeleteTransactionAsync(string transactionId);
    Task<UserTransaction?> GetTransactionByIdAsync(string transactionId);
}