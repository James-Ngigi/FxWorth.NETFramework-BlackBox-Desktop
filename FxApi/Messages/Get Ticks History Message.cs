using System;
using Newtonsoft.Json;

namespace FxApi
{
    public class GetTicksHistoryMessage
    {
        [JsonProperty("ticks_history")]
        public string TicksHistory { get; set; }

        [JsonProperty("adjust_start_time")]
        public int AdjustStartTime { get; set; } = 1;


        [JsonProperty("count")]
        public int Count { get; set; } = 1000;

        [JsonProperty("subscribe")]
        public int Subscribe { get; set; } = 1;

        [JsonProperty("granularity")]
        public int Granularity { get; set; } = 60;

        [JsonProperty("end")]
        public string End { get; set; } = "latest";

        [JsonProperty("start")]
        public int Start { get; set; } = 1;

        [JsonProperty("style")]
        public string Style { get; set; } = "candles";


        [JsonProperty("req_id")]
        public int RequestId { get; set; } = Environment.TickCount;
    }
}