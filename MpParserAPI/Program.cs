using Microsoft.EntityFrameworkCore;
using MpParserAPI.Controllers;
using MpParserAPI.DbContext;
using MpParserAPI.HostedServices;
using MpParserAPI.Interfaces;
using MpParserAPI.Services;
using MpParserAPI.Services.Admin;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

builder.Services.AddDbContextFactory<ParserDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ParserDb")));

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddScoped<IParser, ParserService>();
builder.Services.AddScoped<INotify, NotifyService>();
builder.Services.AddScoped<IParserAuthentificate, ParserAuthoriseService>();
builder.Services.AddSingleton<ICloudinaryService, CloudinaryService>();
builder.Services.AddSingleton<IParserDataStorage, ParserDataStorage>();
builder.Services.AddScoped<IRedis, RedisService>();
builder.Services.AddTransient<IGenerator, Generator>();
builder.Services.AddScoped<IAdmin, AdminService>();
builder.Services.AddScoped<ISpaceProxy, SpaceProxyService>();
builder.Services.AddSignalR();
builder.Services.AddHttpClient();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<ParsersHostedService>();

//builder.WebHost.UseKestrel(options =>
//{
//    options.Listen(System.Net.IPAddress.Any, 9090);
//});

//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowFrontend", policy =>
//    {
//        policy
//            .WithOrigins("https://resortlehi.ru")
//            .AllowCredentials()
//            .AllowAnyHeader()
//            .AllowAnyMethod();
//    });
//});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", policy =>
    {
        policy.WithOrigins("http://localhost:8000")
          .AllowCredentials()
          .AllowAnyHeader()
          .AllowAnyMethod();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ParserDbContext>();
    var parserService = scope.ServiceProvider.GetRequiredService<IParserAuthentificate>();
    var result =  await parserService.LoadAllParsersFromDbAsync();
    if (!result)
        return;
   
    db.Database.Migrate();
}
app.MapHub<ParserHub>("/parserHub");
app.UseRouting();
//pp.UseCors("AllowFrontend"); 
app.UseCors("AllowAllOrigins");
app.MapControllers(); 

app.Run();
