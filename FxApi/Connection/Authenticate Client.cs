using System;
using System.Collections.Generic;
using System.Linq;
using FxApi.Connection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using SuperSocket.ClientEngine;
using WebSocket4Net;
using DateTime = System.DateTime;

namespace FxApi
{
    /// <summary>
    /// The `AuthClient` class is responsible for authenticating with the Deriv API, managing account balances, 
    /// placing trades, and monitoring trade outcomes. It extends the `BinaryClientBase` class to handle 
    /// WebSocket communication and leverages the `TradingParameters` class to define the trading strategy. 
    /// </summary>
    public class AuthClient : BinaryClientBase
    {
        // Logger for recording authentication, balance, trade placement, and outcome information.
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        // Holds the authorization information received from the Deriv API after successful authentication.
        // Contains details about the account, balance, and permissions.
        private Authorize authInfo;

        // Event raised when the account balance changes. Used to notify the UI or other components about balance updates.
        public EventHandler<EventArgs> BalanceChanged;

        // Event raised when authentication with the Deriv API fails. 
        // Indicates an issue with the provided API token or App ID.
        public EventHandler<EventArgs> AuthFailed;

        // Event raised when a trade's state changes (e.g., new trade placed, trade closed, trade outcome updated). 
        // Used to notify the UI or other components about trade updates.
        public EventHandler<TradeEventArgs> TradeChanged;

        public string GetToken()
        {
            return Credentials.Token;
        }

        /// <summary>
        /// Constructor for the `AuthClient` class.
        /// <param name="credentials">The API credentials (token and App ID) used for authentication.</param>
        /// <param name="initialCounter">An initial counter value used for generating unique trade IDs. This ensures that trade IDs are distinct across multiple clients.</param>
        /// </summary>
        public AuthClient(Credentials credentials, int initialCounter) : base(credentials)
        {
            // Initialize the trade ID counter with the provided initial value.
            tradeIdCounter = initialCounter;
        }

        // The trading parameters associated with this client, defining the trading strategy and risk management rules.
        public virtual TradingParameters TradingParameters { get; set; }

        // The current account balance, updated as trades are executed and balance updates are received from the Deriv API.
        public decimal Balance { get; set; }

        // The current profit and loss (P&L) for this client, calculated based on the outcomes of executed trades.
        public decimal Pnl { get; set; }

        // Timestamp of the last losing trade. Used for implementing time-based trading restrictions (e.g., preventing consecutive trades after a loss).
        public DateTime LossTradeTime { get; set; }

        // Timestamp of the last trade, regardless of outcome. Used for implementing time-based trading restrictions.
        public DateTime AnyTradeTime { get; set; }

        // A counter used to generate unique trade IDs for each trade placed by this client.
        private int tradeIdCounter;

        // A set of request IDs for "buy" requests that are currently pending completion (waiting for a response from the server).
        private HashSet<int> waitingClosedIds = new HashSet<int>();

        // A set of contract IDs for trades that are currently open (waiting for a sell transaction to be received).
        private HashSet<long> waitingContradctIds = new HashSet<long>();

        // A flag indicating whether this client is currently engaged in a trade (a trade is considered "in progress" if the `model` is not null).
        public bool IsTrading => model != null;

        // A counter for tracking the number of transactions processed by this client.
        public int transactionsCounter = 0;

        // Temporary storage for contract ID.
        private long currentContractId;

        // Temporary storage for Transaction time
        private int currentTransactionTime;


        /*---------------------------------------------------------------------------------------------------------------------*/


        /// A `TradeModel` object representing the current trade in progress.
        /// This object stores details about the trade, such as stake, profit/loss, and state.
        private TradeModel model;

        /// <summary>
        /// Places a "Buy" (Higher) trade on the specified symbol.
        /// This is the application of the hedging strategy by placing one buy trade here and one sell trade in the `Sell` method below
        /// <param name="symbol">The trading symbol (e.g., "1HZ50V").</param>
        /// <param name="duration">The duration of the trade.</param>
        /// <param name="durationUnit">The unit of time for the trade duration (e.g., "Ticks", "Seconds", "Minutes").</param>
        /// <param name="stake">The stake amount for the trade.</param>
        /// <param name="barrier">The price barrier offset for the trade.(Currently depricate to accomodate hierarchy trades)</param>
        /// </summary>
        public void Buy(string symbol, int duration, string durationUnit, decimal stake)
        {
            // Call the `SendPriceProposal` method to send a "Buy" (CALL) price proposal to the server.
            SendPriceProposal(symbol, duration, durationUnit, stake, "CALL");
        }

        /// <summary>
        /// Places a "Sell" (Lower) trade on the specified symbol.
        /// The sell trade is used to hedge the buy trade placed in the `Buy` method.
        /// <param name="symbol">The trading symbol (e.g., "EURUSD").</param>
        /// <param name="duration">The duration of the trade.</param>
        /// <param name="durationUnit">The unit of time for the trade duration (e.g., "Ticks", "Seconds", "Minutes").</param>
        /// <param name="stake">The stake amount for the trade.</param>
        /// <param name="barrier">The price barrier offset for the trade.</param>
        /// </summary>
        public void Sell(string symbol, int duration, string durationUnit, decimal stake)
        {
            // Call the `SendPriceProposal` method to send a "Sell" (PUT) price proposal to the server.
            SendPriceProposal(symbol, duration, durationUnit, stake, "PUT");
        }

        /// <summary>
        /// Sends a price proposal to the server to initiate a trade. 
        /// Handles trade ID generation, request ID management, and trade model initialization.
        /// </summary>
        private void SendPriceProposal(string symbol, int duration, string durationUnit, decimal stake, string contractType)
        {
            // Synchronize access to the `waitingClosedIds` set to prevent race conditions when multiple threads might try to modify it.
            lock (waitingClosedIds)
            {
                // If there is no current trade in progress (model is null), create a new TradeModel.
                if (model == null)
                {
                    // Create a new TradeModel object to track the trade details.
                    model = new TradeModel
                    {
                        // Assign a unique ID to the trade using the tradeIdCounter.
                        Id = tradeIdCounter++,
                        // Set the stake amount for the trade.
                        Stake = stake,
                        // Set the API token associated with this trade.
                        Token = Credentials.Token
                    };

                    // Raise the TradeUpdate event to notify listeners about the new trade.
                    RaiseTradeUpdate();
                }
            }

            // Create a new PriceProposal object to encapsulate the trade parameters.
            var request = new PriceProposal();

            // Set the currency for the trade based on the authorized account information.
            request.currency = authInfo.currency;
            // Set the trading symbol.
            request.symbol = symbol;
            // Set the trade duration.
            request.duration = duration;
            // Set the contract type (CALL or PUT).
            request.contract_type = contractType;

            // Get the appropriate barrier unit (t, s, m, h, d) from the duration unit.
            var unit = GetBarrierUnit(durationUnit);
            // Set the barrier unit in the request.
            request.duration_unit = unit;
            // Set the stake amount.
            request.amount = stake;
            // Set the barrier offset.
            request.barrier = contractType == "CALL" ? TradingParameters.BuyBarrier : TradingParameters.SellBarrier;

            // Create a new BuyContractRequest object, which will be sent to the server.
            var contract = new BuyContractRequest();

            // Synchronize access to the `waitingClosedIds` set.
            lock (waitingClosedIds)
            {
                // Generate a unique request ID using the transactionsCounter and add it to the waitingClosedIds set.
                contract.req_id += transactionsCounter++;
                waitingClosedIds.Add(contract.req_id);
            }

            // Set the price proposal parameters in the BuyContractRequest.
            contract.parameters = request;
            // Set the price (stake) for the contract.
            contract.price = stake;
            // Send the BuyContractRequest to the server using the `Send` method inherited from BinaryClientBase.
            Send(contract);
        }


        /*---------------------------------------------------------------------------------------------------------------------*/


        /// <summary>
        /// Extracts the first letter of the duration unit string and returns it as the barrier unit.
        /// Example: "Minutes" would return "m".
        /// <param name="durationUnit">The unit of time for the trade duration (e.g., "Ticks", "Seconds", "Minutes").</param>
        /// <returns>The single-letter barrier unit (e.g., "t", "s", "m").</returns>
        /// </summary>
        private static string GetBarrierUnit(string durationUnit)
        {
            // Convert the duration unit to lowercase, get the first character, and convert it back to a string.
            var unit = durationUnit.ToLower()[0].ToString();
            return unit;
        }

        /// <summary>
        /// Handles WebSocket error events. 
        /// Raises the `AuthFailed` event if the error indicates an unauthorized connection.
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Error event arguments containing details about the error.</param>
        /// </summary>
        protected override void SockOnError(object sender, ErrorEventArgs e)
        {
            // Check for Unauthorized error (invalid credentials) and stop the client
            if (e.Exception.Message.Contains("Unauthorized"))
            {
                // Set client status to "Invalid" using the new event
                OnStatusChanged("Invalid");
                logger.Error("<=> Authentication failed. Invalid API token or App ID. Stopping client.");
                isDisposed = true; // Prevent further reconnections
                AuthFailed?.Raise(this, EventArgs.Empty);
                StopInternal();
                return; // Do NOT call base.SockOnError to prevent reconnection
            }

            // For other errors, call base class method to log and potentially attempt reconnect if it isn't an Unauthorized issue.
            base.SockOnError(sender, e);

        }

        // New event to signal status changes
        public event EventHandler<StatusChangedEventArgs> StatusChanged;

        // Method to raise the StatusChanged event
        protected virtual void OnStatusChanged(string status)
        {
            StatusChanged?.Invoke(this, new StatusChangedEventArgs(status));
        }


        /// Starts the client by initializing connection parameters and calling the base class's `Start` method.
        public override void Start()
        {
            // Call the base class's `Start` method to initiate the WebSocket connection.
            base.Start();

            // Reset the Profit & Loss, P&L, to zero.
            Pnl = 0;
            // Clear any pending request or contract IDs.
            waitingClosedIds.Clear();
            waitingContradctIds.Clear();
            // Reset the trade ID counter.
            tradeIdCounter = 0;
            // Clear the current trade model.
            model = null;
            // Reset the timestamps for loss trades and any trades.
            LossTradeTime = default(DateTime);
            AnyTradeTime = default(DateTime);
        }

        /// <summary>
        /// Handles the event when the WebSocket connection is successfully opened.
        /// Sends an authorization request to the server using the provided API token.
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        /// </summary>
        protected override void SockOnOpened(object sender, EventArgs e)
        {
            // If there are any pending request IDs, log an error and clear them, as they likely won't be fulfilled.
            if (waitingClosedIds.Count > 0)
            {
                logger.Warn("<=> Unable to recover {0} pending transactions after reconnection: {1}. These transactions will be discarded.",
                                waitingClosedIds.Count, string.Join(", ", waitingClosedIds)); waitingClosedIds.Clear();
                waitingContradctIds.Clear();
                model = null;
            }

            // Log a message indicating that the connection to the specified Application ID is established.
            logger.Info("<=> Link to Client ID {0} established. Authorizing now...", Credentials.Name);

            // Send an authorization request to the server using the API token from the credentials.
            Send(new AuthorizeMessage() { Authorize = Credentials.Token });

            StateChanged?.Raise(this, new StateChangedArgs(IsOnline, Credentials));
        }

        /// <summary>
        /// Handles incoming messages from the Deriv API WebSocket server. 
        /// Processes different message types, including authorization responses, trade confirmations, balance updates, and transaction notifications.
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Message received event arguments containing the message data.</param>
        /// </summary>
        protected override void SockOnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            // Deserialize the received JSON message into a JObject for easier parsing.
            var jMessage = JsonConvert.DeserializeObject<JObject>(e.Message);

            // Process different message types based on the "msg_type" field.
            switch (jMessage["msg_type"].Value<string>())
            {
                case "profit_table":
                    // This message type provides a historical profit table for completed trades.

                    // If there's no current trade model, ignore this message.
                    if (model == null)
                    {
                        return;
                    }

                    // If the message contains an error, log the error and reset the trade model and pending IDs.
                    if (jMessage.ContainsKey("error"))
                    {
                        logger.Warn("<=> Unable to retrieve profit table data. {0} pending transactions (IDs: {1}) might be affected and will be discarded.",
                                            waitingClosedIds.Count, string.Join(", ", waitingClosedIds)); waitingClosedIds.Clear();
                        waitingContradctIds.Clear();
                        model = null;
                        return;
                    }

                    // Deserialize the message into a ProfitTableResponse object.
                    var profits = jMessage.ToObject<ProfitTableResponse>();

                    // If the profit table or its transactions are null, ignore this message.
                    if (profits.profit_table?.transactions == null)
                    {
                        return;
                    }

                    // Process each transaction in the profit table.
                    foreach (var historyTransaction in profits.profit_table.transactions)
                    {
                        // Call `ProcessTransaction` to update the client's P&L based on historical transactions.
                        ProcessTransaction(historyTransaction.sell_price, true, historyTransaction.contract_id);
                    }
                    break;

                case "buy":
                    // This message type confirms the execution of a "buy" request.

                    // Extract the request ID from the message.
                    var reqId = jMessage["req_id"].Value<int>();

                    // If the request ID is found in the waitingClosedIds set, remove it and process the trade confirmation.
                    if (waitingClosedIds.Remove(reqId))
                    {
                        // If the message contains an error, log the error, mark the trade as failed, and attempt to close it.
                        if (jMessage.ContainsKey("error"))
                        {
                            logger.Error("<=> Buy error {0}", e.Message);
                            model.IsFailed = true;
                            TryCloseTrade();
                            return;
                        }

                        // Extract the contract ID, buy price, and payout from the message.
                        var contractId = jMessage["buy"]["contract_id"].Value<long>();
                        var buyPrice = jMessage["buy"]["buy_price"].Value<decimal>();
                        var payout = jMessage["buy"]["payout"].Value<decimal>();

                        // Calculate the estimated profit for the trade (potential payout minus buy price).
                        var estimatedWin = payout - 2 * buyPrice;

                        // Add the estimated profit to the Payouts list in the trade model.
                        model.Payouts.Add(estimatedWin);

                        // Add the contract ID to the waitingContradctIds set, indicating an open trade.
                        waitingContradctIds.Add(contractId);

                        // Call `ProcessTransaction` to update the client's P&L with the initial buy price.
                        ProcessTransaction(-buyPrice, false, contractId);
                    }

                    // Correctly access contract ID from jMessage
                    currentContractId = jMessage["buy"]["contract_id"].Value<long>();
                    break;

                case "authorize":
                    // This message type is received in response to an authorization request.

                    // If the message contains an error, log the error, raise the AuthFailed event, and return.
                    if (jMessage.ContainsKey("error"))
                    {
                        // Extract the error code and message from the server response (if available).
                        string errorCode = jMessage["error"]["code"]?.Value<string>() ?? "Unknown";
                        string errorMessage = jMessage["error"]["message"]?.Value<string>() ?? "No message provided.";

                        // Log a more informative error message including the error code and message from the server.
                        logger.Error("<=> Authorization failed for Client ID: {0}. Error Code: {1}, Message: {2}",
                                     Credentials.Name, errorCode, errorMessage);

                        AuthFailed.Raise(this, EventArgs.Empty);
                        OnStatusChanged("Invalid");

                        return;
                    }

                    logger.Info("<=> Authentication successful for Client ID : {0}.", Credentials.Name);

                    // Deserialize the authorization information from the response.
                    authInfo = jMessage.ToObject<AuthResponse>().authorize;

                    // Update the client's balance with the authorized account balance.
                    Balance = authInfo.balance;

                    // Raise the BalanceChanged event to notify listeners about the balance update.
                    BalanceChanged?.Raise(this, EventArgs.Empty);

                    // Send requests to get the current balance, recent transactions, and the profit table.
                    Send(new BalanceRequest());
                    Send(new TransactionsRequest());
                    Send(new ProfitTableRequest());

                    // Set the online status to true, indicating a successful connection and authorization.
                    IsOnline = true;
                    break;
                case "balance":
                    // This message type provides the current account balance.

                    // If the message contains an error, ignore it.
                    if (jMessage.ContainsKey("error"))
                    {
                        return;
                    }

                    // Deserialize the balance information from the message.
                    var balance = jMessage.ToObject<BalanceMessage>();

                    // Update the client's balance.
                    Balance = balance.balance.balance;
                    // Raise the BalanceChanged event to notify listeners.
                    BalanceChanged?.Raise(this, EventArgs.Empty);
                    break;

                case "transaction":
                    // This message type notifies about a transaction on the account, including trade outcomes.

                    // If the message contains an error, check for a specific error code ("WrongResponse") and attempt reconnection.
                    if (jMessage.ContainsKey("error"))
                    {
                        // Check for the "WrongResponse" error code, which might indicate a need for reconnection.
                        if (jMessage["error"]["code"].Value<string>() == "WrongResponse" && model != null)
                        {
                            logger.Error("<=> Reconnecting to recover current trade...");
                            // Attempt reconnection by calling StartInternal.
                            StartInternal();
                        }
                        // If the error is not "WrongResponse", ignore the message.
                        return;
                    }

                    // Deserialize the transaction information from the message.
                    var trans = jMessage.ToObject<TransactionMessage>();

                    // If the transaction action is empty, ignore the message.
                    if (string.IsNullOrEmpty(trans.transaction.action))
                    {
                        return;
                    }

                    // Update the client's balance with the transaction balance.
                    Balance = trans.transaction.balance;

                    // If there's no current trade model, ignore this transaction.
                    if (model == null)
                    {
                        return;
                    }

                    // Only process "sell" transactions, which represent the closing of a trade.
                    if (trans.transaction.action != "sell")
                    {
                        return;
                    }

                    //// Log the relevant trade information
                    //logger.Info("<=> Deal Active....... => Client-ID-> {0} :: Contract ID-> {1} :: Symbol: {2}              :: Acc Balance-> {3:C} :: Stake placed-> {4:C}.",
                    //    Credentials.AppId, // App ID
                    //    trans.transaction.contract_id, // Contract ID
                    //    trans.transaction.symbol, // Symbol
                    //    trans.transaction.balance, // Account Balance
                    //    TradingParameters.DynamicStake); // Dynamic Stake

                    // Call `ProcessTransaction` to update the client's P&L and trade model based on the transaction details.
                    ProcessTransaction(
                        trans.transaction.amount,
                        trans.transaction.action == "sell",
                        trans.transaction.contract_id);

                    // Store contract ID and transaction time
                    currentContractId = trans.transaction.contract_id;
                    currentTransactionTime = trans.transaction.transaction_time;
                    break;
                default:
                    // Ignore any other message types.
                    break;
            }
        }

        // New EventArgs class for the StatusChanged event
        public class StatusChangedEventArgs : EventArgs
        {
            public string Status { get; }

            public StatusChangedEventArgs(string status)
            {
                Status = status;
            }
        }

        /// <summary>
        /// Processes a transaction, updating the client's P&L and trade model based on the transaction details.
        /// This method is called for both historical transactions from the profit table and real-time transactions received via the WebSocket.
        /// <param name="payout">The payout amount for the transaction (positive for profit, negative for loss).</param>
        /// <param name="isSell">A flag indicating whether the transaction is a "sell" transaction (closing a trade).</param>
        /// <param name="contractId">The ID of the contract associated with the transaction.</param>
        /// </summary>
        private void ProcessTransaction(decimal payout, bool isSell, long contractId)
        {
            // If the contract ID is not found in the waitingContradctIds set, ignore the transaction.
            if (!waitingContradctIds.Contains(contractId))
            {
                return;
            }

            // Update the client's P&L with the transaction payout.
            Pnl += payout;
            // Update the trade model's profit with the transaction payout.
            model.Profit += payout;

            // If the transaction is a "sell" transaction, remove the contract ID from the waitingContradctIds set.
            if (isSell)
            {
                waitingContradctIds.Remove(contractId);
            }

            // Raise the BalanceChanged event to notify listeners about the updated balance.
            BalanceChanged?.Raise(this, EventArgs.Empty);

            // Attempt to close the trade if all pending requests and contract IDs have been processed.
            TryCloseTrade();
        }

        /// <summary>
        /// Attempts to close the current trade if all pending requests and contract IDs have been processed.
        /// This method marks the trade as closed in the trade model, raises the `TradeUpdate` event, and resets the trade model.
        /// </summary>
        private void TryCloseTrade()
        {
            // Check if both the waitingClosedIds and waitingContradctIds sets are empty.
            if (waitingClosedIds.Count == 0 && waitingContradctIds.Count == 0)
            {
                // If both sets are empty, it means all related requests and contract IDs have been processed.

                // Mark the current trade as closed in the trade model.
                model.IsClosed = true;
                // Raise the TradeUpdate event to notify listeners about the closed trade and its outcome.
                RaiseTradeUpdate();
                // Reset the trade model to null, indicating that there's no active trade in progress.
                model = null;
            }
        }

        /// <summary>
        /// Raises the `TradeUpdate` event, providing details about the trade outcome and updating trading parameters.
        /// This method is called whenever a trade is closed or updated.
        /// </summary>
        private void RaiseTradeUpdate()
        {
            // If the trade is closed and not marked as failed...
            if (model.IsClosed && !model.IsFailed)
            {
                if (int.TryParse(Credentials.AppId, out int appId))
                {
                    TradingParameters.Process(model.Profit, model.Payouts.Max(), appId, currentContractId, currentTransactionTime);
                }
                else
                {
                    logger.Error("Failed to parse AppId to integer.");
                    // Handle the error appropriately (e.g., display an error message, stop trading)
                }
            }

            // If the trade is closed and resulted in a loss, or if the trade failed, update the LossTradeTime.
            if (model.IsClosed && model.Profit < 0 || model.IsFailed)
            {
                LossTradeTime = DateTime.Now;
            }

            // If the trade is closed (regardless of outcome), update the AnyTradeTime.
            if (model.IsClosed)
            {
                AnyTradeTime = DateTime.Now;
            }

            // Raise the TradeChanged event to notify listeners about the trade update.
            TradeChanged?.Raise(this, new TradeEventArgs(model, this));
            StateChanged?.Raise(this, new StateChangedArgs(IsOnline, Credentials));
        }

        private void InitializeTradingParameters(TradingParameters parameters)
        {
            if (TradingParameters != null)
            {
                // Unsubscribe from the old parameters' events
                TradingParameters.TakeProfitReached -= OnTakeProfitReached;
            }

            TradingParameters = parameters;
            if (TradingParameters != null)
            {
                // Subscribe to the new parameters' events
                TradingParameters.TakeProfitReached += OnTakeProfitReached;
            }
        }

        private void OnTakeProfitReached(object sender, decimal totalProfit)
        {
            // Stop trading by clearing the model and waiting IDs
            model = null;
            waitingClosedIds.Clear();
            waitingContradctIds.Clear();
            
            // Log the achievement of take profit target
            logger.Info($"<=> Take profit target reached! Total Profit: {totalProfit:C}");
            
            // Notify the UI that trading has stopped
            StateChanged?.Raise(this, new StateChangedArgs(IsOnline, Credentials));
        }
    }

    /// Represents an authorization message to be sent to the Deriv API and authenticate the client using an API token.
    public class AuthorizeMessage
    {
        // The API token used for authentication.
        [JsonProperty("authorize")]
        public string Authorize { get; set; }
    }

    /// <summary>
    /// Represents a price proposal for a binary options trade. 
    /// This proposal outlines the parameters of the trade, such as symbol, duration, barrier, and contract type.
    /// </summary>
    public class PriceProposal
    {
        // The amount to be staked on the trade.
        public decimal amount { get; set; } = 1;

        // The price barrier offset for the trade.
        public string barrier { get; set; }

        // The basis for the trade payout. Typically set to "stake" for binary options.
        public string basis { get; set; } = "stake";

        // The type of contract ("CALL" for Buy/Higher, "PUT" for Sell/Lower).
        public string contract_type { get; set; }

        // The currency in which the trade will be executed (e.g., "USD").
        public string currency { get; set; }

        // The duration of the trade.
        public int duration { get; set; }

        // The unit of time for the trade duration (e.g., "t", "s", "m", "h", "d" for Ticks, Seconds, Minutes, Hours, Days).
        public string duration_unit { get; set; }

        // The trading symbol for the trade (e.g., "1HZ50V").
        public string symbol { get; set; }
    }

    /// Represents an individual account within the authorized user's account list.
    public class AccountList
    {
        // The currency of the account (e.g., "USD, EUR or even crypto").
        public string currency { get; set; }

        // A flag indicating whether the account is disabled.
        public int is_disabled { get; set; }

        // A flag indicating whether the account is a virtual (demo) account.
        public int is_virtual { get; set; }

        // The name of the landing company associated with the account.
        public string landing_company_name { get; set; }

        // The login ID of the account.
        public string loginid { get; set; }
    }

    /// <summary>
    /// Contains detailed information about the authorized user, including their accounts, balance, and permissions.
    /// This information is received in response to a successful authorization request.
    /// </summary>
    public class Authorize
    {
        // A list of accounts associated with the authorized user.
        public List<AccountList> account_list { get; set; }

        // The account balance of the authorized user.
        public decimal balance { get; set; }

        // The country of the authorized user.
        public string country { get; set; }

        // The currency of the authorized user's main account.
        public string currency { get; set; }

        // The email address of the authorized user.
        public string email { get; set; }

        // The full name of the authorized user.
        public string fullname { get; set; }

        // A flag indicating whether the authorized user's main account is a virtual account.
        public int is_virtual { get; set; }

        // The full name of the landing company associated with the authorized user.
        public string landing_company_fullname { get; set; }

        // The name of the landing company associated with the authorized user.
        public string landing_company_name { get; set; }

        // Local currencies information (if applicable).
        public object local_currencies { get; set; }

        // The login ID of the authorized user.
        public string loginid { get; set; }

        // A list of permissions granted to the authorized user.
        public List<string> scopes { get; set; }

        // A list of landing companies that the authorized user can upgrade to.
        public List<string> upgradeable_landing_companies { get; set; }

        // The user ID of the authorized user.
        public int user_id { get; set; }
    }

    /// Represents the server's response to an authorization request.
    public class AuthResponse
    {
        // The authorization information for the authenticated user.
        public Authorize authorize { get; set; }

        // An echo of the original request (if applicable).
        public object echo_req { get; set; }

        // The message type, indicating the purpose of the message. For authorization responses, this is "authorize".
        public string msg_type { get; set; }
    }

    /// Represents a request to buy a contract (place a trade).
    public class BuyContractRequest
    {
        // A flag indicating that this is a buy request.
        public string buy { get; set; } = "1";

        /// The price proposal parameters for the trade.
        public PriceProposal parameters { get; set; }

        // The price (stake) to be used for the contract.
        public decimal price { get; set; }

        // A unique request ID for this buy request. Used to match the request with the server's response.
        public int req_id { get; set; } = Environment.TickCount;
    }

    /// Represents a request to get the current account balance.
    public class BalanceRequest
    {
        // The account for which the balance is requested. Typically set to "current" for the main account.
        public string account { get; set; } = "current";

        // A flag indicating that this is a balance request.
        public decimal balance { get; set; } = 1;
    }

    /// Represents a request to get the profit table for completed trades.
    public class ProfitTableRequest
    {
        // A flag indicating that this is a profit table request.
        public int profit_table { get; set; } = 1;

        // A flag indicating whether to include descriptions in the profit table.
        public int description { get; set; } = 0;
    }

    /// Represents the account balance information.
    public class Balance
    {
        /// The current account balance.
        public decimal balance { get; set; }

        /// The currency of the balance.
        public string currency { get; set; }

        /// The login ID of the account.
        public string loginid { get; set; }
    }

    /// Represents a message from the server containing the account balance information.
    public class BalanceMessage
    {
        /// The balance information for the account.
        public Balance balance { get; set; }

        /// The message type, indicating the purpose of the message. For balance updates, this is "balance".
        public string msg_type { get; set; }
    }

    /// Represents a request to subscribe to transaction updates (including trade outcomes).
    public class TransactionsRequest
    {
        /// A flag indicating that this is a transaction request.
        public int transaction { get; set; } = 1;

        /// A flag indicating that the client wants to subscribe to transaction updates.
        public int subscribe { get; set; } = 1;
    }

    /// Represents a transaction on the account, including trade openings, closings, and other balance adjustments.
    public class Transaction
    {
        // The type of transaction (e.g., "buy", "sell", "deposit", "withdrawal").
        public string action { get; set; }

        // The amount of the transaction.
        public decimal amount { get; set; }

        // The account balance after the transaction.
        public decimal balance { get; set; }

        // The price barrier offset for the transaction (if applicable).
        public string barrier { get; set; }

        // The ID of the contract associated with the transaction (if applicable).
        public long contract_id { get; set; }

        // The currency of the transaction (e.g., "USD").
        public string currency { get; set; }

        // The expiry date of the contract (if applicable).
        public int date_expiry { get; set; }

        // The display name of the transaction (if applicable).
        public string display_name { get; set; }

        // The transaction ID.
        public string id { get; set; }

        // A long code describing the transaction (if applicable).
        public string longcode { get; set; }

        // The trading symbol for the transaction (if applicable).
        public string symbol { get; set; }

        // The server-generated transaction ID.
        public long transaction_id { get; set; }

        // The timestamp of the transaction (in Unix time).
        public int transaction_time { get; set; }
    }

    /// Represents a message from the server containing transaction information.
    public class TransactionMessage
    {
        // The message type, indicating the purpose of the message. For transaction notifications, this is "transaction".
        public string msg_type { get; set; }

        // The transaction details.
        public Transaction transaction { get; set; }
    }

    /// <summary>
    /// Represents a model for tracking the state and outcome of a trade. 
    /// This model is used to manage individual trades and their associated data.
    /// </summary>
    public class TradeModel
    {
        // The API token associated with the account that placed the trade.
        public string Token { get; set; }

        // The unique ID of the trade.
        public int Id { get; set; }

        // The current state of the trade (e.g., Buying, Purchased).
        public TradeState TradeState => IsClosed ? TradeState.Purchased : TradeState.Buying;

        // The result of the trade (e.g., In-The-Money (ITM), Out-of-The-Money (OTM)).
        public TradeResult TradeResult => Profit > 0 ? TradeResult.ITM : TradeResult.OTM;

        // The profit or loss of the trade.
        public decimal Profit { get; set; }

        // The stake amount for the trade.
        public decimal Stake { get; set; }

        // A flag indicating whether the trade is closed (completed).
        public bool IsClosed { get; set; }

        // A flag indicating whether the trade failed (e.g., due to an error).
        public bool IsFailed { get; set; }

        // A list of potential payouts for the trade (used for calculating estimated profits).
        public List<decimal> Payouts = new List<decimal>();

        /// <summary>
        /// Returns a string representation of the trade model, including all relevant details.
        /// <returns>A string representation of the trade model.</returns>
        /// </summary>
        public override string ToString()
        {
            return $"{nameof(Payouts)}: {string.Join(";", Payouts)}, {nameof(Token)}: {Token}, {nameof(Id)}: {Id}, {nameof(TradeState)}: {TradeState}, {nameof(TradeResult)}: {TradeResult}, {nameof(Profit)}: {Profit}, {nameof(Stake)}: {Stake}, {nameof(IsClosed)}: {IsClosed}, {nameof(IsFailed)}: {IsFailed}";
        }
    }

    /// Represents the possible states of a trade.
    public enum TradeState
    {
        // The trade is currently being placed (waiting for confirmation).
        Buying = 0,

        // The trade has been purchased (confirmed by the server).
        Purchased = 1
    }

    /// Represents the possible results of a trade.
    public enum TradeResult
    {
        /// The trade was In-The-Money (ITM) - a winning trade.
        ITM = 0,

        /// The trade was Out-of-The-Money (OTM) - a losing trade.
        OTM = 1
    }

    /// Event arguments for the TradeChanged event, containing the updated trade model.
    public class TradeEventArgs : EventArgs
    {
        // The updated trade model.
        public TradeModel Model { get; }
        public AuthClient Client { get; }

        /// <summary>
        /// Constructor for `TradeEventArgs`.
        /// <param name="model">The updated trade model.</param>
        /// </summary>
        public TradeEventArgs(TradeModel model, AuthClient client)
        {
            Model = model;
            Client = client;
        }
    }

    /// Represents the server's response to a profit table request.
    public class ProfitTableResponse
    {
        // The message type, indicating the purpose of the message. For profit table responses, this is "profit_table".
        public string msg_type { get; set; }

        // The profit table containing information about completed trades.
        public ProfitTable profit_table { get; set; }
    }

    /// Represents the profit table containing details about completed trades.
    public class ProfitTable
    {
        // The number of trades in the profit table.
        public int count { get; set; }

        // A list of historical transactions representing completed trades.
        public List<HistoryTransaction> transactions { get; set; }
    }

    /// Represents a historical transaction, typically retrieved from the profit table.
    public class HistoryTransaction
    {
        // The application ID associated with the trade.
        public int app_id { get; set; }

        // The buy price of the contract.
        public decimal buy_price { get; set; }

        // The contract ID for the trade.
        public long contract_id { get; set; }

        // The payout amount for the trade.
        public decimal payout { get; set; }

        // The timestamp of the trade purchase (in Unix time).
        public long purchase_time { get; set; }

        // The sell price of the contract.
        public decimal sell_price { get; set; }

        // The timestamp of the trade sell (in Unix time).
        public long sell_time { get; set; }

        // The server-generated transaction ID for the trade.
        public long transaction_id { get; set; }
    }
}