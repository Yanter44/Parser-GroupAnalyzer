using MpParserAPI.Models.SpaceProxyDto;

namespace MpParserAPI.Interfaces
{
    public interface ISpaceProxy
    {
        Task<List<ProxyInfo>> GetAllProxiesAsync();
        Task<ProxyInfo> GetAndSetAvailableProxy(Guid parserId);
        Task<ProxyInfo> GetAvailableProxy();
        Task<ProxyInfo> GetAvailableProxyByProxyAdress(string proxyAddress);
        Task<ProxyInfo> GetProxyByAddress(string proxyAddress);
        bool TryRemoveProxy(string ipAddress);
        void AddOrUpdateProxy(string ipAddress, Guid parserId);
        bool TryGetProxyOwner(string ipAddress, out Guid parserId);
    }
}
