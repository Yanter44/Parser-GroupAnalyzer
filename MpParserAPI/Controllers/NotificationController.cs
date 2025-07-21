using Microsoft.AspNetCore.Mvc;
using MpParserAPI.Interfaces;
using MpParserAPI.Models.Dtos;

namespace MpParserAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class NotificationController : ControllerBase
    {
        private readonly IParserDataStorage _parserDataStorage;

        public NotificationController(IParserDataStorage parserDataStorage)
        {
            _parserDataStorage = parserDataStorage;
        }
        [HttpPost("ValidateParserIdAndPassword")]
        public ActionResult<ValidateResponseDto> ValidateParserIdAndPassword([FromBody] ValidateRequestDto request)
        {
            if (!Guid.TryParse(request.ParserId, out var parserId))
                return BadRequest(new ValidateResponseDto { IsValid = false });

            if (_parserDataStorage.TryGetParser(parserId, out var parserData))
            {
                if (parserData.Password == request.Password)
                    return Ok(new ValidateResponseDto { IsValid = true });
            }

            return Ok(new ValidateResponseDto { IsValid = false });
        }

    }
}

