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
            var result = await _parserAuthentificate.LoginAndStartParser(logindto);

            if (result.Data != null)
            {
                Response.Cookies.Append("ParserId", result.Data.ParserId.ToString(),
                    new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Expires = DateTimeOffset.UtcNow.AddDays(7) });

                if (result.Data.Password != null)
                {
                    Response.Cookies.Append("ParserPassword", result.Data.Password,
                        new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Expires = DateTimeOffset.UtcNow.AddDays(7) });
                }
            }
            return result.Success ? Ok(result) : BadRequest(result);
        }

        [HttpPost("SendVerificationCodeFromTelegram")]
        public async Task<IActionResult> SendVerificationCodeFromTelegram([FromBody] VerificationCodeDto modelDto)
        {
            if (modelDto.TelegramCode == 0)
                return BadRequest("Код не должен быть пустым.");

            if (!Request.Cookies.TryGetValue("ParserId", out var parserIdStr) || !Guid.TryParse(parserIdStr, out var parserId))
            {
                return BadRequest("ParserId cookie отсутствует или некорректна.");
            }

            var result = await _parserAuthentificate.SubmitVerificationCodeFromTelegram(parserId, modelDto.TelegramCode);

            if (result.Success && result.Data?.Password != null)
            {
                Response.Cookies.Append("ParserPassword", result.Data.Password,
                    new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Expires = DateTimeOffset.UtcNow.AddDays(7) });
            }

            return result.Success ? Ok(result) : BadRequest(result);
        }


        [HttpPost("SendATwoFactorPassword")]
        public async Task<IActionResult> SendATwoFactorPassword([FromBody] TwoFactorPasswordDto modelDto)
        {
            if (!Request.Cookies.TryGetValue("ParserId", out var parserIdString) || !Guid.TryParse(parserIdString, out var parserId))
            {
                return BadRequest("ParserId отсутствует или некорректен.");
            }

            if (string.IsNullOrEmpty(modelDto.TwoFactorPassword))
                return BadRequest("Код не должен быть пустым.");

            var result = await _parserAuthentificate.SubmitTwoFactorPassword(parserId, modelDto.TwoFactorPassword);

            if (result.Success && result.Data?.Password != null)
            {
                Response.Cookies.Append("ParserPassword", result.Data.Password,
                    new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Expires = DateTimeOffset.UtcNow.AddDays(7) });

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
