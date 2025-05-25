using Microsoft.EntityFrameworkCore;
using MpParserAPI.Controllers;
using MpParserAPI.DbContext;
using MpParserAPI.Interfaces;
using MpParserAPI.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();

builder.Services.AddDbContextFactory<ParserDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("ParserDb")));

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

builder.Services.AddScoped<IParser, ParserService>();
builder.Services.AddSingleton<ICloudinaryService, CloudinaryService>();
builder.Services.AddSingleton<IParserDataStorage, ParserDataStorage>();
builder.Services.AddTransient<IGenerator, Generator>();
builder.Services.AddSignalR();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
//builder.WebHost.UseKestrel(options =>
//{
//    options.Listen(System.Net.IPAddress.Any, 9090);
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
app.MapHub<ParserHub>("/parserHub");

app.UseCors("AllowAllOrigins");
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); 
    app.UseSwaggerUI(); 
}

app.MapControllers();
app.Run();
