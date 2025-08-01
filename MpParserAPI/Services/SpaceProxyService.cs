using System.Collections.Concurrent;
using System.Net;
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
        private ConcurrentDictionary<string, Guid> ProxyServersAndParsersIds = new ConcurrentDictionary<string, Guid>();

        //ip-address proxy and parserId 
        public SpaceProxyService(IHttpClientFactory httpClientFactory, IConfiguration configuration, IParserDataStorage parserDataStorage, ILogger<SpaceProxyService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _parserDataStorage = parserDataStorage;
            _logger = logger;
        }

        public async Task<bool> SetNewProxy(Guid parserId, string ProxyAdress)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var apikey = _configuration["SpaceProxy:ApiKey"];
            var response = await httpClient.GetAsync($"https://panel.spaceproxy.net/api/proxies/?api_key={apikey}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SpaceProxyResponse>();
            var listproxies = result.Proxies;
            var proxyAddressIp = ProxyAdress.Split(':')[0];
            var proxyAddressPort = ProxyAdress.Split(':')[1];

            if (!_parserDataStorage.TryGetParser(parserId, out var parser))
                return false;

            foreach (var proxy in listproxies)
            {
                if (proxy.IpAddress.Equals(proxyAddressIp, StringComparison.OrdinalIgnoreCase) &&
                    proxy.Socks5Port.ToString() == proxyAddressPort)
                {
                    parser.ProxyAdress = proxy;
                    return true;
                }
            }
            return false;
        }

        public async Task<bool> ReconnectWithNewProxy(Guid parserId, ProxyInfo proxy)
        {
            if (!_parserDataStorage.TryGetParser(parserId, out var parser))
                return false;

            var oldProxy = parser.ProxyAdress;
            var oldProxyIp = oldProxy?.IpAddress;

            try
            {
                if (!string.IsNullOrEmpty(oldProxyIp))
                {
                    ProxyServersAndParsersIds.TryRemove(oldProxyIp, out _);
                }

                ProxyServersAndParsersIds.AddOrUpdate(
                    proxy.IpAddress,
                    parserId,
                    (key, oldValue) => parserId);

                parser.ProxyAdress = proxy;

                parser.DisposeData();
                parser.Client = new Client(what =>
                {
                    if (what == "session_pathname")
                        return GetSessionPath(parser.Phone, isTemp: false);

                    return what switch
                    {
                        "api_id" => "22262339",
                        "api_hash" => "fc15371db5ea0ba274b93faf572aec6b",
                        "phone_number" => parser.Phone,
                        _ => null
                    };
                });

                parser.Client.TcpHandler = async (address, port) =>
                {
                    var proxyClient = new Leaf.xNet.Socks5ProxyClient(proxy.IpAddress, proxy.Socks5Port)
                    {
                        Username = proxy.Username,
                        Password = proxy.Password
                    };
                    return proxyClient.CreateConnection(address, port);
                };

                await parser.Client.LoginUserIfNeeded();
                parser.AuthState = TelegramAuthState.Authorized;

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Ошибка при переподключении парсера {parserId}: {ex.Message}");

                if (parser.ProxyAdress != null && parser.ProxyAdress.IpAddress == proxy.IpAddress)
                {
                    ProxyServersAndParsersIds.TryRemove(proxy.IpAddress, out _);

                    if (!string.IsNullOrEmpty(oldProxyIp))
                    {
                        ProxyServersAndParsersIds.TryAdd(oldProxyIp, parserId);
                    }
                    parser.ProxyAdress = oldProxy;
                }

                return false;
            }
        }

        public async Task<ProxyInfo> GetAvailableProxyByProxyAdress(string proxyAddress, Guid parserId)
        {
            if (string.IsNullOrWhiteSpace(proxyAddress))
                return null;

            var parts = proxyAddress.Split(':');
            if (parts.Length < 2) return null;

            var proxyAddressIp = parts[0];
            int proxyAddressPort;
            if (!int.TryParse(parts[1], out proxyAddressPort)) return null;

            var httpClient = _httpClientFactory.CreateClient();
            var apiKey = _configuration["SpaceProxy:ApiKey"];
            var response = await httpClient.GetAsync($"https://panel.spaceproxy.net/api/proxies/?api_key={apiKey}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<SpaceProxyResponse>();

            foreach (var proxy in result.Proxies)
            {
                if (proxy.IpAddress.Equals(proxyAddressIp, StringComparison.OrdinalIgnoreCase) &&
                    proxy.Socks5Port == proxyAddressPort)
                {
     
                    if (!ProxyServersAndParsersIds.TryGetValue(proxy.IpAddress, out var currentParserId) ||
                        currentParserId == parserId)
                    {
                        return proxy;
                    }
                }
            }
            return null;
        }





        //пока ниже лишнее всеее________________________________________________________
        private string GetSessionPath(string phone, bool isTemp)
        {
            var cleanedPhone = new string(phone.Where(char.IsDigit).ToArray());
            var sessionsFolder = Path.Combine(AppContext.BaseDirectory, "sessions");
            if (!Directory.Exists(sessionsFolder))
                Directory.CreateDirectory(sessionsFolder);
            return Path.Combine(sessionsFolder, $"{(isTemp ? "temp_session" : "session")}_{cleanedPhone}.session");
        }

        public async Task<ProxyInfo> GetAndSetAvailableProxy(Guid parserId)
        {
            var httpClient = _httpClientFactory.CreateClient();
            var apikey = _configuration["SpaceProxy:ApiKey"];
            var responce = await httpClient.GetAsync($"https://panel.spaceproxy.net/api/proxies/?api_key={apikey}");
            responce.EnsureSuccessStatusCode();

            var result = await responce.Content.ReadFromJsonAsync<SpaceProxyResponse>();
            var listproxies = result.Proxies;

            foreach (var proxy in listproxies)
            {
                if (ProxyServersAndParsersIds.TryGetValue(proxy.IpAddress, out _))
                {
                    continue;
                }
                else
                {
                    var resultt = ProxyServersAndParsersIds.TryAdd(proxy.IpAddress, parserId);
                    if (resultt)
                    {
                        _parserDataStorage.TryGetParser(parserId, out var parser);
                        if(parser != null)
                        {
                            parser.ProxyAdress = proxy;
                            return proxy;
                        }        
                       
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
