using System.Collections.Generic;

namespace FxApi
{
    public class CandlesResponse
    {
        public List<Candle> candles { get; set; }
        public object echo_req { get; set; }
        public string msg_type { get; set; }
        public int pip_size { get; set; }
        public int req_id { get; set; }
        public Subscription subscription { get; set; }
    }
}