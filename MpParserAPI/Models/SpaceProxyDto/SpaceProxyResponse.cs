using System.Text.Json.Serialization;

namespace MpParserAPI.Models.SpaceProxyDto
{
    public class SpaceProxyResponse
    {
        [JsonPropertyName("count")]
        public int TotalCount { get; set; }

        [JsonPropertyName("next")]
        public string NextPageUrl { get; set; }

        [JsonPropertyName("previous")]
        public string PreviousPageUrl { get; set; }

        [JsonPropertyName("results")]
        public List<ProxyInfo> Proxies { get; set; }
    }
}
