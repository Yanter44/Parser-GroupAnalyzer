using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MpParserAPI.Interfaces;

namespace MpParserAPI.Common
{
    public class ParserAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var request = context.HttpContext.Request;
            var response = context.HttpContext.Response;

            var parserService = context.HttpContext.RequestServices.GetService(typeof(IParser)) as IParser;
            if (parserService == null)
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

            var isValid = await parserService.IsParserAuthValid(parserId, password);
            if (!isValid)
            {
                context.Result = new UnauthorizedResult();
                return;
            }
            context.HttpContext.Items["ParserId"] = parserId;
        }
    }
}
