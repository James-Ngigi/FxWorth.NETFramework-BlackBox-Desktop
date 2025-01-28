namespace FxApi
{
    /// <summary>
    /// The `Credentials` class encapsulates the authentication credentials required to connect to a Deriv trading account. 
    /// It holds the API token, Client ID, a user-friendly name for the account, and a flag indicating whether the account is currently enabled for trading.
    /// </summary>
    public class Credentials
    {
        /// A user-friendly name for the trading account. 
        public string Name { get; set; }

        /// The API token used for authenticating with the Deriv API. 
        /// This token is unique to each Deriv account and grants access to the account's data and trading capabilities.
        public string Token { get; set; }

        /// The Client ID(Application ID) associated with the Deriv account. This is a numerical identifier assigned to an Api token.
        public string AppId { get; set; }

        /// A flag indicating whether this trading account is currently enabled for trading within the application.
        /// This allows users to selectively activate or deactivate accounts for trading without removing their credentials.
        public bool IsChecked { get; set; }
    }
}