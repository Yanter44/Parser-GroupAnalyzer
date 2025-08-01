namespace MpParserAPI.Interfaces
{
    public interface IRedis
    {
        Task<bool> SetAddAsync(string key, string value);  
        Task<bool> SetContainsAsync(string key, string value);  
        Task<string[]> GetAllSetMembersAsync(string key);       
        Task<bool> RemoveSetMemberAsync(string key, string value);
        Task<bool> DeleteKeyAsync(string key);
    }
}
