using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MpParserAPI.Interfaces;

namespace MpParserAPI.Common
{
    public class ParserAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var logger = context.HttpContext.RequestServices.GetService<ILogger<ParserAuthorizeAttribute>>();
            var request = context.HttpContext.Request;

            IParser parserService;
            try
            {
                parserService = context.HttpContext.RequestServices.GetRequiredService<IParser>();
            }
            catch (Exception ex)
            {
                context.Result = new StatusCodeResult(500);
                return;
            }

            var parserIdStr = request.Cookies["ParserId"];
            var password = request.Cookies["ParserPassword"];

            if (string.IsNullOrWhiteSpace(parserIdStr) || string.IsNullOrWhiteSpace(password))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            if (!Guid.TryParse(parserIdStr, out var parserId))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            bool isValid;
            try
            {
                isValid = await parserService.IsParserAuthValid(parserId, password);
            }
            catch (Exception ex)
            {
                context.Result = new StatusCodeResult(500);
                return;
            }

            if (!isValid)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            context.HttpContext.Items["ParserId"] = parserId;
            
        }
    }
}
