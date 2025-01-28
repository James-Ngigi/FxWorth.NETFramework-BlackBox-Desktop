using Newtonsoft.Json;

namespace FxApi
{
    public class ForgetAllMessage
    {
        [JsonProperty("forget_all")]

        public string Forget { get; set; }
    }
}