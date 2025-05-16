using Microsoft.AspNetCore.Mvc;
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
            if (result.Success)
                return Ok(result.Message);
            return BadRequest(result.Message);
        }
        [HttpPost("SendVerificationCodeFromTelegram")]
        public async Task<IActionResult> SendVerificationCodeFromTelegram([FromBody] VerificationCodeDto modelDto)
        {
            if (modelDto.TelegramCode == 0)
                return BadRequest("Код не должен быть пустым.");
            
            var result = await _parser.SubmitVerificationCodeFromTelegram(modelDto.ParserId, modelDto.TelegramCode);

            if (result.Success)
                return Ok(result.Message);

            return BadRequest(result.Message);
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
        public async Task<ActionResult<EnterToSessionByKeyResponceDto>> EnterToSessionByKeyAndPassword([FromBody] LoginToSessionDto logindto)
        {
            var result = await _parser.EnterToSessionByKeyAndPassword(logindto);
            return result.Success ? Ok(result.Data) : BadRequest(result.Message);
        }

    }
}
