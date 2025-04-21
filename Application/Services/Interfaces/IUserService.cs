using Domain.Entities;
using Telegram.Bot.Types;

namespace Application.Services.Interfaces;

public interface IUserService
{
    Task<User?> GetUserByUsernameAsync(string username);
    Task<bool> CreateUserAsync(User user);
    
    Task<int> GetUserBalanceAsync(long chatId);

  
}