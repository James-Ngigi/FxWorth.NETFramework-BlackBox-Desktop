namespace FxApi
{
    public class Tick
    {
        public double ask { get; set; }
        public double bid { get; set; }
        public int epoch { get; set; }
        public string id { get; set; }
        public int pip_size { get; set; }
        public double quote { get; set; }
        public string symbol { get; set; }
    }
}