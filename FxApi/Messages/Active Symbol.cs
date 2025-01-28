namespace FxApi
{
    public class ActiveSymbol
    {
        public int allow_forward_starting { get; set; }
        public string display_name { get; set; }
        public int exchange_is_open { get; set; }
        public int is_trading_suspended { get; set; }
        public string market { get; set; }
        public string market_display_name { get; set; }
        public double pip { get; set; }
        public string submarket { get; set; }
        public string submarket_display_name { get; set; }
        public string symbol { get; set; }
        public string symbol_type { get; set; }
    }
}