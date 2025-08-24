using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using MpParserAPI.Controllers;
using MpParserAPI.DbContext;
using MpParserAPI.HostedServices;
using MpParserAPI.Interfaces;
using MpParserAPI.Middlewares;
using MpParserAPI.Services;
using MpParserAPI.Services.Admin;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

builder.Services.AddDbContextFactory<ParserDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ParserDb")));

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();


builder.Host.UseSerilog();

builder.Services.AddScoped<IParser, ParserService>();
builder.Services.AddScoped<INotify, NotifyService>();
builder.Services.AddScoped<IParserAuthentificate, ParserAuthoriseService>();
builder.Services.AddSingleton<ICloudinaryService, CloudinaryService>();
builder.Services.AddSingleton<IParserDataStorage, ParserDataStorage>();
builder.Services.AddScoped<IRedis, RedisService>();
builder.Services.AddTransient<IGenerator, Generator>();
builder.Services.AddScoped<IAdmin, AdminService>();
builder.Services.AddScoped<ISpaceProxy, SpaceProxyService>();
builder.Services.AddScoped<ISubscriptionManager, SubscriptionManager>();
builder.Services.AddSignalR();
builder.Services.AddHttpClient();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<ParsersHostedService>();

builder.WebHost.UseKestrel(options =>
{
    options.Listen(System.Net.IPAddress.Any, 9090);
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .WithOrigins("https://resortlehi.ru")
            .AllowCredentials()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowAllOrigins", policy =>
//    {
//        policy.WithOrigins("http://localhost:8000")
//          .AllowCredentials()
//          .AllowAnyHeader()
//          .AllowAnyMethod();
//    });
//});

var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(key)
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(token))
                {
                    context.Token = token;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
var app = builder.Build();
app.UseMiddleware<TelegramExceptionMiddleware>();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ParserDbContext>();
    db.Database.Migrate();

    var parserService = scope.ServiceProvider.GetRequiredService<IParserAuthentificate>();
    var result =  await parserService.LoadAllParsersFromDbAsync();
    if (!result)
        return;
   

}

app.MapHub<ParserHub>("/parserHub");
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
//app.UseCors("AllowFrontend"); 
app.UseCors("AllowAllOrigins");
app.MapControllers(); 

app.Run();
