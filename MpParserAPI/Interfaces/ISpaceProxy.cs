using MpParserAPI.Models.SpaceProxyDto;

namespace MpParserAPI.Interfaces
{
    public interface ISpaceProxy
    {
        Task<List<ProxyInfo>> GetAllProxiesAsync();
        Task<ProxyInfo> GetAndSetAvailableProxy(Guid parserId);
        Task<bool> SetNewProxy(Guid parserId, string ProxyAdress);
        Task<ProxyInfo> GetAvailableProxy();
        Task<ProxyInfo> GetAvailableProxyByProxyAdress(string proxyAddress);
        Task<bool> ReconnectWithNewProxy(Guid parserId, ProxyInfo proxy);
    }
}
