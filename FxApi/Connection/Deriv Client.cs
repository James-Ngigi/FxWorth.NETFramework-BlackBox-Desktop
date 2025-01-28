using System;
using System.Security.Authentication;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using SuperSocket.ClientEngine;
using WebSocket4Net;

namespace FxApi
{
    /// <summary>
    /// The `BinaryClientBase` class provides the foundational framework for interacting with the Deriv API via WebSockets.
    /// It handles the WebSocket connection lifecycle (opening, closing, sending/receiving data, and reconnection), 
    /// authentication, and serialization/deserialization of JSON messages. This base class is designed for extensibility,
    /// allowing derived classes like `AuthClient`, `MarketDataClient`, and `PingClient` to implement specialized API interactions.
    /// </summary>
    public class BinaryClientBase
    {
        /// <summary>
        /// Logger for recording events and errors related to the WebSocket connection and API interaction.
        /// </summary>
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// <summary>
        /// Flag indicating whether the client has been disposed.  Used to prevent further operations on a disposed object.
        /// </summary>
        protected bool isDisposed;

        /// <summary>
        /// Stores the credentials (Application ID and API token) required for authenticating with the Deriv API.
        /// </summary>
        protected readonly Credentials Credentials;

        /// <summary>
        /// The WebSocket endpoint URL for the Deriv API, including the application ID.
        /// </summary>
        private readonly string websocketPath;

        /// <summary>
        /// The WebSocket instance used for communication with the Deriv API.
        /// </summary>
        private WebSocket sock;

        /// <summary>
        /// Semaphore to synchronize reconnection attempts, ensuring only one attempt is active at a time.
        /// </summary>
        private readonly SemaphoreSlim reconnectLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Counter for tracking the number of reconnection attempts.
        /// </summary>
        private int reconnectAttempts = 0;

        /// <summary>
        /// Maximum number of reconnection attempts before stopping.
        /// </summary>
        private readonly int maxReconnectAttempts = 20;

        /// <summary>
        /// Timer to monitor the WebSocket connection attempt and trigger a timeout if the connection takes too long.
        /// </summary>
        private Timer connectionTimeoutTimer;

        /// <summary>
        /// Maximum delay between reconnection attempts (using linear backoff).
        /// </summary>
        private readonly TimeSpan maxReconnectDelay = TimeSpan.FromSeconds(180);

        /// <summary>
        /// Increment for each reconnection attempt delay (linear backoff).
        /// </summary>
        private readonly TimeSpan reconnectDelayIncrement = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Cancellation token source for managing reconnection attempt cancellations.
        /// </summary>
        private CancellationTokenSource reconnectCancellationTokenSource;

        /// <summary>
        /// Lock object to synchronize access to the WebSocket during send operations, ensuring thread safety.
        /// </summary>
        private object socketSendLock = new object();

        /// <summary>
        /// Flag indicating whether the WebSocket connection is currently open and active.
        /// </summary>
        private bool isOnline = false;

        /// <summary>
        /// Constructor for `BinaryClientBase`. Initializes the API credentials and WebSocket path.
        /// <param name="credentials">API credentials (Application ID and Token).</param>
        /// </summary>
        public BinaryClientBase(Credentials credentials)
        {
            this.Credentials = credentials;
            websocketPath = "wss://ws.binaryws.com/websockets/v3?l=EN&app_id=" + credentials.AppId;
        }

        /// <summary>
        /// Property indicating the online/offline status of the WebSocket connection. 
        /// Raises the `StateChanged` event when the status changes.
        /// </summary>
        public bool IsOnline
        {
            get => isOnline;
            set
            {
                if (isOnline == value)
                {
                    return;
                }

                isOnline = value;
                StateChanged?.Raise(this, new StateChangedArgs(isOnline, Credentials));
            }
        }

        /// <summary>
        /// Event raised when the connection state changes (e.g., connected, disconnected).
        /// </summary>
        public EventHandler<StateChangedArgs> StateChanged;

        /// <summary>
        /// Sends a data object as a JSON message over the WebSocket.
        /// </summary>
        /// <param name="obj">The object to be sent. It will be serialized to JSON.</param>
        protected void Send(object obj)
        {
            SendJsonSafe(sock, obj);
        }

        /// <summary>
        /// Sends a JSON-serializable object over the WebSocket with error handling and reconnection logic.
        /// <param name="sock">The WebSocket instance.</param>
        /// <param name="json">The object to be serialized and sent.</param>
        /// </summary>
        protected async void SendJsonSafe(WebSocket sock, object json)
        {
            // Check if the WebSocket is in a valid state to send data.
            if (sock == null || sock.State != WebSocketState.Open)
            {
                logger.Warn("<=> WebSocket is not open. Cannot send data.");
                return; // Exit the method if the WebSocket is not open.
            }

            try
            {
                // Acquire the reconnection lock to prevent concurrent reconnection attempts.
                await reconnectLock.WaitAsync();

                // Lock to ensure thread-safe access to the WebSocket.
                lock (socketSendLock)
                {
                    var str = JsonConvert.SerializeObject(json);
                    logger.Trace(str);
                    sock.Send(str);
                }
            }

            catch (Exception ex)
            {
                if (!isDisposed) // Check if the client has been disposed.
                {
                    logger.Error(ex, "<=> Error sending data. Attempting reconnection...");
                    AttemptReconnectWithBackoff();
                }
                {
                    logger.Error(ex, "<=> Error sending data. Attempting reconnection...");
                    AttemptReconnectWithBackoff();
                }
            }

            finally
            {
                reconnectLock.Release();
            }
        }

        /// <summary>
        /// Sends a JArray (JSON array) over the WebSocket with error handling and reconnection logic.
        /// <param name="sock">The WebSocket instance.</param>
        /// <param name="json">The JArray to be sent.</param>
        /// </summary>
        protected async void SendJsonSafe(WebSocket sock, JArray json)
        {
            if (sock == null || sock.State != WebSocketState.Open)
            {
                logger.Warn("<=> WebSocket is not open. Cannot send data.");
                return;
            }

            try
            {
                await reconnectLock.WaitAsync();

                lock (socketSendLock)
                {
                    var str = JsonConvert.SerializeObject(json);
                    logger.Debug(str);
                    sock.Send(str);
                }
            }
            catch (Exception ex)
            {
                if (!isDisposed)
                {
                    logger.Error(ex, "<=> Error sending data. Attempting reconnection...");
                    AttemptReconnectWithBackoff();
                }
            }
            finally
            {
                reconnectLock.Release();
            }
        }

        /// <summary>
        /// Starts the WebSocket connection process.
        /// </summary>
        public virtual void Start()
        {
            isDisposed = false;
            StartInternal();
        }

        /// <summary>
        /// Stops the WebSocket connection and releases resources.
        /// </summary>
        public void Stop()
        {
            IsOnline = false;
            isDisposed = true;
            StopInternal();
        }

        /// <summary>
        /// Initializes and opens the WebSocket connection. Sets up event handlers for connection events.
        /// </summary>
        protected void StartInternal()
        {
            // Stop any existing connection and cancel pending reconnection attempts.
            StopInternal();
            reconnectCancellationTokenSource?.Cancel(); // Cancel any ongoing reconnection attempts.

            try // Attempt to create a new WebSocket instance.
            {
                // Create a new WebSocket instance with specified parameters.
                sock = new WebSocket(websocketPath, sslProtocols: SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls)
                {
                    NoDelay = true // Disable Nagle's algorithm for better latency.
                };

                // Attach event handlers for WebSocket events.
                sock.Opened += SockOnOpened;
                sock.Error += SockOnError;
                sock.Closed += SockOnClosed;
                sock.MessageReceived += SockOnMessageReceived;
                sock.DataReceived += SockOnDataReceived;

                // Start the connection timeout timer.
                connectionTimeoutTimer = new Timer(OnConnectionTimeout, null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);

                // Attempt to open the WebSocket connection.
                sock.Open();
            }

            catch (Exception ex)
            {
                logger.Error(ex, "<=> Error creating WebSocket instance.");
                AttemptReconnectWithBackoff();
            }
        }

        /// <summary>
        /// Event handler for connection timeout. Attempts reconnection if triggered.
        /// <param name="state">The state object passed to the timer (not used in this case).</param>
        /// </summary>
        private void OnConnectionTimeout(object state)
        {
            // Check if the WebSocket is still connecting after the timeout period.
            if (sock != null && sock.State == WebSocketState.Connecting)
            {
                logger.Error("<=> WebSocket connection timed out.");
                sock.Close(); // Close the timed-out connection.
                AttemptReconnectWithBackoff();
            }
        }

        /// <summary>
        /// Event handler for receiving raw binary data.  Should be overridden in derived classes if needed.
        /// This data is used for specialized binary protocols such as the Deriv Tick Stream.
        /// </summary>
        protected virtual void SockOnDataReceived(object sender, DataReceivedEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Event handler for receiving text messages. Should be overridden in derived classes.
        /// </summary>
        protected virtual void SockOnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Event handler triggered when the WebSocket connection is closed. Handles reconnection logic.
        /// </summary>
        protected virtual void SockOnClosed(object sender, EventArgs e)
        {
            logger.Info("<=> Server - Client {0} link terminated. Attempting Reconnection", Credentials.Name);
            IsOnline = false;


            if (!isDisposed)
            {
                var closedEventArgs = e as ClosedEventArgs;

                if (closedEventArgs != null)
                {

                    if (closedEventArgs.Code >= 1003 && closedEventArgs.Code <= 1015)
                    {
                        logger.Error("<=> Websocket closure error detected. Stopping the client. Error code: " + closedEventArgs.Code);
                        isDisposed = true;
                        StopInternal(); // Stop the client gracefully
                        return;
                    }

                    else if (closedEventArgs.Code == 1002 || closedEventArgs.Code != 1000) // Attempt reconnection only for protocol error (1002) or non-normal closures
                    {
                        AttemptReconnectWithBackoff();
                    }

                }

                else // Null closedEventArgs, probably due to a network issue - attempt reconnect.
                {
                    AttemptReconnectWithBackoff();
                }
            }

        }

        /// <summary>
        /// Attempts reconnection to the WebSocket with exponential backoff algorithm.
        /// </summary>
        private async void AttemptReconnectWithBackoff()
        {
            // Cancel any previous reconnection attempt.
            reconnectCancellationTokenSource?.Cancel();
            reconnectCancellationTokenSource = new CancellationTokenSource();
            var token = reconnectCancellationTokenSource.Token;
            reconnectAttempts++;

            if (reconnectAttempts <= maxReconnectAttempts && !isDisposed) // check for isDisposed to prevent re-entry
            {
                // Exponential backoff with jitter
                int reconnectDelayMs = (int)(Math.Pow(2, reconnectAttempts - 1) * reconnectDelayIncrement.TotalMilliseconds);
                reconnectDelayMs = new Random().Next(reconnectDelayMs / 2, reconnectDelayMs * 3 / 2);

                // Cap the delay at the maximum.
                int maxReconnectDelayMs = (int)maxReconnectDelay.TotalMilliseconds;
                if (reconnectDelayMs > maxReconnectDelayMs)
                {
                    reconnectDelayMs = maxReconnectDelayMs;
                }

                logger.Info($"<=> Reconnection attempt {reconnectAttempts} of {maxReconnectAttempts}, waiting for {reconnectDelayMs / 1000.0} seconds...");

                try
                {
                    await Task.Delay(reconnectDelayMs, token);
                    if (!token.IsCancellationRequested && !isDisposed) // Extra check for isDisposed
                    {
                        StartInternal(); // Initiate the reconnection
                    }

                }

                catch (TaskCanceledException)
                {
                    logger.Info("<=> Reconnection attempt cancelled.");
                }

            }

            else
            {
                logger.Error($"<=> Maximum reconnection attempts reached or client disposed ({reconnectAttempts}). Stopping further attempts.");
            }
        }

        /// <summary>
        /// Event handler for WebSocket errors. Logs the error.
        /// </summary>
        protected virtual void SockOnError(object sender, ErrorEventArgs e)
        {
            logger.Error(e.Exception, "<=> {0} Data error.", Credentials.Name);
            StateChanged?.Raise(this, new StateChangedArgs(true, Credentials));
        }

        /// <summary>
        /// Event handler triggered when the WebSocket connection is successfully opened.
        /// </summary>
        protected virtual void SockOnOpened(object sender, EventArgs e)
        {
            // Reset reconnection attempts and cancel any ongoing attempt.
            reconnectAttempts = 0;
            reconnectCancellationTokenSource?.Cancel();

            logger.Info("<=> Server - Client {0} Link established.", Credentials.Name);
            IsOnline = true;

            // Force ClientsStateChanged event after successful reconnection
            StateChanged?.Raise(this, new StateChangedArgs(true, Credentials));
        }

        /// <summary>
        /// Stops the WebSocket connection, detaches event handlers, and releases resources.
        /// </summary>
        protected void StopInternal()
        {
            // Dispose of the connection timeout timer.
            connectionTimeoutTimer?.Dispose();
            connectionTimeoutTimer = null;

            if (sock == null)
            {
                return;
            }

            // Cancel any pending reconnection attempts.
            reconnectCancellationTokenSource?.Cancel();

            // Detach event handlers to prevent memory leaks.
            sock.Opened -= SockOnOpened;
            sock.Error -= SockOnError;
            sock.Closed -= SockOnClosed;
            sock.MessageReceived -= SockOnMessageReceived;
            sock.DataReceived -= SockOnDataReceived;

            // Close the WebSocket connection if it's open or connecting.
            if (sock.State == WebSocketState.Open || sock.State == WebSocketState.Connecting)
            {
                try
                {
                    sock.Close();
                }

                catch (Exception ex)
                {
                    logger.Error(ex, "<=> Error closing WebSocket.");
                }
            }

            sock = null;
        }

        /// <summary>
        /// Utility method for repeatedly executing an action at a specified interval using a timer.
        /// Used for tasks like periodic ping requests.
        /// <param name="token">Cancellation token to stop the repeating action.</param>
        /// <param name="action">The action to be executed.</param>
        /// <param name="timeoutMs">Interval between executions in milliseconds.</param>
        /// </summary>
        public static void RepeatAction(CancellationToken token, Action action, int timeoutMs)
        {
            if (token.IsCancellationRequested)
            {
                logger.Info("<=> Action repeat stopped: {0}", action.Method.Name);
                return;
            }

            try
            {
                action();
            }

            catch (Exception ex)
            {
                logger.Error(ex, action.Method.Name);
            }

            // Schedule the action to repeat after the specified delay.
            Task.Delay(timeoutMs, token).ContinueWith(x =>
            {
                if (x.IsCanceled)
                {
                    logger.Info("<=> Action repeat stopped: {0}", action.Method.Name);
                    return;
                }

                RepeatAction(token, action, timeoutMs);
            }, token);
        }
    }

    /// <summary>
    /// Event arguments for connection state changes, providing the new state and credentials.
    /// </summary>
    public class StateChangedArgs : EventArgs
    {
        /// <summary>
        /// The new connection state (true for online, false for offline).
        /// </summary>
        public bool State { get; }

        /// <summary>
        /// The credentials associated with the connection.
        /// </summary>
        public Credentials Creds { get; }

        /// <summary>
        /// Constructor for `StateChangedArgs`.
        /// <param name="state">The new connection state.</param>
        /// <param name="creds">The associated credentials.</param>
        /// </summary>
        public StateChangedArgs(bool state, Credentials creds)
        {
            State = state;
            Creds = creds;
        }
    }
}