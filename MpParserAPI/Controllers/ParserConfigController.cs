using Microsoft.AspNetCore.Mvc;
using MpParserAPI.Common;
using MpParserAPI.Interfaces;
using MpParserAPI.Models.Dtos;
using MpParserAPI.Utils;

namespace MpParserAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ParserConfigController : ControllerBase
    {
        private readonly IParser _parser;
        private readonly ILogger<ParserConfigController> _logger;
        private readonly ISubscriptionManager _subscriptionManager;
        public ParserConfigController(IParser parser, 
            ILogger<ParserConfigController> logger, 
            ISubscriptionManager subscriptionManager)
        {
            _parser = parser;
            _logger = logger;
            _subscriptionManager = subscriptionManager;
        }
        [ParserAuthorize]
        [HttpPost("StartParsing")]
        public async Task<IActionResult> StartParsing()
        {
            var parserId = (Guid)HttpContext.Items["ParserId"];

             var result =  await _parser.StartParsing(parserId);
            if (result.Success)
            {
                _logger.LogInformation("������ ������� ������� ��� ParserId: {ParserId}", parserId);
                return Ok("������� ������� �������.");
            }
            else
            {
                return Forbid();
            }
        }

        [ParserAuthorize]
        [HttpPost("StopParsing")]
        public IActionResult StopParser()
        {
            var parserId = (Guid)HttpContext.Items["ParserId"];
            _parser.StopParsing(parserId);
            _logger.LogInformation("������ ������� ���������� ��� ParserId: {ParserId}", parserId);
            return Ok("������ ����������.");
        }
        [ParserAuthorize]
        [HttpPost("Logout")]
        public IActionResult Logout()
        {

            var parserId = (Guid)HttpContext.Items["ParserId"];
            _parser.StopParsing(parserId);
            _logger.LogInformation("������ ������� ���������� ��� ParserId: {ParserId}", parserId);

            Response.Cookies.Delete("ParserId");
            Response.Cookies.Delete("ParserPassword");

            _logger.LogInformation("������������ ����� �� ������� � ���� �������.");
            return Ok("������ ����������.");
        }

      //  [ParserAuthorize]
        [HttpGet("GetParserState")]
        public async Task<IActionResult> GetParserState()
        {
            var parserIdStr = HttpContext.Request.Cookies["ParserId"];
            if (!Guid.TryParse(parserIdStr, out var parserId))
                return Unauthorized("ParserId ����������� ��� �����������");

            _logger.LogInformation("GetParserState: ����� ��� parserId={ParserId}", parserId);

            var parserData = await _parser.GetParserState(parserId);
            _logger.LogInformation("GetParserState: _parser.GetParserState ���������, data={ParserData}", parserData);

            if (parserData == null)
            {
                _logger.LogInformation("GetParserState: parserData == null, ������ NotFound");
                return NotFound("������ �� ������");
            }

            _logger.LogInformation("GetParserState: �������� ����������, ������ Ok");
            return Ok(parserData);
        }


        [ParserAuthorize]
        [HttpPost("AddParserKeywords")]
        public async Task<IActionResult> AddParserKeywords([FromBody] List<string> keywords)
        {
            _logger.LogInformation("AddParserKeywords ������ � ��������� �������: {Keywords}", string.Join(", ", keywords));

            if (keywords == null)
            {
                _logger.LogWarning("AddParserKeywords: �������� ����� �� �������� (null)");
                return BadRequest("�������� ����� �� ��������.");
            }

            var parserId = (Guid)HttpContext.Items["ParserId"];

            var result = await _parser.SetKeywords(parserId, keywords);

            if (result.Success)
            {
                _logger.LogInformation("�������� ����� ������� ����������� ��� {ParserId}:", parserId);
                return Ok(result.Message);
            }
            else
            {
                _logger.LogWarning("�� ������� ���������� �������� ����� ��� ParserId: {ParserId}. Message: {Message}", parserId, result.Message);
                return BadRequest(result.Message);
            }
        }


        [ParserAuthorize]
        [HttpPost("AddGroupsToParser")]
        public async Task<IActionResult> AddGroupsToParser([FromBody] GroupNamesDto dto)
        {
            _logger.LogInformation("AddGroupsToParser ������ � ��������: {Groups}", dto?.GroupNames);

            if (!dto.GroupNames.Any())
            {
                _logger.LogInformation("AddGroupsToParser: ������ ������ ������ � ������� ������");
            }

            var parserId = (Guid)HttpContext.Items["ParserId"];

            try
            {
                await _parser.SetGroupsNames(parserId, dto.GroupNames);
                _logger.LogInformation("������ ������� ��������� ��� ParserId: {ParserId}", parserId);
                return Ok("������ ������� ��������� � ������.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "������ ��������� ��� ���������� ����� for ParserId: {ParserId}", parserId);
                return BadRequest($"�� ������� �������� ������. {ex.Message}");
            }
        }
        [ParserAuthorize]
        [HttpPost("GetParserRemainTime")]
        public async Task<IActionResult> GetParserRemainTime()
        {
            var parserId = (Guid)HttpContext.Items["ParserId"];
            var timeSpan = _subscriptionManager.GetRemainingParsingTime(parserId);

            var formattedTime = TimeFormatterHelper.ToHumanReadableStringThisSeconds(timeSpan);

            return Ok(new { remainingParsingTimeHoursMinutes = formattedTime });  
        }

        [ParserAuthorize] 
        [HttpPost("AddNewSpamMessage")]
        public async Task<IActionResult> AddNewSpamMessage(AddNewSpamMessageDto messageDto)
        {
            var parserId = (Guid)HttpContext.Items["ParserId"];
            var result = await _parser.AddNewSpamMessage(parserId, messageDto);
            return Ok(result);
        }

    }
}
