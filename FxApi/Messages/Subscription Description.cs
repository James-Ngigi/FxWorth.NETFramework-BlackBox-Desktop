namespace FxApi
{
    public class SubscriptionDescription
    {
        public ActiveSymbol ActiveSymbol { get; set; }
        public string RemoteId { get; set; }
        public int LocalId { get; set; }

        public ChartsCache Cache { get; set; } 
        public IIndicator Token { get; set; }
    }
}