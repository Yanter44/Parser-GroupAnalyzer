using StackExchange.Redis;

namespace MpParserAPI.Services
{
    public class RedisService : Interfaces.IRedis
    {
        private readonly ConnectionMultiplexer _redis;
        private readonly IDatabase _database;

        public RedisService(IConfiguration configuration)
        {
            var redisConnectionString = configuration.GetConnectionString("Redis") 
                                        ?? configuration["Redis:ConnectionString"];
             _redis = ConnectionMultiplexer.Connect(redisConnectionString);
            _database = _redis.GetDatabase();
        }

        public Task<bool> SetAddAsync(string key, string value)
        {
            return _database.SetAddAsync(key, value);
        }

        public Task<bool> SetContainsAsync(string key, string value)
        {
            return _database.SetContainsAsync(key, value);
        }

        public async Task<string[]> GetAllSetMembersAsync(string key)
        {
            var values = await _database.SetMembersAsync(key);
            return values.Select(x => (string)x).ToArray();
        }

        public Task<bool> RemoveSetMemberAsync(string key, string value)
        {
            return _database.SetRemoveAsync(key, value);
        }

        public Task<bool> DeleteKeyAsync(string key)
        {
            return _database.KeyDeleteAsync(key);
        }
        public async Task<long> SetAddRangeAsync(string key, IEnumerable<string> values)
        {
            var redisValues = values.Select(x => (RedisValue)x).ToArray();
            return await _database.SetAddAsync(key, redisValues); 
        }
    }
}
