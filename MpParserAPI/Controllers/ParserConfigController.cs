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
                _logger.LogInformation("Парсер успешно запущен для ParserId: {ParserId}", parserId);
                return Ok("Парсинг успешно запущен.");
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
            _logger.LogInformation("Парсер успешно остановлен для ParserId: {ParserId}", parserId);
            return Ok("Парсер остановлен.");
        }
        [ParserAuthorize]
        [HttpPost("Logout")]
        public IActionResult Logout()
        {

            var parserId = (Guid)HttpContext.Items["ParserId"];
            _parser.StopParsing(parserId);
            _logger.LogInformation("Парсер успешно остановлен для ParserId: {ParserId}", parserId);

            Response.Cookies.Delete("ParserId");
            Response.Cookies.Delete("ParserPassword");

            _logger.LogInformation("Пользователь вышел из системы и куки удалены.");
            return Ok("Парсер остановлен.");
        }

      //  [ParserAuthorize]
        [HttpGet("GetParserState")]
        public async Task<IActionResult> GetParserState()
        {
            var parserIdStr = HttpContext.Request.Cookies["ParserId"];
            if (!Guid.TryParse(parserIdStr, out var parserId))
                return Unauthorized("ParserId отсутствует или некорректен");

            _logger.LogInformation("GetParserState: старт для parserId={ParserId}", parserId);

            var parserData = await _parser.GetParserState(parserId);
            _logger.LogInformation("GetParserState: _parser.GetParserState вернулось, data={ParserData}", parserData);

            if (parserData == null)
            {
                _logger.LogInformation("GetParserState: parserData == null, вернем NotFound");
                return NotFound("Парсер не найден");
            }

            _logger.LogInformation("GetParserState: успешное выполнение, вернем Ok");
            return Ok(parserData);
        }


        [ParserAuthorize]
        [HttpPost("AddParserKeywords")]
        public async Task<IActionResult> AddParserKeywords([FromBody] List<string> keywords)
        {
            _logger.LogInformation("AddParserKeywords вызван с ключевыми словами: {Keywords}", string.Join(", ", keywords));

            if (keywords == null)
            {
                _logger.LogWarning("AddParserKeywords: Ключевые слова не переданы (null)");
                return BadRequest("Ключевые слова не переданы.");
            }

            var parserId = (Guid)HttpContext.Items["ParserId"];

            var result = await _parser.SetKeywords(parserId, keywords);

            if (result.Success)
            {
                _logger.LogInformation("Ключевые слова успешно установлены для {ParserId}:", parserId);
                return Ok(result.Message);
            }
            else
            {
                _logger.LogWarning("Не удалось установить ключевые слова для ParserId: {ParserId}. Message: {Message}", parserId, result.Message);
                return BadRequest(result.Message);
            }
        }


        [ParserAuthorize]
        [HttpPost("AddGroupsToParser")]
        public async Task<IActionResult> AddGroupsToParser([FromBody] GroupNamesDto dto)
        {
            _logger.LogInformation("AddGroupsToParser вызван с группами: {Groups}", dto?.GroupNames);

            if (!dto.GroupNames.Any())
            {
                _logger.LogInformation("AddGroupsToParser: пришёл пустой список — очищаем группы");
            }

            var parserId = (Guid)HttpContext.Items["ParserId"];

            try
            {
                await _parser.SetGroupsNames(parserId, dto.GroupNames);
                _logger.LogInformation("Группы успешно добавлены для ParserId: {ParserId}", parserId);
                return Ok("Группы успешно добавлены в парсер.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "пиздец произошел при добавлении групп for ParserId: {ParserId}", parserId);
                return BadRequest($"Не удалось добавить группы. {ex.Message}");
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
