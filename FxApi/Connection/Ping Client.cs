using System;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NLog;
using WebSocket4Net;
using System.Threading.Tasks;

namespace FxApi
{
    /// <summary>
    /// The `PingClient` class is responsible for measuring the latency (round-trip time) to the Deriv API server.
    /// It periodically sends ping messages and calculates the time it takes to receive a pong response.
    /// This latency information is used to monitor the network connection quality and potentially adjust trading behavior.
    /// </summary>

    public class PingClient : BinaryClientBase
    {
        /// The measured latency in milliseconds. A value of -1 indicates that no latency measurement is available yet.
        public int Latency { get; private set; } = -1;

        /// Logger for recording debug and error messages.
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();

        /// Timestamp (in milliseconds) of the last ping message sent.
        private int lastPingSendTime;

        /// Cancellation token source for the repeating ping task.
        CancellationTokenSource pingCancellationTokenSource = new CancellationTokenSource();

        /// Flag indicating whether a pong response has been received for the last ping message.
        private bool gotPong = true;

        /// <summary>
        /// Constructor for the `PingClient` class.
        /// <param name="credentials">API credentials required for connecting to the Deriv API.</param>
        /// </summary>
        public PingClient(Credentials credentials) : base(credentials)
        {
        }

        /// Event raised when the latency value changes.
        public EventHandler<EventArgs> PingChanged;

        /// <summary>
        /// Called when the WebSocket connection is successfully opened. 
        /// Starts the periodic ping task to measure latency.
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        /// </summary>
        protected override void SockOnOpened(object sender, EventArgs e)
        {
            base.SockOnOpened(sender, e);
            gotPong = true;
            pingCancellationTokenSource = new CancellationTokenSource();

            // Start the repeating ping task with an interval of 100 milliseconds.
            // This means a ping message will be sent every 100ms(Tenth of a second) to measure latency.
            RepeatAction(pingCancellationTokenSource.Token, PingChannel, 100);
        }

        /// <summary>
        /// Called when a message is received from the WebSocket server.
        /// Processes the pong response and calculates the latency.
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Message received event arguments.</param>
        /// </summary>
        protected override void SockOnMessageReceived(object sender, MessageReceivedEventArgs e)
        {
            // Deserialize the JSON message received from the server.
            var jMessage = JsonConvert.DeserializeObject<JObject>(e.Message);

            // Check if the message type is "ping" (pong response).
            switch (jMessage["msg_type"].Value<string>())
            {
                case "ping":
                    // Set the gotPong flag to indicate a pong response was received.
                    gotPong = true;
                    // Get the current timestamp (in milliseconds).
                    var pongTime = Environment.TickCount;

                    // Calculate the latency by subtracting the last ping send time from the current time.
                    Latency = pongTime - lastPingSendTime;
                    // Raise the PingChanged event to notify listeners that the latency value has changed.
                    PingChanged.Raise(this, EventArgs.Empty);

                    // Log the measured latency.
                    logger.Trace("{0} ping latency {1}", Credentials.Name, Latency);
                    break;
                default:
                    // Ignore other message types.
                    break;
            }
        }

        /// <summary>
        /// Sends a ping message to the server to measure latency.
        /// This method is called repeatedly by the `RepeatAction` method.
        /// </summary>
        private void PingChannel()
        {
            // Don't send a ping if the connection is not online.
            if (!IsOnline)
            {
                return;
            }

            // Don't send a ping if the client is disposed.
            if (isDisposed)
            {
                return;
            }

            // Don't send a new ping if the previous ping hasn't been answered yet (gotPong is false).
            if (!gotPong)
            {
                return;
            }

            // Set gotPong to false, indicating we're waiting for a pong response.
            gotPong = false;

            // Record the current timestamp as the last ping send time.
            lastPingSendTime = Environment.TickCount;

            // Send a new ping message to the server.
            Send(new PingMessage());
        }

        /// <summary>
        /// Called when the WebSocket connection is closed.
        /// Cancels the repeating ping task.
        /// <param name="sender">The object that raised the event.</param>
        /// <param name="e">Event arguments.</param>
        /// </summary>
        protected override void SockOnClosed(object sender, EventArgs e)
        {
            // Cancel the repeating ping task when the connection is closed.
            pingCancellationTokenSource.Cancel();

            base.SockOnClosed(sender, e);
        }
    }

    /// <summary>
    /// Represents a ping message to be sent to the server.
    /// </summary>
    public class PingMessage
    {
        /// <summary>
        /// The ping value, always set to 1 to indicate a ping request.
        /// </summary>
        [JsonProperty("ping")]
        public int Ping { get; set; } = 1;
    }
}