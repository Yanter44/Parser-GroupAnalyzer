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
        public AuthController(IParser parser)
        {
            _parser = parser;
        }
        [HttpPost("LoginAndStartParser")]
        public async Task<IActionResult> LoginAndStartParser([FromBody] AuthentificateDto logindto)
        {
            var result = await _parser.LoginAndStartParser(logindto);

            if (result.Success && result.Data is not null)
            {
                Response.Cookies.Append("ParserId", result.Data.ParserId.ToString(),
                    new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Expires = DateTimeOffset.UtcNow.AddDays(7) });

                Response.Cookies.Append("ParserPassword", result.Data.Password,
                    new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Strict, Expires = DateTimeOffset.UtcNow.AddDays(7) });
            }

            return result.Success ? Ok(result) : BadRequest(result);
        }


        [HttpPost("SendVerificationCodeFromTelegram")]
        public async Task<IActionResult> SendVerificationCodeFromTelegram([FromBody] VerificationCodeDto modelDto)
        {
            if (modelDto.TelegramCode == 0)
            {
                return Ok(OperationResult<ParserAuthResultDto>.Fail("Код не должен быть пустым."));
            }

            var result = await _parser.SubmitVerificationCodeFromTelegram(modelDto.ParserId, modelDto.TelegramCode);

            return result.Success ? Ok(result) : BadRequest(result);
        }


        [HttpPost("SendATwoFactorPassword")]
        public async Task<IActionResult> SendATwoFactorPassword([FromBody] TwoFactorPasswordDto modelDto)
        {
            if (modelDto.TwoFactorPassword == 0)
                return BadRequest("Код не должен быть пустым.");

            var result = await _parser.SubmitTwoFactorPassword(modelDto.ParserId, modelDto.TwoFactorPassword);

            if (result.Success)
                return Ok(result.Message);

            return BadRequest(result.Message);
        }

        [HttpPost("EnterToSessionByKeyAndPassword")]
        public async Task<ActionResult<EnterToSessionByKeyResponceDto>> EnterToSessionByKeyAndPassword()
        {
            if (!Request.Cookies.TryGetValue("ParserId", out var parserIdStr) ||
                !Request.Cookies.TryGetValue("ParserPassword", out var parserPassword) ||
                !Guid.TryParse(parserIdStr, out var parserId))
            {
                return BadRequest("Отсутствует сессионная информация (ParserId или Password).");
            }

            var result = await _parser.EnterToSessionByKeyAndPassword(parserId, parserPassword);

            return result.Success ? Ok(result.Data) : BadRequest(result.Message);
        }
    }
}
