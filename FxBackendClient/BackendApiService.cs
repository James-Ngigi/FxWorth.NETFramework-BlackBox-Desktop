using Microsoft.AspNet.SignalR.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

// Define a DTO for the login response (adjust properties based on actual backend response)
public class LoginResponseDto
{
    [JsonProperty("token")]
    public string Token { get; set; }
}


public class TradingQueueItemDto
{
    public Guid Id { get; set; } 
    public string ApiTokenValue { get; set; }
    public string Name { get; set; }
    public decimal ProfitTargetAmount { get; set; }
}


namespace FxBackendClient
{
    public class BackendApiService : IDisposable
    {
        private readonly HttpClient _restClient;
        private HubConnection _signalRConnection;
        private string _operatorJwt;
        private readonly string _backendBaseUrl; // e.g., "http://localhost:8080" or "https://fxworth-api-backend.onrender.com"
        private bool _isSignalRConnected = false;
        private bool _isDisposed = false;
        private System.Timers.Timer _reconnectionTimer;
        private int _reconnectAttempts = 0;
        private const int MaxReconnectAttempts = 10;
        private const double ReconnectBaseDelayMs = 5000;
        private const double ReconnectMaxDelayMs = 60000;

        public BackendApiService(string backendBaseUrl)
        {
            if (string.IsNullOrWhiteSpace(backendBaseUrl))
                throw new ArgumentNullException(nameof(backendBaseUrl));

            _backendBaseUrl = backendBaseUrl.TrimEnd('/'); 
            _restClient = new HttpClient { BaseAddress = new Uri(_backendBaseUrl + "/api/") }; // Set base address for REST
            _restClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _reconnectionTimer = new System.Timers.Timer();
            _reconnectionTimer.Elapsed += async (sender, e) => await AttemptSignalRReconnect();
            _reconnectionTimer.AutoReset = false;
        }

        public bool IsUserLoggedIn => !string.IsNullOrEmpty(_operatorJwt);
        public bool IsSignalRConnected => _isSignalRConnected;

        /// <summary>
        /// Attempts to log in the operator using provided credentials.
        /// Stores the JWT on success.
        /// </summary>
        /// <returns>True if login successful, false otherwise.</returns>
        public async Task<bool> LoginOperatorAsync(string email, string password)
        {
            try
            {
                var loginData = new { email, password };
                string jsonPayload = JsonConvert.SerializeObject(loginData);
                var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await _restClient.PostAsync("users/login", content);

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var loginResponse = JsonConvert.DeserializeObject<LoginResponseDto>(jsonResponse);
                    if (loginResponse != null && !string.IsNullOrEmpty(loginResponse.Token))
                    {
                        _operatorJwt = loginResponse.Token;
                        _restClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _operatorJwt);
                        Console.WriteLine("Operator login successful.");
                        return true;
                    }
                    else
                    {
                        Console.WriteLine("Operator login failed: Invalid response format.");
                        _operatorJwt = null;
                        return false;
                    }
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Operator login failed: {response.StatusCode} - {errorContent}");
                    _operatorJwt = null;
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Operator login error: {ex.Message}");
                _operatorJwt = null;
                return false;
            }
        }

        /// <summary>
        /// Establishes the SignalR connection to the /tradinghub.
        /// Handles authentication and sets up robust reconnection logic.
        /// </summary>
        public async Task ConnectSignalRAsync()
        {
            if (string.IsNullOrEmpty(_operatorJwt))
            {
                Console.WriteLine("Cannot connect SignalR: Operator not logged in.");
                return;
            }
            if (_signalRConnection != null && _signalRConnection.State != ConnectionState.Disconnected)
            {
                Console.WriteLine("SignalR connection already exists or is connecting.");
                return;
            }

            // Construct hub URL (relative to base address)
            string hubUrl = _backendBaseUrl + "/tradinghub";
            Console.WriteLine($"Attempting to connect SignalR to: {hubUrl}");

            _signalRConnection = new HubConnection(hubUrl);

            // --- Authentication ---
            _signalRConnection.Headers.Add("Authorization", $"Bearer {_operatorJwt}");
            var hubProxy = _signalRConnection.CreateHubProxy("TradingHub");
            hubProxy["access_token"] = _operatorJwt;

            // --- Event Handlers ---
            _signalRConnection.StateChanged += SignalRConnection_StateChanged;
            _signalRConnection.Closed += SignalRConnection_Closed;
            _signalRConnection.Error += SignalRConnection_Error;

            await StartSignalRWithRetryAsync();
        }

        private async Task StartSignalRWithRetryAsync()
        {
            if (_signalRConnection == null || _isDisposed) return;

            Console.WriteLine("Starting SignalR connection attempt...");
            try
            {
                await _signalRConnection.Start();
            }
            catch (HttpRequestException httpEx) 
            {
                Console.WriteLine($"SignalR connection failed (HTTP): {httpEx.Message}. Will retry.");
                ScheduleSignalRReconnect();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SignalR connection failed (General): {ex.Message}. Will retry.");
                ScheduleSignalRReconnect();
            }
        }

        private void SignalRConnection_StateChanged(StateChange state)
        {
            _isSignalRConnected = state.NewState == ConnectionState.Connected;
            Console.WriteLine($"SignalR State Changed: {state.OldState} -> {state.NewState}");

            if (state.NewState == ConnectionState.Connected)
            {
                _reconnectAttempts = 0;
                _reconnectionTimer.Stop();
            }
        }

        private async void SignalRConnection_Closed()
        {
            _isSignalRConnected = false;
            Console.WriteLine("SignalR connection closed.");
            if (!_isDisposed)
            {
                ScheduleSignalRReconnect();
            }
        }

        private void SignalRConnection_Error(Exception error)
        {
            _isSignalRConnected = false;
            Console.WriteLine($"SignalR connection error: {error?.Message ?? "Unknown error"}");
        }

        private void ScheduleSignalRReconnect()
        {
            if (_reconnectionTimer.Enabled || _isDisposed) return;

            if (_reconnectAttempts < MaxReconnectAttempts)
            {
                _reconnectAttempts++;
                // Exponential backoff with jitter
                double delay = Math.Pow(2, _reconnectAttempts) * ReconnectBaseDelayMs;
                delay = Math.Min(delay, ReconnectMaxDelayMs);
                delay *= 0.8 + new Random().NextDouble() * 0.4; // Add jitter +/- 20%

                _reconnectionTimer.Interval = delay;
                _reconnectionTimer.Start();
                Console.WriteLine($"SignalR reconnection attempt {_reconnectAttempts}/{MaxReconnectAttempts} scheduled in {delay / 1000:F1} seconds.");
            }
            else
            {
                Console.WriteLine("Max SignalR reconnection attempts reached. Will not retry automatically for a while.");
                // Optionally, schedule a longer-term retry (e.g., 5 minutes)
                // _reconnectionTimer.Interval = 300000;
                // _reconnectionTimer.Start();
            }
        }

        private async Task AttemptSignalRReconnect()
        {
            if (!_isDisposed)
            {
                Console.WriteLine($"Executing SignalR reconnection attempt {_reconnectAttempts}.");
                await StartSignalRWithRetryAsync();
            }
        }


        /// <summary>
        /// Gracefully stops the SignalR connection.
        /// </summary>
        public async Task DisconnectSignalRAsync()
        {
            if (_signalRConnection != null)
            {
                _reconnectionTimer.Stop();
                _signalRConnection.StateChanged -= SignalRConnection_StateChanged;
                _signalRConnection.Closed -= SignalRConnection_Closed;
                _signalRConnection.Error -= SignalRConnection_Error;

                if (_signalRConnection.State != ConnectionState.Disconnected)
                {
                    Console.WriteLine("Stopping SignalR connection...");
                    try
                    {
                        _signalRConnection.Stop(TimeSpan.FromSeconds(5));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error stopping SignalR connection: {ex.Message}");
                    }
                }
                _signalRConnection.Dispose();
                _signalRConnection = null;
                _isSignalRConnected = false;
                Console.WriteLine("SignalR connection disposed.");
            }
        }

        /// <summary>
        /// Fetches a specific trading queue from the backend.
        /// </summary>
        /// <param name="queueType">"paid", "virtual-trial", "free-virtual", or "free-real"</param>
        /// <returns>List of tokens or null on error.</returns>
        public async Task<List<TradingQueueItemDto>> FetchTradingQueueAsync(string queueType)
        {
            if (string.IsNullOrEmpty(_operatorJwt))
            {
                Console.WriteLine("Cannot fetch queue: Operator not logged in.");
                return null;
            }
            if (!new[] { "paid", "virtual-trial", "free-virtual", "free-real" }.Contains(queueType))
            {
                Console.WriteLine($"Invalid queue type requested: {queueType}");
                return null;
            }

            try
            {
                _restClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _operatorJwt);

                HttpResponseMessage response = await _restClient.GetAsync($"TradingQueue/{queueType}");

                if (response.IsSuccessStatusCode)
                {
                    string jsonResponse = await response.Content.ReadAsStringAsync();
                    var queue = JsonConvert.DeserializeObject<List<TradingQueueItemDto>>(jsonResponse);
                    Console.WriteLine($"Successfully fetched {queue?.Count ?? 0} items for queue '{queueType}'.");
                    return queue ?? new List<TradingQueueItemDto>();
                }
                else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    Console.WriteLine($"Fetching queue '{queueType}' forbidden: Operator not authorized.");
                    return null;
                }
                else
                {
                    string errorContent = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"Failed to fetch queue '{queueType}': {response.StatusCode} - {errorContent}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching queue '{queueType}': {ex.Message}");
                return null;
            }
        }


        // --- Placeholder Methods for Sending Updates via SignalR (maybe called from your FxApi/Trading Logic) ---

        public async Task SendProfitUpdateAsync(string apiTokenValue, decimal newProfit)
        {
            if (!_isSignalRConnected || _signalRConnection == null)
            {
                Console.WriteLine($"SignalR not connected. Cannot send profit update for {apiTokenValue}.");
                return;
            }
            try
            {
                var hubProxy = _signalRConnection.CreateHubProxy("TradingHub");
                await hubProxy.Invoke("SendProfitUpdate", apiTokenValue, newProfit);
                Console.WriteLine($"Sent profit update for {apiTokenValue}: {newProfit}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending profit update for {apiTokenValue}: {ex.Message}");
                // Consider implications if sending fails (e.g., queueing?)
            }
        }

        public async Task SendStatusUpdateAsync(string apiTokenValue, string newStatus)
        {
            if (!_isSignalRConnected || _signalRConnection == null)
            {
                Console.WriteLine($"SignalR not connected. Cannot send status update for {apiTokenValue}.");
                return;
            }
            try
            {
                var hubProxy = _signalRConnection.CreateHubProxy("TradingHub");
                await hubProxy.Invoke("SendStatusUpdate", apiTokenValue, newStatus);
                Console.WriteLine($"Sent status update for {apiTokenValue}: {newStatus}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending status update for {apiTokenValue}: {ex.Message}");
            }
        }

        public async Task SendTradingPingAsync(string apiTokenValue)
        {
            if (!_isSignalRConnected || _signalRConnection == null)
            {
                Console.WriteLine($"SignalR not connected. Cannot send trading ping for {apiTokenValue}.");
                return;
            }
            try
            {
                var hubProxy = _signalRConnection.CreateHubProxy("TradingHub");
                await hubProxy.Invoke("SendTradingPing", apiTokenValue);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending trading ping for {apiTokenValue}: {ex.Message}");
            }
        }

        // --- Dispose Pattern ---
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_isDisposed) return;

            if (disposing)
            {
                _reconnectionTimer?.Stop();
                _reconnectionTimer?.Dispose();
                Task.Run(async () => await DisconnectSignalRAsync()).Wait(TimeSpan.FromSeconds(5));
                _signalRConnection?.Dispose();
                _restClient?.Dispose();
            }

            // Free unmanaged resources (unmanaged objects) and override finalizer
            _operatorJwt = null;
            _signalRConnection = null;

            _isDisposed = true;
            Console.WriteLine("BackendApiService disposed.");
        }

        ~BackendApiService()
        {
            Dispose(disposing: false);
        }
    }
}