namespace FxApi
{
    public class OHLC
    {
        public double close { get; set; }
        public int epoch { get; set; }
        public int granularity { get; set; }
        public double high { get; set; }
        public string id { get; set; }
        public double low { get; set; }
        public double open { get; set; }
        public int open_time { get; set; }
        public int pip_size { get; set; }
        public string symbol { get; set; }
    }
}