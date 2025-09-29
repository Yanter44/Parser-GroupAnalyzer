using Microsoft.AspNetCore.Mvc;
using MpParserAPI.Common;
using MpParserAPI.Interfaces;
using MpParserAPI.Models.Dtos;

namespace MpParserAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IParser _parser;
        private readonly IParserAuthentificate _parserAuthentificate;
        public AuthController(IParser parser, IParserAuthentificate parserAuthentificate)
        {
            _parser = parser;
            _parserAuthentificate = parserAuthentificate;
        }

        [HttpPost("LoginAndStartParser")]
        public async Task<IActionResult> LoginAndStartParser([FromBody] AuthentificateDto logindto)
        {
            if (Request.Cookies.ContainsKey("TempAuthId"))
                Response.Cookies.Delete("TempAuthId");

            var result = await _parserAuthentificate.RequestLoginAsync(logindto.phone);

            if (result.Success && result.Data != Guid.Empty)
            {
                var tempAuthId = result.Data.ToString();
                Response.Cookies.Append("TempAuthId", tempAuthId, GetCookieOptions(TimeSpan.FromMinutes(10)));
            }

            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("ResendVerificationCode")]
        public async Task<IActionResult> ResendVerificationCode()
        {
            if (!Request.Cookies.TryGetValue("TempAuthId", out var tempAuthIdStr) ||
                !Guid.TryParse(tempAuthIdStr, out var tempAuthId))
                return BadRequest(new { success = false, message = "Сессия устарела" });

            var result = await _parserAuthentificate.ResendVerificationCode(tempAuthId);

            return result.Success
                ? Ok(new { success = true, message = result.Message })
                : BadRequest(new { success = false, message = result.Message });
        }

        [HttpPost("SendVerificationCodeFromTelegram")]
        public async Task<IActionResult> SendVerificationCodeFromTelegram([FromBody] VerificationCodeDto modelDto)
        {
            if (modelDto.TelegramCode == 0)
                return BadRequest("Код не должен быть пустым.");

            if (!Request.Cookies.TryGetValue("TempAuthId", out var tempAuthIdStr) || !Guid.TryParse(tempAuthIdStr, out var tempAuthId))
                return BadRequest("TempAuthId cookie отсутствует или некорректен.");

            var result = await _parserAuthentificate.SubmitVerificationCodeFromTelegram(tempAuthId, modelDto.TelegramCode);

            if (result.Success && result.Data != null)
            {
                Response.Cookies.Delete("TempAuthId");

                Response.Cookies.Append("ParserId", result.Data.ParserId.ToString(), GetCookieOptions(TimeSpan.FromDays(7)));

                if (!string.IsNullOrEmpty(result.Data.Password))
                {
                    Response.Cookies.Append("ParserPassword", result.Data.Password, GetCookieOptions(TimeSpan.FromDays(7)));
                }
            }

            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("VerifyTelegramCodeForParser")]
        public async Task<IActionResult> VerifyTelegramCodeForParser([FromBody] VerificationCodeDto modelDto)
        {
            if (modelDto.TelegramCode == 0)
                return BadRequest("Код не должен быть пустым.");

            if (!Request.Cookies.TryGetValue("ParserId", out var parserIdStr)
                || !Guid.TryParse(parserIdStr, out var parserId))
            {
                return BadRequest("ParserId отсутствует или некорректен.");
            }

            var result = await _parserAuthentificate.VerifyTelegramCodeForParser(parserId, modelDto.TelegramCode);

            return result.Success ? Ok(result) : BadRequest(result);
        }



        [HttpPost("SendATwoFactorPassword")]
        public async Task<IActionResult> SendATwoFactorPassword([FromBody] TwoFactorPasswordDto modelDto)
        {
            if (string.IsNullOrEmpty(modelDto.TwoFactorPassword))
                return BadRequest("Пароль не должен быть пустым.");

            if (!Request.Cookies.TryGetValue("TempAuthId", out var tempAuthIdStr) || !Guid.TryParse(tempAuthIdStr, out var tempAuthId))
                return BadRequest("TempAuthId отсутствует или некорректен.");

            var result = await _parserAuthentificate.SubmitTwoFactorPassword(tempAuthId, modelDto.TwoFactorPassword);

            if (result.Success && result.Data != null)
            {
                Response.Cookies.Delete("TempAuthId");

                Response.Cookies.Append("ParserId", result.Data.ParserId.ToString(), GetCookieOptions(TimeSpan.FromDays(7)));

                if (!string.IsNullOrEmpty(result.Data.Password))
                {
                    Response.Cookies.Append("ParserPassword", result.Data.Password, GetCookieOptions(TimeSpan.FromDays(7)));
                }

                return Ok(result);
            }

            return BadRequest(new { success = false, message = result.Message });
        }

        [ParserAuthorize]
        [HttpPost("TryEnterToSessionByCookie")]
        public async Task<IActionResult> TryEnterToSessionByCookie()
        {
            var parserId = HttpContext.Items["ParserId"] as Guid?;
            if (parserId == null)
                return Unauthorized();

            return Ok(new { success = true });
        }

        [HttpPost("EnterToSessionByKeyAndPassword")]
        public async Task<IActionResult> EnterToSessionByKeyAndPassword([FromBody] EnterToParserSessionByKeyAndPasswordDto? model)
        {
            var result = await _parserAuthentificate.EnterToSessionByKeyAndPassword(model.ParserId, model.ParserPassword);

            if (!result.Success)
                return BadRequest(result.Message);

            Response.Cookies.Append("ParserId", result.Data.ParserId.ToString(), GetCookieOptions(TimeSpan.FromDays(7)));
            Response.Cookies.Append("ParserPassword", result.Data.Password, GetCookieOptions(TimeSpan.FromDays(7)));

            return Ok(result.Message);
        }
 
        private CookieOptions GetCookieOptions(TimeSpan expiration)
        {
            bool isDevelopment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";

            return new CookieOptions
            {
                HttpOnly = true,
                Secure = !isDevelopment,
                SameSite = isDevelopment ? SameSiteMode.Lax : SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.Add(expiration)
            };
        }
    }
}

