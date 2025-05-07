using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Timers;

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

/// <summary>
/// This Class is responsible for managing the connection to the backend API and SignalR.
/// Connects to the FxWorth Backend Service responsible for fetching subsrcribers to allow for easy communication between account manger and clients
/// </summary>
namespace FxBackendClient
{
    public class BackendApiService : IDisposable
    {
        private readonly HttpClient _restClient;
        private HubConnection _signalRConnection;
        private string _operatorJwt;
        private readonly string _backendBaseUrl;
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
            _restClient = new HttpClient { BaseAddress = new Uri(_backendBaseUrl + "/api/") };
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
            if (_signalRConnection != null && _signalRConnection.State != HubConnectionState.Disconnected)
            {
                Console.WriteLine("SignalR connection already exists or is connecting.");
                return;
            }

            string hubUrl = _backendBaseUrl + "/tradinghub";
            Console.WriteLine($"Attempting to connect SignalR to: {hubUrl}");

            _signalRConnection = new HubConnectionBuilder()
                .WithUrl(hubUrl, options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(_operatorJwt);
                })
                .WithAutomaticReconnect(new[] {
                    TimeSpan.FromSeconds(0),
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromSeconds(10)
                })
                .Build();

            // Event handlers
            _signalRConnection.Closed += async (error) =>
            {
                _isSignalRConnected = false;
                Console.WriteLine($"SignalR connection closed: {error?.Message}");
                if (!_isDisposed)
                {
                    await Task.Run(() => ScheduleSignalRReconnect());
                }
            };

            _signalRConnection.Reconnecting += error =>
            {
                _isSignalRConnected = false;
                Console.WriteLine($"SignalR attempting to reconnect: {error?.Message}");
                return Task.CompletedTask;
            };

            _signalRConnection.Reconnected += connectionId =>
            {
                _isSignalRConnected = true;
                _reconnectAttempts = 0;
                Console.WriteLine($"SignalR reconnected. ConnectionId: {connectionId}");
                return Task.CompletedTask;
            };

            await StartSignalRWithRetryAsync();
        }

        /// Attempts to start the SignalR connection with retry logic.
        private async Task StartSignalRWithRetryAsync()
        {
            if (_signalRConnection == null || _isDisposed) return;

            Console.WriteLine("Starting SignalR connection attempt...");
            try
            {
                await _signalRConnection.StartAsync();
                _isSignalRConnected = true;
                _reconnectAttempts = 0;
                Console.WriteLine("SignalR connection established successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SignalR connection failed: {ex.Message}. Will retry.");
                ScheduleSignalRReconnect();
            }
        }

        /// Schedules a reconnection attempt with exponential backoff and jitter.
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
            }
        }

        /// Attempts to reconnect SignalR when the timer elapses.
        private async Task AttemptSignalRReconnect()
        {
            if (!_isDisposed)
            {
                Console.WriteLine($"Executing SignalR reconnection attempt {_reconnectAttempts}.");
                await StartSignalRWithRetryAsync();
            }
        }

        /// Gracefully stops the SignalR connection.
        public async Task DisconnectSignalRAsync()
        {
            if (_signalRConnection != null)
            {
                _reconnectionTimer.Stop();

                if (_signalRConnection.State != HubConnectionState.Disconnected)
                {
                    Console.WriteLine("Stopping SignalR connection...");
                    try
                    {
                        await _signalRConnection.StopAsync();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error stopping SignalR connection: {ex.Message}");
                    }
                }
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

        // --- Placeholder Methods for Sending Updates via SignalR ---
        public async Task SendProfitUpdateAsync(string apiTokenValue, decimal newProfit)
        {
            if (!_isSignalRConnected || _signalRConnection == null)
            {
                //Console.WriteLine($"SignalR not connected. Cannot send profit update for {apiTokenValue}.");
                return;
            }
            try
            {
                await _signalRConnection.InvokeAsync("SendProfitUpdate", apiTokenValue, newProfit);
                Console.WriteLine($"Sent profit update for {apiTokenValue}: {newProfit}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending profit update for {apiTokenValue}: {ex.Message}");
            }
        }

        /// Sends a status update to the SignalR hub.
        public async Task SendStatusUpdateAsync(string apiTokenValue, string newStatus)
        {
            if (!_isSignalRConnected || _signalRConnection == null)
            {
                Console.WriteLine($"SignalR not connected. Cannot send status update for {apiTokenValue}.");
                return;
            }
            try
            {
                await _signalRConnection.InvokeAsync("SendStatusUpdate", apiTokenValue, newStatus);
                Console.WriteLine($"Sent status update for {apiTokenValue}: {newStatus}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error sending status update for {apiTokenValue}: {ex.Message}");
            }
        }

        /// Sends a trading ping to the SignalR hub.
        public async Task SendTradingPingAsync(string apiTokenValue)
        {
            if (!_isSignalRConnected || _signalRConnection == null)
            {
                Console.WriteLine($"SignalR not connected. Cannot send trading ping for {apiTokenValue}.");
                return;
            }
            try
            {
                await _signalRConnection.InvokeAsync("SendTradingPing", apiTokenValue);
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
                _restClient?.Dispose();
            }

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