using Microsoft.EntityFrameworkCore;
using Application.Services.Interfaces;
using Data.Context;
using Domain.Enum;
using Domain.IRipository;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace Application.Services.Implementations;

public class UserService(IBaseRepository<User> userRepository, ITelegramBotClient botClient,BotContext context) : IUserService
{
    public async Task<User?> GetUserByUsernameAsync(string username)
    {
        return await context.Users
            .FirstOrDefaultAsync(u => u.UserName == username);
    }

    public async Task<bool> CreateUserAsync(User user)
    {
        try
        {
            await userRepository.AddAsync(user);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<int> GetUserBalanceAsync(long chatId)
    {
        var transactions = await context.Transactions
            .Where(t => t.ChatId == chatId)
            .ToListAsync();
        
        var balance = transactions
            .Where(t => t.TransactionType == TransactionType.Increase)
            .Sum(t => t.Price) - transactions
            .Where(t => t.TransactionType == TransactionType.Decrease)
            .Sum(t => t.Price);

        return (int)balance;
    }    
}