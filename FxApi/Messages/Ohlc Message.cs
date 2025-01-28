namespace FxApi
{
    public class OhlcMessage
    {
        public object echo_req { get; set; }
        public string msg_type { get; set; }
        public OHLC ohlc { get; set; }
        public int req_id { get; set; }
        public Subscription subscription { get; set; }
    }
}