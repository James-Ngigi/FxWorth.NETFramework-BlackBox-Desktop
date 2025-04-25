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
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        protected bool isDisposed;
        protected readonly Credentials Credentials;
        private readonly string websocketPath;
        private WebSocket sock;
        private readonly SemaphoreSlim reconnectLock = new SemaphoreSlim(1, 1);
        private int reconnectAttempts = 0;
        private readonly int maxReconnectAttempts = 20;
        private Timer connectionTimeoutTimer;
        private readonly TimeSpan maxReconnectDelay = TimeSpan.FromSeconds(180);
        private readonly TimeSpan reconnectDelayIncrement = TimeSpan.FromSeconds(1);
        private CancellationTokenSource reconnectCancellationTokenSource;

        private object socketSendLock = new object();
        private bool isOnline = false;

        public BinaryClientBase(Credentials credentials)
        {
            this.Credentials = credentials;
            websocketPath = "wss://ws.binaryws.com/websockets/v3?l=EN&app_id=" + credentials.AppId;
        }

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

        public EventHandler<StateChangedArgs> StateChanged;

        protected void Send(object obj)
        {
            SendJsonSafe(sock, obj);
        }

        protected async void SendJsonSafe(WebSocket sock, object json)
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
                    logger.Trace(str);
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

        public virtual void Start()
        {
            isDisposed = false;
            StartInternal();
        }

        public void Stop()
        {
            IsOnline = false;
            isDisposed = true;
            StopInternal();
        }

        protected void StartInternal()
        {
            StopInternal();
            reconnectCancellationTokenSource?.Cancel(); 

            try
            {
                sock = new WebSocket(websocketPath, sslProtocols: SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls)
                {
                    NoDelay = true // Disable Nagle's algorithm for better latency.
                };

                sock.Opened += SockOnOpened;
                sock.Error += SockOnError;
                sock.Closed += SockOnClosed;
                sock.MessageReceived += SockOnMessageReceived;
                sock.DataReceived += SockOnDataReceived;

                connectionTimeoutTimer = new Timer(OnConnectionTimeout, null, TimeSpan.FromSeconds(10), Timeout.InfiniteTimeSpan);
                sock.Open();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "<=> Error creating WebSocket instance.");
                AttemptReconnectWithBackoff();
            }
        }

        private void OnConnectionTimeout(object state)
        {
            if (sock != null && sock.State == WebSocketState.Connecting)
            {
                logger.Error("<=> WebSocket connection timed out.");
                sock.Close();
                AttemptReconnectWithBackoff();
            }
        }

        protected virtual void SockOnDataReceived(object sender, DataReceivedEventArgs e)
        {
            throw new NotImplementedException();
        }

        protected virtual void SockOnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            throw new NotImplementedException();
        }

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
                        StopInternal();
                        return;
                    }

                    else if (closedEventArgs.Code == 1002 || closedEventArgs.Code != 1000)
                    {
                        AttemptReconnectWithBackoff();
                    }
                }

                else
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
            reconnectCancellationTokenSource?.Cancel();
            reconnectCancellationTokenSource = new CancellationTokenSource();
            var token = reconnectCancellationTokenSource.Token;
            reconnectAttempts++;

            if (reconnectAttempts <= maxReconnectAttempts && !isDisposed)
            {
                int reconnectDelayMs = (int)(Math.Pow(2, reconnectAttempts - 1) * reconnectDelayIncrement.TotalMilliseconds);
                reconnectDelayMs = new Random().Next(reconnectDelayMs / 2, reconnectDelayMs * 3 / 2);
                int maxReconnectDelayMs = (int)maxReconnectDelay.TotalMilliseconds;

                if (reconnectDelayMs > maxReconnectDelayMs)
                {
                    reconnectDelayMs = maxReconnectDelayMs;
                }

                logger.Info($"<=> Reconnection attempt {reconnectAttempts} of {maxReconnectAttempts}, waiting for {reconnectDelayMs / 1000.0} seconds...");

                try
                {
                    await Task.Delay(reconnectDelayMs, token);
                    if (!token.IsCancellationRequested && !isDisposed)
                    {
                        StartInternal();
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

        /// Event handler for WebSocket errors. Logs the error.
        protected virtual void SockOnError(object sender, ErrorEventArgs e)
        {
            logger.Error(e.Exception, "<=> {0} Data error.", Credentials.Name);
            StateChanged?.Raise(this, new StateChangedArgs(true, Credentials));
        }

        /// Event handler triggered when the WebSocket connection is successfully opened.
        protected virtual void SockOnOpened(object sender, EventArgs e)
        {
            reconnectAttempts = 0;
            reconnectCancellationTokenSource?.Cancel();

            logger.Info("<=> Server - Client {0} Link established.", Credentials.Name);
            IsOnline = true;

            StateChanged?.Raise(this, new StateChangedArgs(true, Credentials));
        }

        /// Stops the WebSocket connection, detaches event handlers, and releases resources.
        protected void StopInternal()
        {
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

    /// Event arguments for connection state changes, providing the new state and credentials.
    public class StateChangedArgs : EventArgs
    {
        public bool State { get; }

        public Credentials Creds { get; }

        public StateChangedArgs(bool state, Credentials creds)
        {
            State = state;
            Creds = creds;
        }
    }
}