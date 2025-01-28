namespace FxApi
{
    public class TickMessage
    {
        public object echo_req { get; set; }
        public string msg_type { get; set; }
        public int req_id { get; set; }
        public Subscription subscription { get; set; }
        public Tick tick { get; set; }
    }
}