using Application.Services.Implementations;
using Application.Services.Interfaces;
using Data.Repository;
using Domain.IRipository;
using Microsoft.Extensions.DependencyInjection;
using Telegram.Bot;
namespace Ioc;


public static class DependencyContainer
{
    public static IServiceCollection AddServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(IBaseRepository<>), typeof(BaseRipository<>));
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IStateService, StateService>();
        return services;
    }
 
}