using System.Text.Json;
using System.Text.RegularExpressions;
using TL;

namespace MpParserAPI.Middlewares
{
    public class TelegramExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<TelegramExceptionMiddleware> _logger;

        public TelegramExceptionMiddleware(RequestDelegate next, ILogger<TelegramExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (RpcException rpcEx)
            {
                await HandleTelegramExceptionAsync(context, rpcEx);
            }
        }

        private async Task HandleTelegramExceptionAsync(HttpContext context, RpcException rpcEx)
        {
            _logger.LogError(rpcEx,
                "Telegram RPC Exception: Code={Code}, Message={Message}, Request={Method} {Path}",
                rpcEx.Code, rpcEx.Message, context.Request.Method, context.Request.Path);

            context.Response.ContentType = "application/json";

            var (statusCode, errorMessage) = rpcEx switch
            {
                { Code: 420 } => (429, $"Слишком много запросов. Подождите {GetWaitTime(rpcEx.Message)}"),

                { Code: 406 } => (403, "IP адрес или канал заблокирован"),

                { Code: 401, Message: var msg } when msg.Contains("SESSION_PASSWORD") =>
                    (401, "Требуется пароль двухфакторной аутентификации"),

                { Code: 401, Message: var msg } when msg.Contains("AUTH_KEY") =>
                    (401, "Ключ аутентификации недействителен"),

                { Code: 401 } => (401, "Сессия отозвана. Требуется повторный вход"),

                { Code: 400, Message: var msg } when msg.Contains("PHONE_CODE") =>
                    (400, "Неверный код подтверждения"),

                { Code: 400, Message: var msg } when msg.Contains("PHONE_NUMBER") =>
                    (400, "Неверный номер телефона"),



                { Code: 400 } => (400, "Неверный запрос к Telegram API"),

                _ => (502, "Временная проблема с Telegram API. Попробуйте позже")
            };

            context.Response.StatusCode = statusCode;

            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = errorMessage,
                code = rpcEx.Code,
                requestId = context.TraceIdentifier
            }));
        }

        private string GetWaitTime(string message)
        {
            var match = Regex.Match(message, @"(\d+)");
            if (match.Success)
            {
                var seconds = int.Parse(match.Value);
                if (seconds < 60)
                    return $"{seconds} секунд";

                var minutes = seconds / 60;
                return $"{minutes} минут";
            }
            return "некоторое время";
        }
    }
}
