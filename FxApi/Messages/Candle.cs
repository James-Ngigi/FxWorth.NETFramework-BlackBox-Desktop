namespace FxApi
{
    public class Candle
    {
        public double close { get; set; }
        public int epoch { get; set; }
        public double high { get; set; }
        public double low { get; set; }
        public double open { get; set; }

        public int tick_count { get; set; }
        public override string ToString()
        {
            return $"{nameof(close)}: {close}, {nameof(epoch)}: {epoch}, {nameof(high)}: {high}, {nameof(low)}: {low}, {nameof(open)}: {open}";
        }
    }
}