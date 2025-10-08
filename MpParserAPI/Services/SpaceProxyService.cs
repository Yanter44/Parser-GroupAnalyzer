using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using MpParserAPI.Controllers;
using MpParserAPI.DbContext;
using MpParserAPI.Interfaces;
using MpParserAPI.Models.SpaceProxyDto;
using WTelegram;

namespace MpParserAPI.Services
{
    public class SpaceProxyService : ISpaceProxy
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly IParserDataStorage _parserDataStorage;
        private readonly ILogger<SpaceProxyService> _logger;
        private readonly IHubContext<ParserHub> _parserHubContext;
        private readonly IDbContextFactory<ParserDbContext> _dbContextFactory;
        private ConcurrentDictionary<string, Guid> ProxyServersAndParsersIds = new ConcurrentDictionary<string, Guid>();
        //public event Func<Guid, ProxyInfo, Task> ProxyChanged;

        //ip-address proxy and parserId 
        public SpaceProxyService(IHttpClientFactory httpClientFactory,
            IConfiguration configuration, 
            IParserDataStorage parserDataStorage,
            ILogger<SpaceProxyService> logger,
            IHubContext<ParserHub> parserHub,
            IDbContextFactory<ParserDbContext> dbcontextFactory)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _parserDataStorage = parserDataStorage;
            _logger = logger;
            _parserHubContext = parserHub;
            _dbContextFactory = dbcontextFactory;
        }
        public bool TryGetProxyOwner(string ipAddress, out Guid parserId)
        {
            return ProxyServersAndParsersIds.TryGetValue(ipAddress, out parserId);
        }

        public bool TryRemoveProxy(string ipAddress)
        {
            if (string.IsNullOrEmpty(ipAddress))
                return false;

            return ProxyServersAndParsersIds.TryRemove(ipAddress, out _);
        }

        public void AddOrUpdateProxy(string ipAddress, Guid parserId)
        {
            if (string.IsNullOrEmpty(ipAddress))
                return;

            ProxyServersAndParsersIds.AddOrUpdate(ipAddress, parserId, (key, oldValue) => parserId);
        }
        public async Task<ProxyInfo> GetProxyByAddress(string proxyAddress)
        {
            if (string.IsNullOrWhiteSpace(proxyAddress))
                return null;

            var httpClient = _httpClientFactory.CreateClient();
            var apiKey = _configuration["SpaceProxy:ApiKey"];
            var response = await httpClient.GetAsync($"https://panel.spaceproxy.net/api/proxies/?api_key={apiKey}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SpaceProxyResponse>();
            var parts = proxyAddress.Split(':');
            var ip = parts[0];
            var port = parts[1];

            return result.Proxies.FirstOrDefault(p =>
                p.IpAddress.Equals(ip, StringComparison.OrdinalIgnoreCase) &&
                p.Socks5Port.ToString() == port
            );
        }



        public async Task<ProxyInfo> GetAvailableProxyByProxyAdress(string proxyAddress)
        {
            if (string.IsNullOrWhiteSpace(proxyAddress))
                return null;

            var parts = proxyAddress.Split(':');
            if (parts.Length < 2) return null;

            var proxyIp = parts[0];
            if (!int.TryParse(parts[1], out var proxyPort)) return null;

            var httpClient = _httpClientFactory.CreateClient();
            var apiKey = _configuration["SpaceProxy:ApiKey"];
            var response = await httpClient.GetAsync($"https://panel.spaceproxy.net/api/proxies/?api_key={apiKey}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SpaceProxyResponse>();
            return result.Proxies.FirstOrDefault(p =>
                p.IpAddress.Equals(proxyIp, StringComparison.OrdinalIgnoreCase)
                && p.Socks5Port == proxyPort);
        }


        //пока ниже лишнее всеее________________________________________________________

        public async Task<ProxyInfo> GetAndSetAvailableProxy(Guid parserId)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var apikey = _configuration["SpaceProxy:ApiKey"];
            var response = await httpClient.GetAsync($"https://panel.spaceproxy.net/api/proxies/?api_key={apikey}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SpaceProxyResponse>();
            var listproxies = result.Proxies;

            foreach (var proxy in listproxies)
            {
                if (ProxyServersAndParsersIds.TryAdd(proxy.IpAddress, parserId))
                {
                    if (_parserDataStorage.TryGetParser(parserId, out var parser) && parser != null)
                    {
                        parser.ProxyAdress = proxy;
                        return proxy;
                    }
                    else
                    {
                        ProxyServersAndParsersIds.TryRemove(proxy.IpAddress, out _);
                        continue;
                    }
                }
            }
            return null;
        }

        public async Task<ProxyInfo> GetAvailableProxy()
        {
            var httpClient = _httpClientFactory.CreateClient();
            var apiKey = _configuration["SpaceProxy:ApiKey"];
            var response = await httpClient.GetAsync($"https://panel.spaceproxy.net/api/proxies/?api_key={apiKey}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SpaceProxyResponse>();
            var listProxies = result.Proxies;

            foreach (var proxy in listProxies)
            {
                if (!ProxyServersAndParsersIds.ContainsKey(proxy.IpAddress))
                {
                    return proxy;
                }
            }

            return null; 
        }

        public async Task<List<ProxyInfo>> GetAllProxiesAsync()
        {
            var httpclient = _httpClientFactory.CreateClient();
            var apiKey = _configuration["SpaceProxy:ApiKey"];
            var responce = await httpclient.GetAsync($"https://panel.spaceproxy.net/api/proxies/?api_key={apiKey}");

            responce.EnsureSuccessStatusCode();

            var result = await responce.Content.ReadFromJsonAsync<SpaceProxyResponse>();
            return result.Proxies;
        }
    }
}
