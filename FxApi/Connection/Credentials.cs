namespace FxApi
{
    /// <summary>
    /// The `Credentials` class encapsulates the authentication credentials required to connect to a Deriv trading account. 
    /// It holds the API token, Client ID, a user-friendly name for the account, and a flag indicating whether the account is currently enabled for trading.
    /// </summary>
    public class Credentials
    {
        public string Name { get; set; }

        public string Token { get; set; }
        
        public string AppId { get; set; }

        public bool IsChecked { get; set; }

        public decimal ProfitTarget { get; set; }
    }
}