using MpBossParserNotification.HostedServices;
using MpBossParserNotification.Interfaces;
using MpBossParserNotification.Models;
using MpBossParserNotification.Services;
using Telegram.Bot;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddSingleton<ParserSubscriptionStorage>();
        builder.Services.AddSingleton<IBotService, BotService>();
        builder.Services.AddSingleton<IStorage, ParserSubscriptionStorage>();
        builder.Services.AddSingleton(_ => new TelegramBotClient("7574107892:AAH7YFmXlySt15SrOfgLjHXOnXj1Ts-J7OM"));
        builder.Services.AddHostedService<BotHostedService>();
        builder.Services.AddHttpClient();

        builder.WebHost.UseKestrel(options =>
        {
            options.Listen(System.Net.IPAddress.Any, 5000);
        });
        var app = builder.Build();

        app.MapPost("/Notify", async (NotifyRequest req, IBotService botService) =>
        {
            await botService.NotifyAsync(req.ParserId, req.Message, req.MessageLink);
            return Results.Ok("✅ Уведомления отправлены");
        });

        app.Run();
    }
}