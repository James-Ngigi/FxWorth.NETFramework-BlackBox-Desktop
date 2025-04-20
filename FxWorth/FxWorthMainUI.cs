using FxApi;
using FxApi.Connection;
using FxWorth.Hierarchy;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using static FxApi.AuthClient;
using static FxWorth.Hierarchy.HierarchyNavigator;
using FxBackendClient;
using System.Threading.Tasks;

namespace FxWorth
{
    public partial class FxWorth : Form
    {
        
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            PowerManager.PreventSleep();

            // !SETTING HIGH PRIORITY!
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
        }

        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= 0x02000000;  // WS_EX_COMPOSITED setting enabled.
                return cp;
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (Process.GetCurrentProcess().PriorityClass == ProcessPriorityClass.High)
            {
                PowerManager.AllowSleep();
            }

            if (e.Cancel == false)
            {
                _backendApiService?.Dispose();
            }
            base.OnFormClosing(e);
        }

        private bool tradingSessionCompleted = false;
        private static readonly Logger logger = LogManager.GetCurrentClassLogger();
        private readonly Stopwatch sw = new Stopwatch();
        private readonly TokenStorage storage;
        private readonly string layoutPath = "layout.json";
        private readonly List<NumericUpDown> numerics = new List<NumericUpDown>();
        private readonly ApiCompliance apiCompliance;
        private DateTime lastNoInternetShown = DateTime.MinValue;
        private TimeSpan noInternetDelay = TimeSpan.FromSeconds(12);
        private PhaseParameters phase1Parameters;
        private PhaseParameters phase2Parameters;
        private HierarchyNavigator hierarchyNavigator;
        private BackendApiService _backendApiService;
        private bool _isOperatorLoggedIn = false;
        private string _backendApiUrl = "http://localhost:8080"; // Later configured from App.config


        public Dictionary<int, CustomLayerConfig> customLayerConfigs = new Dictionary<int, CustomLayerConfig>();

        private void UpdateLatencyLabel(int latency)
        {
            if (Internet_conection_LBL.InvokeRequired)
            {
                Internet_conection_LBL.Invoke((MethodInvoker)delegate { UpdateLatencyLabel(latency); });
            }
            else
            {
                Internet_conection_LBL.Text = string.Format("Latency: {0} ms", latency);
            }
        }

        public void LoadTokensFromFetch(List<CredentialsWithTarget> fetchedData)
        {
            MessageBox.Show($"Received {fetchedData.Count} tokens to load.", "Data Extracted");
            // TODO: Implement logic to process fetchedData
            // - Clear existing tokens in dataGridView1?
            // - Create FxApi.Credentials objects
            // - Add them to storage.Credentials and storage.Clients
            // - Populate dataGridView1 on the main form
            // - Potentially associate the ProfitTarget with the TradingParameters for each client
        }

        public FxWorth()
        {
            logger.Info("<============================================================================================================================================>");
            Version appVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
            logger.Info("<=> Starting new session. App version {0}...", appVersion.ToString());

            InitializeComponent();
            {
                Choose_Asset_CMBX.SelectedIndex = 0;
                Duration0_CMBX.SelectedIndex = 0;
                Close_Interval0_CMBX.SelectedIndex = 0;
            }

            this.AutoScaleMode = AutoScaleMode.Dpi;
            customLayerConfigs = new Dictionary<int, CustomLayerConfig>();
            string logFolderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log files");
            string archiveFolderPath = Path.Combine(logFolderPath, "Archived files");
            apiCompliance = new ApiCompliance(logFolderPath, archiveFolderPath);
            apiCompliance.Start();
            storage = new TokenStorage("tokens.json");
            storage.ClientsStateChanged += ClientsStateChanged;
            storage.InternetSpeedChanged += OnInternetSpeedChanged;
            /// storage.TradeUpdated += OnTradeUpdate;
            storage.AuthFailed += OnAuthFailed;
            phase1Parameters = new PhaseParameters();
            phase2Parameters = new PhaseParameters();
            storage.ClientsStateChanged += ClientsStateChanged;
            Timer updateTimer = new Timer();
            updateTimer.Interval = 300; 
            updateTimer.Tick += (s, args) => ClientsStateChanged(storage, EventArgs.Empty);
            updateTimer.Start();
            storage.TradeUpdated += Storage_TradeUpdated;

            try
            {
                _backendApiService = new BackendApiService(_backendApiUrl);
            }
            catch (ArgumentNullException ex)
            {
                MessageBox.Show($"Backend API URL is not configured correctly: {ex.Message}", "Configuration Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                // Handle error appropriately, maybe disable features or close app
                // For now, disable the fetch button if service fails to initialize
                Fetch.Enabled = false; // Assuming 'Fetch' is the design name of your label/button
            }
            catch (Exception ex) // Catch other potential exceptions during initialization
            {
                MessageBox.Show($"Failed to initialize Backend API Service: {ex.Message}", "Initialization Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Fetch.Enabled = false;
            }

            foreach (var kvp in storage.Clients)
            {
                kvp.Value.StatusChanged += Client_StatusChanged;
            }

            foreach (var credential in storage.Credentials)
            {
                Main_Token_Table.Rows.Add(credential.IsChecked, credential.Token, credential.AppId, null, null, null);
            }

            foreach (DataGridViewRow row in Main_Token_Table.Rows)
            {
                row.Cells[5].Value = "Spooling";
            }

            if (storage.Credentials.Count >= TokenStorage.MaxTokensCount)
            {
                Add_BTN.Enabled = false;
            }

            var fields = this.GetType().GetFields(
                BindingFlags.NonPublic |
                BindingFlags.Instance).Where(x => x.FieldType.Name == nameof(NumericUpDown)).ToList();
            LoadLayout();

            foreach (var f in fields)
            {
                var ctr = (NumericUpDown)f.GetValue(this);
                ctr.ContextMenuStrip = Cut_Copy_Paste_CMS;
                numerics.Add(ctr);
            }
        }

        private void Storage_TradeUpdated(object sender, TradeEventArgs e)
        {
            this.InvokeIfRequired(() =>
            {
                TradeModel model = e.Model;

                dataGridView2.SuspendLayout();
                try
                {
                    var existingRow = dataGridView2.Rows
                        .Cast<DataGridViewRow>()
                        .FirstOrDefault(r => r.Cells[0].Value?.ToString() == model.Token && (int)r.Cells[1].Value == model.Id);

                    if (existingRow != null)
                    {
                        existingRow.Cells[2].Value = model.TradeState;
                        existingRow.Cells[4].Value = model.TradeResult;
                        existingRow.Cells[5].Value = model.Profit;
                    }
                    else
                    {
                        int newRowIndex = dataGridView2.Rows.Add(model.Token, model.Id, model.TradeState, model.Stake, null, null);
                        dataGridView2.FirstDisplayedScrollingRowIndex = newRowIndex;
                    }
                }
                finally
                {
                    dataGridView2.ResumeLayout(true);
                }

                if (storage.IsHierarchyMode)
                {
                    var client = e.Client;

                    if (client != null)
                    {
                        HierarchyLevel currentLevel = storage.hierarchyNavigator.GetCurrentLevel();

                        if (currentLevel != null)
                        {
                            // Pass the current level's recoveryResults to Process
                            client.TradingParameters.Process(model.Profit, model.Payouts.Max(), int.Parse(client.GetToken()), model.Id, 0);

                            if (!client.TradingParameters.IsRecoveryMode)
                            {
                                storage.hierarchyNavigator.MoveToNextLevel(client);

                                if (storage.hierarchyNavigator.currentLevelId == "0")
                                {
                                    logger.Info("Returned to root level trading.");
                                }
                                else
                                {
                                    storage.hierarchyNavigator.LoadLevelTradingParameters(storage.hierarchyNavigator.currentLevelId, client, client.TradingParameters);
                                    logger.Info($"Moved to next level: {storage.hierarchyNavigator.currentLevelId}");
                                }
                            }
                            else
                            {
                                currentLevel.AmountToBeRecovered = client.TradingParameters.AmountToBeRecoverd;

                                decimal maxDrawdown = currentLevel.MaxDrawdown ?? (currentLevel.LevelId.StartsWith("1.") ? storage.phase2Parameters.MaxDrawdown : storage.phase1Parameters.MaxDrawdown);
                                if (currentLevel.AmountToBeRecovered > maxDrawdown && currentLevel.LevelId.Split('.').Length < storage.MaxHierarchyDepth + 1)
                                {
                                    int nextLayer = currentLevel.LevelId.Split('.').Length + 1;

                                    decimal initialStakeForNextLayer;
                                    if (nextLayer == 2)
                                    {
                                        initialStakeForNextLayer = storage.customLayerConfigs.ContainsKey(nextLayer) ?
                                            (storage.customLayerConfigs[nextLayer].InitialStake ?? storage.InitialStakeLayer1) :
                                            storage.InitialStakeLayer1;
                                    }
                                    else
                                    {
                                        initialStakeForNextLayer = storage.customLayerConfigs.ContainsKey(nextLayer) ?
                                            (storage.customLayerConfigs[nextLayer].InitialStake ?? currentLevel.InitialStake) :
                                            currentLevel.InitialStake;
                                    }


                                    storage.hierarchyNavigator.CreateLayer(nextLayer, currentLevel.AmountToBeRecovered, client.TradingParameters, storage.customLayerConfigs, initialStakeForNextLayer);

                                    string nextLevelId = $"{currentLevel.LevelId}.1";
                                    storage.hierarchyNavigator.currentLevelId = nextLevelId;
                                    storage.hierarchyNavigator.LoadLevelTradingParameters(nextLevelId, client, client.TradingParameters);
                                    logger.Info($"Created new layer {nextLayer} and moved to level: {nextLevelId}");
                                }
                            }
                        }
                    }
                }
            });
        }

        private void Client_StatusChanged(object sender, StatusChangedEventArgs e)
        {
            this.InvokeIfRequired(() =>
            {
                foreach (DataGridViewRow row in Main_Token_Table.Rows)
                {

                    var creds = storage.Clients.FirstOrDefault(x => x.Value == sender).Key;


                    if (creds == null) continue;
                    if (row.Cells[1].Value?.ToString() == creds.Token &&
                        row.Cells[2].Value?.ToString() == creds.AppId)
                    {
                        row.Cells[5].Value = e.Status;

                        if (e.Status == "Invalid")
                        {
                            row.Cells[0].Value = false;
                            storage.EnableCredentials(false, creds.AppId, creds.Token);
                        }

                        break;
                    }

                }
            });
        }

        private void OnAuthFailed(object sender, AuthFailedArgs e)
        {
            this.InvokeIfRequired(() =>
            {
                foreach (DataGridViewRow row in Main_Token_Table.Rows)
                {
                    var token = row.Cells[1].Value.ToString();
                    var appId = row.Cells[2].Value.ToString();

                    if (e.Credentials.Token == token && e.Credentials.AppId == appId)
                    {
                        row.Cells[0].Value = false;
                        return;
                    }
                }
            });
        }

        /// <summary>
        /// This method loads a previously saved trading layout or configuration file and applies its settings to a 
        /// the UI controls. It’s designed to streamline resuming work with saved preferences, minimizing manual setup.
        /// </summary>
        private void LoadLayout()
        {
            if (!File.Exists(layoutPath))
            {
                return;
            }

            var jstr = File.ReadAllText(layoutPath);
            var layout = JsonConvert.DeserializeObject<Layout>(jstr);

            // Market Data Parameters
            Period0_TXT.Value = layout.MarketDataParameters.Rsi.Period;
            Overbought0_TXT.Value = (decimal)layout.MarketDataParameters.Rsi.Overbought;
            Oversold_TXT.Value = (decimal)layout.MarketDataParameters.Rsi.Oversold;
            Close_Interval0_TXT.Value = Math.Abs(layout.MarketDataParameters.Rsi.TimeFrame);
            Close_Interval0_CMBX.SelectedIndex = layout.MarketDataParameters.Rsi.TimeFrame > 0 ? 1 : 0;
            Choose_Asset_CMBX.SelectedIndex = Choose_Asset_CMBX.Items.IndexOf(layout.MarketDataParameters.Symbol);

            // Trading Parameters - Phase 1
            Barrier_Offset_TXT.Value = layout.TradingParameters.Barrier;
            Duration_TXT.Value = layout.TradingParameters.Duration;
            Duration0_CMBX.SelectedIndex = Duration0_CMBX.Items.IndexOf(layout.TradingParameters.DurationType);
            Stake_TXT.Value = layout.TradingParameters.Stake;
            Take_Profit_TXT.Value = layout.TradingParameters.TakeProfit;
            Max_Drawdown_TXT1.Value = layout.TradingParameters.MaxDrawdown;
            Martingale_Level_TXT.Value = layout.TradingParameters.MartingaleLevel;
            Stake_TXT2.Value = layout.TradingParameters.InitialStake4Layer1;
            Hierarchy_Levels_TXT.Value = layout.TradingParameters.HierarchyLevels;
            Max_Depth_TXT.Value = layout.TradingParameters.MaxHierarchyDepth;

            // Trading Parameters - Phase 2 (Hierarchy)
            Barrier_Offset_TXT2.Value = layout.Phase2Parameters.Barrier;
            Martingale_Level_TXT2.Value = layout.Phase2Parameters.MartingaleLevel;
            Max_Drawdown_TXT2.Value = layout.Phase2Parameters.MaxDrawdown;

            // Null check for backward compatibility.
            if (layout.CustomLayerConfigs != null)
            {
                customLayerConfigs = layout.CustomLayerConfigs;
            }
        }

        private void OnInternetSpeedChanged(object sender, EventArgs e)
        {
            this.InvokeIfRequired(() =>
            {
                Internet_conection_LBL.Text = string.Format("Latency: {0} ms", storage.PingClient.Latency);
                Trade_Logs_GRBX.Text = string.Format("Trade table report ⤵ : RSI ➟ {0:N2}", storage.rsi?.Value);
                bool isTrading = false;

                foreach (DataGridViewRow row in Main_Token_Table.Rows)
                {
                    if (row.Cells[5].Value?.ToString() == "Trading")
                    {
                        isTrading = true;
                        break;
                    }
                }

                if (storage.IsInternetSlow())
                {
                    if (sw.IsRunning)
                    {
                        logger.Info("<=> Internet latency exceeded threshold. Pausing trades & stopwatch.");
                        sw.Stop();
                    }
                    if (isTrading && // Only show if currently trading
                        OwnedForms.Length == 0 &&
                        WindowState != FormWindowState.Minimized &&
                        (DateTime.Now - lastNoInternetShown) > noInternetDelay)
                    {
                        BeginInvoke((Action)(() =>
                        {
                            var frm = new No_Internet();
                            frm.ShowDialog(this);
                            lastNoInternetShown = DateTime.Now;
                        }));
                    }
                }

                else // Internet speed is okay
                {
                    if (!sw.IsRunning && storage.IsTradingAllowed && !tradingSessionCompleted)
                    {
                        logger.Info("<=> Internet latency back within threshold. Resuming trades & stopwatch.");
                        sw.Start();
                    }
                    if (OwnedForms.Length > 0 && OwnedForms[0] is No_Internet)
                    {
                        OwnedForms[0].Close();
                        lastNoInternetShown = DateTime.MinValue;
                    }
                }
            });
        }

        private void ClientsStateChanged(object sender, EventArgs e)
        {
            this.InvokeIfRequired(() =>
            {
                Valid_Tokens_LBL.Text = string.Format("Valid Accounts: {0}", storage.Clients.Count(x => x.Value.IsOnline));

                if (tradingSessionCompleted)
                {
                    return;
                }

                foreach (DataGridViewRow row in Main_Token_Table.Rows)
                {
                    var token = row.Cells[1].Value?.ToString();
                    var appId = row.Cells[2].Value?.ToString();
                    var key = storage.Clients.Keys.FirstOrDefault(x => x.AppId == appId && x.Token == token);

                    if (key == null)
                    {
                        continue;
                    }

                    var client = storage.Clients[key];
                    row.Cells[3].Value = client.Balance;
                    row.Cells[4].Value = client.Pnl;

                    if (client.TradingParameters == null)
                    {
                        row.Cells[5].Value = "Standby";
                        continue;
                    }

                    bool isSelected = (bool)row.Cells[0].Value;
                    string currentStatus = row.Cells[5].Value?.ToString();

                    if (currentStatus == "Invalid")
                    {
                        // Maintain "Invalid" status.
                    }
                    else if (!client.IsOnline)
                    {
                        row.Cells[5].Value = "Offline";
                    }
                    else // Client is online
                    {
                        if (client.Pnl <= -client.TradingParameters.Stoploss || client.Balance < 2 * client.TradingParameters.DynamicStake)
                        {
                            row.Cells[5].Value = "Stoploss";
                        }
                        else if (client.Pnl >= client.TradingParameters.TakeProfit)
                        {
                            row.Cells[5].Value = "TakeProfit";
                        }
                        else if (storage.IsTradingAllowed && isSelected)
                        {
                            row.Cells[5].Value = "Trading";
                        }
                        else if (!storage.IsTradingAllowed)
                        {
                            row.Cells[5].Value = "Analyzing";
                        }
                        else
                        {
                            row.Cells[5].Value = "Standby";
                        }
                    }
                }

                TimeSpan minimumTradingTime = TimeSpan.FromSeconds(2);

                if (!sw.IsRunning || sw.Elapsed < minimumTradingTime)
                {
                    return;
                }

                bool allClientsCompleted = true;

                foreach (DataGridViewRow row in Main_Token_Table.Rows)
                {
                    if ((bool)row.Cells[0].Value && storage.Clients.ContainsKey(storage.Credentials[row.Index]) && storage.Clients[storage.Credentials[row.Index]].TradingParameters != null)
                    {
                        if (row.Cells[5].Value?.ToString() == "Trading" || row.Cells[5].Value?.ToString() == "Analyzing")
                        {
                            allClientsCompleted = false;
                            break;
                        }
                    }
                }

                if (allClientsCompleted)
                {
                    logger.Info("<=> Trading session completed on all accounts. Stopping timer!");
                    sw.Stop();
                    tradingSessionCompleted = true;

                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;
                    PowerManager.AllowSleep();
                }
            });
        }

        private void FxWorth_Resize(object sender, EventArgs e)
        {
            bool MousePointerNotOnTaskbar = Screen.GetWorkingArea(this).Contains(Cursor.Position);
            if (WindowState == FormWindowState.Minimized && MousePointerNotOnTaskbar)
            {
                ShowInTaskbar = false;
                Minimize_Notification.Visible = true;
                Minimize_Notification.ShowBalloonTip(80);
            }
        }
        private void Notification_Icon_Click(object sender, EventArgs e)
        {

        }

        private void panel6_Paint(object sender, PaintEventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void FxWorth_Load(object sender, EventArgs e)
        {
            Minimize_Notification.BalloonTipText = "Assessing & making moves in the background.";
            Minimize_Notification.BalloonTipTitle = "Keeping It Slick";
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            var frm = new Exit_Application();

            if (WindowState == FormWindowState.Minimized)
            {
                frm.StartPosition = FormStartPosition.CenterScreen;
            }

            DialogResult result = frm.ShowDialog(this);

            if (result == DialogResult.Yes)
            {
                storage.Dispose();
                return;
            }

            e.Cancel = true;
        }

        private void Add_BTN_Click(object sender, EventArgs e)
        {
            var frm = new Adding_New_Token();
            var result = frm.ShowDialog(this);

            if (result == DialogResult.Cancel)
            {
                return;
            }

            var creds = new Credentials
            {
                AppId = frm.appTextBox.Text,
                Token = frm.tokenTextBox.Text,
                Name = frm.appTextBox.Text
            };

            if (creds.AppId.Length * creds.Token.Length == 0)
            {
                return;
            }

            if (!storage.Add(creds))
            {
                return;
            }

            Main_Token_Table.Rows.Add(false, creds.Token, creds.AppId, null, null);

            if (storage.Credentials.Count >= TokenStorage.MaxTokensCount)
            {
                Add_BTN.Enabled = false;
            }
        }

        private void Remove_BTN_Click(object sender, EventArgs e)
        {
            for (int i = Main_Token_Table.SelectedRows.Count - 1; i >= 0; i--)
            {
                var row = Main_Token_Table.SelectedRows[i];
                var appId = row.Cells[2].Value.ToString();
                var token = row.Cells[1].Value.ToString();
                var frm = new Remove_Token(appId, token);
                var result = frm.ShowDialog(this);

                if (result == DialogResult.No)
                {
                    return;
                }
                else if (result == DialogResult.Yes)
                {
                    storage.Remove(appId, token);
                    Main_Token_Table.Rows.Remove(row);
                }

            }

            if (storage.Credentials.Count < TokenStorage.MaxTokensCount)
            {
                Add_BTN.Enabled = true;
            }
        }

        private void Start_BTN_Click(object sender, EventArgs e)
        {
            bool anyClientChecked = storage.Credentials.Any(c => c.IsChecked);

            if (!anyClientChecked && storage.Clients.Count(x => x.Value.IsOnline) == 0)
            {
                var frm = new Select_Token();
                frm.ShowDialog(this);
                return;
            }

            if (Stake_TXT.Value > Max_Drawdown_TXT1.Value)
            {
                var mess = new Maximum_Stake_Error();
                mess.ShowDialog(this);
                return;
            }

            if (storage.Credentials.Count == 0)
            {
                var frm = new Select_Token();
                frm.ShowDialog(this);
                return;
            }

            int rsiTimeFrame = (int)Close_Interval0_TXT.Value;

            if (Close_Interval0_CMBX.SelectedIndex == 0)
            {
                rsiTimeFrame *= -1;
            }

            if (Close_Interval0_CMBX.SelectedIndex >= 2)
            {
                rsiTimeFrame *= 60;
            }

            if (Close_Interval0_CMBX.SelectedIndex >= 3)
            {
                rsiTimeFrame *= 60;
            }

            if (!TimeFrameValidator.IsSupportedCustomTimeFrame(rsiTimeFrame))
            {
                var mess = new RSI_Close_Interval("RSI", "RSI2");
                mess.ShowDialog(this);
                return;
            }

            if (Duration0_CMBX.SelectedIndex == 0 && Duration_TXT.Value < 5)
            {
                var mess = new Ticks_Duration();
                mess.ShowDialog(this);
                return;
            }

            if (Duration0_CMBX.SelectedIndex == 1 && Duration_TXT.Value < 15)
            {
                var mess = new Seconds_duration();
                mess.ShowDialog(this);
                return;
            }

            decimal initialStakeLayer1 = Stake_TXT2.Value;

            phase1Parameters = new PhaseParameters
            {
                Barrier = Barrier_Offset_TXT.Value,
                MartingaleLevel = (int)Martingale_Level_TXT.Value,
                MaxDrawdown = Max_Drawdown_TXT1.Value
            };

            phase2Parameters = new PhaseParameters
            {
                Barrier = Barrier_Offset_TXT2.Value,
                MartingaleLevel = (int)Martingale_Level_TXT2.Value,
                MaxDrawdown = Max_Drawdown_TXT2.Value
            };

            dataGridView2.Rows.Clear();
            DisableAll();

            var parameters = new TradingParameters()
            {
                Barrier = Barrier_Offset_TXT.Value,
                Symbol = storage.MarketDataClient.GetInstrument(Choose_Asset_CMBX.Text),
                Duration = (int)Duration_TXT.Value,
                DurationType = Duration0_CMBX.Text,
                Stake = Stake_TXT.Value,
                TakeProfit = Take_Profit_TXT.Value,
                MaxDrawdown = Max_Drawdown_TXT1.Value,
                MartingaleLevel = (int)Martingale_Level_TXT.Value,
                DynamicStake = Stake_TXT.Value,
                HierarchyLevels = (int)Hierarchy_Levels_TXT.Value,
                MaxHierarchyDepth = (int)Max_Depth_TXT.Value
            };

            storage.SetHierarchyParameters(phase1Parameters, phase2Parameters, customLayerConfigs);
            storage.InitialStakeLayer1 = initialStakeLayer1;
            hierarchyNavigator = new HierarchyNavigator(parameters.AmountToBeRecoverd, parameters, phase1Parameters, phase2Parameters, customLayerConfigs, storage.InitialStakeLayer1, storage);

            storage.SetTradingParameters(parameters);
            storage.StartAll();

            var md = storage.SubscribeMarketData(
                (int)Period0_TXT.Value,
                (double)Overbought0_TXT.Value,
                (double)Oversold_TXT.Value,
                rsiTimeFrame,
                Choose_Asset_CMBX.Text);

            storage.IsTradingAllowed = true;
            var layout = new Layout()
            {
                MarketDataParameters = md,
                TradingParameters = parameters,
                Phase2Parameters = phase2Parameters,
                CustomLayerConfigs = customLayerConfigs
            };
            File.WriteAllText("layout.json", JsonConvert.SerializeObject(layout, Formatting.Indented));

            if (Process.GetCurrentProcess().PriorityClass != ProcessPriorityClass.High)
            {
                PowerManager.PreventSleep();
                Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            }

            tradingSessionCompleted = false;

            if (!sw.IsRunning)
            {
                sw.Reset();
                sw.Start();
            }

            foreach (DataGridViewRow row in Main_Token_Table.Rows)
            {
                var appId = row.Cells[2].Value.ToString();
                var token = row.Cells[1].Value.ToString();
                var key = storage.Clients.Keys.FirstOrDefault(x => x.AppId == appId && x.Token == token);

                if (key == null)
                {
                    continue;
                }

                var client = storage.Clients[key];

                if (client.TradingParameters == null)
                {
                    continue;
                }

                bool isSelected = (bool)row.Cells[0].Value;

                if (isSelected)
                {
                    row.Cells[5].Value = "Trading";
                }
                else
                {
                    row.Cells[5].Value = "Standby";
                }
            }
        }

        private void DisableAll()
        {
            Start_BTN.Enabled = false;
            Main_Token_Table.Enabled = false;
            Add_BTN.Enabled = false;
            Remove_BTN.Enabled = false;
            Trading_Parameters_GRBX.Enabled = false;
            Money_Management_GRBX.Enabled = false;
            RSI_Indicator_Settings_GRBX.Enabled = false;
        }

        private void EnableAll()
        {
            Start_BTN.Enabled = true;
            Main_Token_Table.Enabled = true;
            Add_BTN.Enabled = true;
            Remove_BTN.Enabled = true;
            Trading_Parameters_GRBX.Enabled = true;
            Money_Management_GRBX.Enabled = true;
            RSI_Indicator_Settings_GRBX.Enabled = true;
        }

        private void Stop_BTN_Click(object sender, EventArgs e)
        {
            sw.Stop();
            storage.IsTradingAllowed = false;
            tradingSessionCompleted = true;

            // Update DataGrid status *before* stopping the clients
            foreach (DataGridViewRow row in Main_Token_Table.Rows)
            {
                if (((row.Cells[5].Value?.ToString() == "Trading" || row.Cells[5].Value?.ToString() == "Analyzing") && storage.Clients.ContainsKey(storage.Credentials[row.Index]) && storage.Clients[storage.Credentials[row.Index]].IsOnline) || row.Cells[5].Value?.ToString() == "Standby")
                {
                    row.Cells[5].Value = "Completed";
                }
            }

            storage.StopAll();
            Pause_BTN.Text = "Pause";
            EnableAll();
            storage.MarketDataClient.UnsubscribeAll();
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.Normal;
            PowerManager.AllowSleep();
        }

        private void Pause_BTN_Click(object sender, EventArgs e)
        {
            if (Pause_BTN.Text == "Pause")
            {
                if (!storage.IsTradingAllowed)
                {
                    return;
                }

                storage.IsTradingAllowed = false;
                Pause_BTN.Text = "Resume";
                sw.Stop();

                foreach (DataGridViewRow row in Main_Token_Table.Rows)
                {
                    var token = row.Cells[1].Value?.ToString();
                    var appId = row.Cells[2].Value?.ToString();
                    var key = storage.Clients.Keys.FirstOrDefault(x => x.AppId == appId && x.Token == token);

                    if (key == null)
                    {
                        continue;
                    }

                    // Change to "Analyzing" ONLY if previously "Trading" AND client is online
                    if (row.Cells[5].Value?.ToString() == "Trading" && storage.Clients[key].IsOnline)
                    {
                        row.Cells[5].Value = "Analyzing";
                    }

                    // If offline while trying to pause trading, show "Invalid"
                    if (!storage.Clients[key].IsOnline && (row.Cells[5].Value?.ToString() == "Trading"))
                    {
                        row.Cells[5].Value = "Offline";
                    }
                }
            }
            else // Resuming trading
            {
                sw.Start();
                storage.IsTradingAllowed = true;
                Pause_BTN.Text = "Pause";

                foreach (DataGridViewRow row in Main_Token_Table.Rows)
                {
                    var token = row.Cells[1].Value?.ToString();
                    var appId = row.Cells[2].Value?.ToString();
                    var key = storage.Clients.Keys.FirstOrDefault(x => x.AppId == appId && x.Token == token);

                    if (key == null)
                    {
                        continue;
                    }

                    // Change back to "Trading" ONLY if previously "Analyzing" AND client is online
                    if (row.Cells[5].Value?.ToString() == "Analyzing" && storage.Clients[key].IsOnline)
                    {
                        row.Cells[5].Value = "Trading";
                    }

                    // Handle the case where a client goes offline while paused
                    if (!storage.Clients[key].IsOnline && (row.Cells[5].Value?.ToString() == "Analyzing"))
                    {
                        row.Cells[5].Value = "Offline";
                    }
                }
            }
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {

        }

        private void dataGridView1_CellValueChanged_1(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
            {
                return;
            }

            var row = Main_Token_Table.Rows[e.RowIndex];
            storage.EnableCredentials((bool)row.Cells[0].Value,
                row.Cells[2].Value.ToString(),
                row.Cells[1].Value.ToString());
        }

        private void Notification_Icon_DoubleClick(object sender, EventArgs e)
        {
            ShowInTaskbar = true;
            Minimize_Notification.Visible = false;
            WindowState = FormWindowState.Normal;
        }

        private void Display_TSMI_Click(object sender, EventArgs e)
        {
            ShowInTaskbar = true;
            Minimize_Notification.Visible = true;
            WindowState = FormWindowState.Normal;
        }

        private void Display_App_TSMI_Click(object sender, EventArgs e)
        {
            ShowInTaskbar = true;
            Minimize_Notification.Visible = true;
            WindowState = FormWindowState.Normal;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            label3.Text = sw.Elapsed.ToString("hh\\:mm\\:ss");
        }

        private void Exit_TSMI_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Pause_TSMI_Click(object sender, EventArgs e)
        {
            Pause_BTN.PerformClick();
        }

        private void Paste_TSMI_Click(object sender, EventArgs e)
        {
            var val = Clipboard.GetText();
            int res = 0;

            if (!int.TryParse(val, out res))
            {
                return;
            }

            foreach (var numeric in numerics)
            {
                if (numeric.Focused)
                {
                    numeric.Value = res;
                    numeric.Select(numeric.Text.Length, 0);
                    return;
                }
            }
        }

        private void Copy_TSMI_Click(object sender, EventArgs e)
        {
            foreach (var numeric in numerics)
            {
                if (numeric.Focused)
                {
                    Clipboard.SetText(numeric.Value.ToString());
                    return;
                }
            }
        }

        private void Cut_TSMI_Click(object sender, EventArgs e)
        {
            foreach (var numeric in numerics)
            {
                if (numeric.Focused)
                {
                    Clipboard.SetText(numeric.Value.ToString());
                    numeric.Value = Math.Max(numeric.Minimum, Math.Min(numeric.Maximum, 0));
                    return;
                }
            }
        }

        private void Custom_Layer_Config_BTN_Click(object sender, EventArgs e)
        {
            Layer_Configuration layerConfigForm = new Layer_Configuration(this);
            layerConfigForm.Owner = this;
            layerConfigForm.BarrierOffset = Barrier_Offset_TXT2.Value;
            layerConfigForm.MartingaleLevel = (int)Martingale_Level_TXT2.Value;
            layerConfigForm.HierarchyLevels = (int)Hierarchy_Levels_TXT.Value;
            layerConfigForm.MaxDrawdown = Max_Drawdown_TXT2.Value;
            layerConfigForm.InitialStake = (int)Stake_TXT2.Value;

            List<int> layerOptions = new List<int>();
            for (int i = 2; i <= Max_Depth_TXT.Value; i++)
            {
                layerOptions.Add(i);
            }

            layerConfigForm.LayerComboBox.DataSource = layerOptions;

            if (layerConfigForm.ShowDialog() == DialogResult.OK)
            {
                int selectedLayer = (int)layerConfigForm.LayerComboBox.SelectedItem;

                if (customLayerConfigs.ContainsKey(selectedLayer))
                {
                    CustomLayerConfig config = customLayerConfigs[selectedLayer];

                    logger.Info($"Custom configuration loaded for Layer {config.LayerNumber}:");
                    logger.Info($"Hierarchy Levels: {config.HierarchyLevels}");
                    logger.Info($"Martingale Level: {config.MartingaleLevel}");
                    logger.Info($"Max Drawdown: {config.MaxDrawdown}");
                    logger.Info($"Barrier Offset: {config.BarrierOffset}");
                    logger.Info($"Initial stake: {config.InitialStake}");
                }
                else
                {
                    logger.Info($"No custom configuration for Layer {selectedLayer}. Using default values.");
                }
            }
        }

        private void Hierarchy_Levels_TXT_ValueChanged(object sender, EventArgs e)
        {
            if (Hierarchy_Levels_TXT.Value == 1)
            {
                Hierarchy_Levels_TXT.Value = 2;
            }

            // Ensure Max_Depth_TXT is at least Hierarchy_Levels_TXT + 1
            Max_Depth_TXT.Value = Math.Max(Max_Depth_TXT.Value, Hierarchy_Levels_TXT.Value + 1);
        }

        private void Max_Depth_TXT_ValueChanged(object sender, EventArgs e)
        {
            // Ensure Max_Depth_TXT is at least Hierarchy_Levels_TXT + 1
            Max_Depth_TXT.Value = Math.Max(Max_Depth_TXT.Value, Hierarchy_Levels_TXT.Value + 1);
        }

        private async Task<bool> EnsureOperatorLogin()
        {
            if (_isOperatorLoggedIn) return true;

            // Show your Admin_Authorization form as a dialogue
            using (var loginDialog = new Admin_Authorization())
            {
                var result = loginDialog.ShowDialog(this); 

                if (result == DialogResult.OK)
                {
                    string email = loginDialog.EnteredEmail;
                    string password = loginDialog.EnteredPassword;

                    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
                    {
                        MessageBox.Show("Email and Password cannot be empty.", "Login Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }

                    this.Cursor = Cursors.WaitCursor;
                    bool loginSuccess = false;
                    try
                    {
                        loginSuccess = await _backendApiService.LoginOperatorAsync(email, password);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"An error occurred during login: {ex.Message}", "Login Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        // Log the full exception details
                    }
                    finally
                    {
                        this.Cursor = Cursors.Default;
                    }


                    if (loginSuccess)
                    {
                        _isOperatorLoggedIn = true;
                        // Connect SignalR AFTER successful login
                        try
                        {
                            await _backendApiService.ConnectSignalRAsync();
                            // Optionally notify user of successful connection
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Failed to connect to real-time service after login: {ex.Message}", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            // Login succeeded, but SignalR failed. Decide how to handle this.
                            // Maybe allow fetch but warn that real-time updates might not work?
                            // For now, we still return true as login itself worked.
                        }
                        return true;
                    }
                    else
                    {
                        // Login failed message is shown by LoginOperatorAsync or caught exception
                        _isOperatorLoggedIn = false;
                        return false;
                    }
                }
                else // User cancelled the login dialogue
                {
                    return false;
                }
            }
        }

        // --- Corrected Fetch_Click Event Handler ---
        private async void Fetch_Click(object sender, EventArgs e)
        {
            if (_backendApiService == null)
            {
                MessageBox.Show("Backend service is not available. Please check configuration or restart the application.", "Service Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            bool isLoggedIn = await EnsureOperatorLogin();

            if (isLoggedIn)
            {
                using (var dialog = new Subscriber_Fetch(_backendApiService))
                {
                    dialog.ShowDialog(this);

                    // Optional: Handle any result from the dialogue if needed
                    // if (dialog.DialogResult == DialogResult.OK) { ... }
                }
            }
            // If not loggedIn, the EnsureOperatorLogin method already handled showing errors or cancellation.
            // No further action needed here if login failed or was cancelled.
        }
    }

    public static class InvokeExtensions
    {
        public static void InvokeIfRequired(this Control control, MethodInvoker action)
        {
            if (control.IsDisposed || !control.IsHandleCreated)
            {
                return;
            }

            if (control.InvokeRequired)
            {
                try
                {
                    control.Invoke(action);
                }
                catch (ObjectDisposedException)
                {
                    // catch 'System.Threading.Tasks.TaskCanceledException' exception.
                }
            }
            else
            {
                action();
            }
        }
    }
}