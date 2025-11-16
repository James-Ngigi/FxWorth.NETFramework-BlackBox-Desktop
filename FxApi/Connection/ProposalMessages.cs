using System;
using Newtonsoft.Json;

namespace FxApi.Connection
{
    public class ProposalRequest
    {
        [JsonProperty("proposal")]
        public int proposal { get; } = 1;

        [JsonProperty("amount")]
        public decimal amount { get; set; }

        [JsonProperty("barrier")]
        public string barrier { get; set; }

        [JsonProperty("basis")]
        public string basis { get; set; } = "stake";

        [JsonProperty("contract_type")]
        public string contract_type { get; set; }

        [JsonProperty("currency")]
        public string currency { get; set; }

        [JsonProperty("duration")]
        public int duration { get; set; }

        [JsonProperty("duration_unit")]
        public string duration_unit { get; set; }

        [JsonProperty("symbol")]
        public string symbol { get; set; }

        [JsonProperty("req_id")]
        public int req_id { get; set; } = Environment.TickCount;
    }

    public class ProposalResponse
    {
        [JsonProperty("proposal")]
        public ProposalPayload proposal { get; set; }

        [JsonProperty("echo_req")]
        public ProposalEcho echo_req { get; set; }

        [JsonProperty("msg_type")]
        public string msg_type { get; set; }

        [JsonProperty("error")]
        public ProposalError error { get; set; }
    }

    public class ProposalPayload
    {
        [JsonProperty("ask_price")]
        public decimal ask_price { get; set; }

        [JsonProperty("payout")]
        public decimal payout { get; set; }

        [JsonProperty("id")]
        public string id { get; set; }

        [JsonProperty("longcode")]
        public string longcode { get; set; }

        [JsonProperty("return")]
        public decimal? contract_return { get; set; }
    }

    public class ProposalEcho
    {
        [JsonProperty("req_id")]
        public int req_id { get; set; }
    }

    public class ProposalError
    {
        [JsonProperty("code")]
        public string code { get; set; }

        [JsonProperty("message")]
        public string message { get; set; }
    }
}
