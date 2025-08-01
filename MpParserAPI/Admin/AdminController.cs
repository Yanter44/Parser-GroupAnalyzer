using Microsoft.AspNetCore.Mvc;
using MpParserAPI.Interfaces;
using MpParserAPI.Models.AdminDtos;

namespace MpParserAPI.Admin
{
    [ApiController]
    [Route("[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly IAdmin _adminService;
        private readonly IParserDataStorage _parserDataStorage;
        private readonly ISpaceProxy _spaceproxyService;
        public AdminController(IAdmin adminService, IParserDataStorage parserDataStorage, ISpaceProxy spaceproxyService)
        {
            _adminService = adminService;
            _parserDataStorage = parserDataStorage;
            _spaceproxyService = spaceproxyService;
        }
        [HttpGet("GetAllParsers")]
        public async Task<IActionResult> GetAllParsers()
        {
            var parsers = await _parserDataStorage.GetAllParsers();
            List<AllParsersResponceDto> allParsersResponceDtos = new List<AllParsersResponceDto>();
            foreach (var parser in parsers)
            {
                var model = new AllParsersResponceDto()
                {
                    ParserId = parser.Id.ToString(),
                    Password = parser.Password,
                    TgNickname = parser.Client.User.username,
                    TotalParsingTime = parser.TotalParsingMinutes?.ToString(@"hh\:mm\:ss") ?? "00:00:00",
                    PaidTotalParsingTime = parser.TotalParsingMinutes?.ToString(@"hh\:mm\:ss") ?? "00:00:00",
                    ProxyAddress = parser.ProxyAdress != null ? $"{parser.ProxyAdress.IpAddress}:{parser.ProxyAdress.Socks5Port}" : "???"
                };
                allParsersResponceDtos.Add(model);
            }
            return Ok(allParsersResponceDtos);
        }
       
        [HttpPost("AddTimeParsing")]
        public async Task<IActionResult> AddTimeParsing(AddTimeParsingModelDto model)
        {
            var result = await _adminService.AddTimeParsing(model);
            if (result.Success)
            {
                return Ok();
            }
            return BadRequest();
        }

        [HttpPost("SetNewProxy")]
        public async Task<IActionResult> SetNewProxy(SetNewProxyDto model)
        {
            var parserId = Guid.Parse(model.ParserId);
            var result = await _spaceproxyService.SetNewProxy(parserId, model.ProxyAdress);
            if (result)
            {
                // Передаем parserId в метод
                var availableproxy = await _spaceproxyService.GetAvailableProxyByProxyAdress(
                    model.ProxyAdress,
                    parserId);

                if (availableproxy != null)
                {
                    var reconnectedresult = await _spaceproxyService.ReconnectWithNewProxy(
                        parserId,
                        availableproxy);

                    if (reconnectedresult)
                        return Ok();
                }
            }
            return BadRequest();
        }

        [HttpDelete("DeleteUserAndParser")]
        public async Task<IActionResult> DeleteUserAndParser(DeleteUserAndParserDto model)
        {
            var result = await _adminService.DeleteUserAndParser(model);
            return Ok();
        }
    }
}
