using Microsoft.AspNetCore.Mvc;
using MpParserAPI.Common;
using MpParserAPI.Interfaces;
using MpParserAPI.Models.Dtos;
using TL;

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

                Response.Cookies.Append("TempAuthId", tempAuthId,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTimeOffset.UtcNow.AddMinutes(10)
                    });
            }
            return result.Success ? Ok(result) : BadRequest(result);
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

                Response.Cookies.Append("ParserId", result.Data.ParserId.ToString(), new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(7)
                });

                if (!string.IsNullOrEmpty(result.Data.Password))
                {
                    Response.Cookies.Append("ParserPassword", result.Data.Password, new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTimeOffset.UtcNow.AddDays(7)
                    });
                }
            }

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

                Response.Cookies.Append("ParserId", result.Data.ParserId.ToString(), new CookieOptions
                {
                    HttpOnly = true,
                    SameSite = SameSiteMode.Strict,
                    Expires = DateTimeOffset.UtcNow.AddDays(7)
                });

                if (!string.IsNullOrEmpty(result.Data.Password))
                {
                    Response.Cookies.Append("ParserPassword", result.Data.Password, new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Strict,
                        Expires = DateTimeOffset.UtcNow.AddDays(7)
                    });
                }

                return Ok(result);
            }

            return BadRequest(new { success = false, message = result.Message });
        }




        [HttpPost("EnterToSessionByKeyAndPassword")]
        public async Task<IActionResult> EnterToSessionByKeyAndPassword([FromBody] EnterToParserSessionByKeyAndPasswordDto? model)
        {
            var result = await _parserAuthentificate.EnterToSessionByKeyAndPassword(model.ParserId, model.ParserPassword);

            if (!result.Success)
                return BadRequest(result.Message);

            Response.Cookies.Append("ParserId", result.Data.ParserId.ToString(),
                 new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Expires = DateTimeOffset.UtcNow.AddDays(7) });

            Response.Cookies.Append("ParserPassword", result.Data.Password,
                new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Expires = DateTimeOffset.UtcNow.AddDays(7) });

            return Ok(result.Message);
        }

    }
}
