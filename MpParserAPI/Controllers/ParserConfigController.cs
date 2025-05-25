using Microsoft.AspNetCore.Mvc;
using MpParserAPI.Common;
using MpParserAPI.Interfaces;
using MpParserAPI.Models.Dtos;

namespace MpParserAPI.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class ParserConfigController : ControllerBase
    {
        private readonly IParser _parser;
        private readonly ILogger<ParserConfigController> _logger;
        public ParserConfigController(IParser parser, ILogger<ParserConfigController> logger)
        {
            _parser = parser; 
            _logger = logger;
        }
        [ParserAuthorize]
        [HttpPost("StartParsing")]
        public IActionResult StartParsing()
        {
            var parserId = (Guid)HttpContext.Items["ParserId"];
            _parser.StartParsing(parserId);
            _logger.LogInformation("������ ������� ������� ��� ParserId: {ParserId}", parserId);
            return Ok("������� ������� �������.");
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

        [ParserAuthorize]
        [HttpGet("GetParserState")]
        public async Task<IActionResult> GetParserState()
        {
            var parserId = (Guid)HttpContext.Items["ParserId"];
            var parserData = await _parser.GetParserState(parserId); 

            if (parserData == null)
                return NotFound("������ �� ������");

            return Ok(parserData);
        }

        [ParserAuthorize]
        [HttpPost("AddParserKeywords")]
        public async Task<IActionResult> AddParserKeywords([FromBody] string keywords)
        {
            _logger.LogInformation("AddParserKeywords ������ � ��������� �������: {Keywords}", keywords);

            if (string.IsNullOrWhiteSpace(keywords))
            {
                _logger.LogWarning("AddParserKeywords: �������� ����� ����� ");
                return BadRequest("�� �� ����� �������� �����.");
            }

            var parserId = (Guid)HttpContext.Items["ParserId"];

            var result = await _parser.SetKeywordsFromText(parserId, keywords);

            if (result.Success)
            {
                _logger.LogInformation("�������� ����� ������� ����������� ��� ParserId: {ParserId}", parserId);
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

            if (dto?.GroupNames == null || !dto.GroupNames.Any())
            {
                _logger.LogWarning("AddGroupsToParser: ������ ����� ������");
                return BadRequest("������ ����� ������. ���������� �������� �������� �����");
            }

            var parserId = (Guid)HttpContext.Items["ParserId"];

            try
            {
                await _parser.SetGroupsNamesForParser(parserId, dto.GroupNames);
                _logger.LogInformation("������ ������� ��������� ��� ParserId: {ParserId}", parserId);
                return Ok("������ ������� ��������� � ������.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "������ ��������� ��� ���������� ����� for ParserId: {ParserId}", parserId);
                return BadRequest($"�� ������� �������� ������. {ex.Message}");
            }
        }


    }
}
