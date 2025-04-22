using Console;
using Data.Context;
using Ioc;
using Telegram.Bot;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ITelegramBotClient>(provider =>
    new TelegramBotClient("7659473718:AAFf5LmCaGUj0vFxdz7hshq0P5Hucq81Y8Q"));

#region sql config

builder.Services.AddDbContext<BotContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("BotConnectionString")));

#endregion
builder.Services.AddHostedService<TelegramBotService>();
builder.Services.AddServices();
builder.Services.AddSingleton<Dictionary<long, string>>();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();
app.Run();