using System.Text.Json.Serialization;

namespace MpParserAPI.Models.SpaceProxyDto
{
    public class ProxyInfo
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("ip")]
        public string IpAddress { get; set; }

        [JsonPropertyName("port_http")]
        public int HttpPort { get; set; }

        [JsonPropertyName("port_socks5")]
        public int Socks5Port { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("password")]
        public string Password { get; set; }

        [JsonPropertyName("country")]
        public string CountryCode { get; set; }

    }
}
