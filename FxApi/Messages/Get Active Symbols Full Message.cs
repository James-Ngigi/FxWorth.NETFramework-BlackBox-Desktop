using Newtonsoft.Json;

namespace FxApi
{
    public class GetActiveSymbolsFullMessage
    {
        [JsonProperty("active_symbols")]
        public string ActiveSymbols { get; set; } = "brief";

        [JsonProperty("product_type")]
        public string ProductType { get; set; } = "basic";
    }
}