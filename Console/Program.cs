using Data.Context;
using Ioc;
using Telegram.Bot;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton<ITelegramBotClient>(
    new TelegramBotClient("7659473718:AAG4FBXc6ks1bu4qR4DYo2vCTw_YWfjhsi0")
);
builder.Services.AddHostedService<TelegramBotService>();

#region sql config

builder.Services.AddDbContext<BotContext>
(options => options.UseSqlServer
(builder.Configuration.GetConnectionString
    ("BotConnectionString")));

#endregion

builder.Services.AddServices();
var app = builder.Build();

app.MapGet("/", () => "Hello World!");
app.Run();