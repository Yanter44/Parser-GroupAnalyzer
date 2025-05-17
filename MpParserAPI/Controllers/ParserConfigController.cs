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
        public ParserConfigController(IParser parser)
        {
            _parser = parser;
        }
        [ParserAuthorize]
        [HttpPost("StopParser")]
        public IActionResult StopParser()
        {
            var parserId = (Guid)HttpContext.Items["ParserId"];
            _parser.StopParser(parserId);
            return Ok("������ ����������.");
        }
        [ParserAuthorize]
        [HttpPost("AddParserKeywords")]
        public async Task<IActionResult> AddParserKeywords([FromBody] string keywords)
        {
            if (string.IsNullOrWhiteSpace(keywords))
                return BadRequest("�� �� ����� �������� �����.");

            var parserId = (Guid)HttpContext.Items["ParserId"];
            var result = await _parser.SetKeywordsFromText(parserId, keywords);
            return result.Success ? Ok(result.Message) : BadRequest(result.Message);
        }

        [ParserAuthorize]
        [HttpPost("AddGroupsToParser")]
        public async Task<IActionResult> AddGroupsToParser([FromBody] GroupNamesDto dto)
        {
            if (dto?.GroupNames == null || !dto.GroupNames.Any())
                return BadRequest("������ ����� ������. ���������� �������� �������� �����");

            var parserId = (Guid)HttpContext.Items["ParserId"];

            try
            {
                await _parser.SetGroupsNamesForParser(parserId, dto.GroupNames);
                return Ok("������ ������� ��������� � ������.");
            }
            catch (Exception ex)
            {
                return BadRequest($"�� ������� �������� ������. {ex.Message}");
            }
        }


    }
}
