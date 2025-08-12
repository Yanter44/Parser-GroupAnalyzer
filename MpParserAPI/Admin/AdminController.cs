using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MpParserAPI.DbContext;
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
        private readonly IDbContextFactory<ParserDbContext> _dbContextFactory;
        public AdminController(IAdmin adminService, IParserDataStorage parserDataStorage, 
            ISpaceProxy spaceproxyService, IDbContextFactory<ParserDbContext> dbContextFactory)
        {
            _adminService = adminService;
            _parserDataStorage = parserDataStorage;
            _spaceproxyService = spaceproxyService;
            _dbContextFactory = dbContextFactory;
        }
        [Authorize]
        [HttpGet("GetAllParsers")]
        public async Task<IActionResult> GetAllParsers()
        {
            var parsers = await _parserDataStorage.GetAllParsers();
            List<AllParsersResponceDto> allParsersResponceDtos = new List<AllParsersResponceDto>();
            foreach (var parser in parsers)
            {
                using var database = await _dbContextFactory.CreateDbContextAsync();
                var existparser = await database.ParsersStates.FirstOrDefaultAsync(x => x.ParserId == parser.Id);
                var model = new AllParsersResponceDto()
                {
                    ParserId = parser.Id.ToString(),
                    Password = parser.Password,
                    TgNickname = parser.Client.User.username,
                    TotalParsingTime = parser.TotalParsingMinutes?.ToString(@"hh\:mm\:ss") ?? "00:00:00",
                    PaidTotalParsingTime = existparser.PaidMinutes?.ToString(@"hh\:mm\:ss") ?? "00:00:00",
                    ProxyAddress = parser.ProxyAdress != null ? $"{parser.ProxyAdress.IpAddress}:{parser.ProxyAdress.Socks5Port}" : "???"
                };
                allParsersResponceDtos.Add(model);
            }
            return Ok(allParsersResponceDtos);
        }

        [Authorize]
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

        [Authorize]
        [HttpPost("SetNewProxy")]
        public async Task<IActionResult> SetNewProxy(SetNewProxyDto model)
        {
            var parserId = Guid.Parse(model.ParserId);
            var result = await _spaceproxyService.SetNewProxy(parserId, model.ProxyAdress);
            if (result)
            {
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
        [Authorize]
        [HttpDelete("DeleteUserAndParser")]
        public async Task<IActionResult> DeleteUserAndParser(DeleteUserAndParserDto model)
        {
            var result = await _adminService.DeleteUserAndParser(model);
            return Ok();
        }
    }
}
