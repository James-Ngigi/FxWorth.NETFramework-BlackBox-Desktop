using Newtonsoft.Json;

namespace FxApi
{
    public class ForgetMessage
    {
        [JsonProperty("forget")]

        public string Forget { get; set; }
    }
}