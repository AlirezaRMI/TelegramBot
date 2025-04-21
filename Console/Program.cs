using Console;
using Data.Context;
using Ioc;
using Telegram.Bot;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ITelegramBotClient>(provider =>
    new TelegramBotClient("7659473718:AAG4FBXc6ks1bu4qR4DYo2vCTw_YWfjhsi0"));

#region sql config

builder.Services.AddDbContext<BotContext>(options => options.UseSqlServer(builder.Configuration.GetConnectionString("BotConnectionString")));

#endregion
builder.Services.AddHostedService<TelegramBotService>();
builder.Services.AddTransient<AddTransaction>();
builder.Services.AddServices();
builder.Services.AddSingleton<Dictionary<long, string>>();
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

var app = builder.Build();
app.Run();