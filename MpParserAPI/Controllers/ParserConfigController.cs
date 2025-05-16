using Microsoft.AspNetCore.Mvc;
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

        [HttpPost("StopParser")]
        public IActionResult StopParser([FromBody] Guid clientId)
        {
            _parser.StopParser(clientId);
            return Ok("Parser stopped.");
        }

        [HttpPost("AddParserKeywords")]
        public async Task<IActionResult> AddParserKeywords([FromQuery] Guid clientId, [FromBody] string keywords)
        {
            if (string.IsNullOrWhiteSpace(keywords))
                return BadRequest("Вы не ввели ключевые слова.");

            var result = await _parser.SetKeywordsFromText(clientId, keywords);
            return result.Success ? Ok(result.Message) : BadRequest(result.Message);
        }


        [HttpPost("AddGroupsToParser")]
        public async Task<IActionResult> AddGroupsToParser([FromQuery] Guid clientId, [FromBody] GroupNamesDto dto)
        {
            if (dto?.GroupNames == null || !dto.GroupNames.Any())
                return BadRequest("Список групп пустой. Попробуйте добавить название групп");

            try
            {
                await _parser.SetGroupsNamesForParser(clientId, dto.GroupNames);
                return Ok("Группы успешно добавлены в парсер.");
            }
            catch (Exception ex)
            {
                return BadRequest($"Не удалось добавить группы. {ex.Message}");
            }
        }


    }
}
