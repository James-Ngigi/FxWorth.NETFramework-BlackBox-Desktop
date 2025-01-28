namespace FxApi
{
    public class TicksResponse
    {
        public object echo_req { get; set; }
        public History history { get; set; }
        public string msg_type { get; set; }
        public int pip_size { get; set; }
        public int req_id { get; set; }
        public Subscription subscription { get; set; }
    }
}