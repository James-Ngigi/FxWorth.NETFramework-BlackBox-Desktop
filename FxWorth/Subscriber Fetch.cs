using FxApi;
using FxBackendClient;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FxWorth
{
    public partial class Subscriber_Fetch : Form
    {
        private readonly BackendApiService _backendApiService;

        public Subscriber_Fetch(BackendApiService apiService)
        {
            InitializeComponent();
            _backendApiService = apiService ?? throw new ArgumentNullException(nameof(apiService));

            // Assign event handlers (ensure these match designer)
            this.Clear_n_Fetch_Paid_BTN.Click += new System.EventHandler(this.Clear_n_Fetch_Paid_BTN_Click);
            this.Clear_n_Fetch_Trial_BTN.Click += new System.EventHandler(this.Clear_n_Fetch_Trial_BTN_Click);
            this.Clear_n_Fetch_OTH_Real_BTN.Click += new System.EventHandler(this.Clear_n_Fetch_OTH_Real_BTN_Click);
            this.Clear_n_Fetch_OTH_Virtual_BTN.Click += new System.EventHandler(this.Clear_n_Fetch_OTH_Virtual_BTN_Click);
            this.Extract_Paid_BTN.Click += new System.EventHandler(this.Extract_Paid_BTN_Click);
            this.Extract_Trial_BTN.Click += new System.EventHandler(this.Extract_Trial_BTN_Click);
            this.Extract_OTH_Real_BTN.Click += new System.EventHandler(this.Extract_OTH_Real_BTN_Click);
            this.Extract_OTH_Virtual_BTN.Click += new System.EventHandler(this.Extract_OTH_Virtual_BTN_Click);
        }

        // --- Helper Method to Fetch and Populate Grid ---
        private async Task FetchAndPopulate(string queueType, DataGridView targetGrid, string tokenColumnName, string targetColumnName)
        {
            if (!_backendApiService.IsUserLoggedIn)
            {
                MessageBox.Show("Operator session expired or not logged in. Please close and reopen the fetch window.", "Authentication Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!targetGrid.Columns.Contains(tokenColumnName) || !targetGrid.Columns.Contains(targetColumnName))
            {
                MessageBox.Show($"Required columns not found in the grid for '{queueType}'. Expected '{tokenColumnName}' and '{targetColumnName}'. Check column Names in the designer.", "Grid Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            targetGrid.Rows.Clear();
            this.Cursor = Cursors.WaitCursor;
            string fetchError = null;
            int fetchedCount = 0;

            try
            {
                List<TradingQueueItemDto> items = await _backendApiService.FetchTradingQueueAsync(queueType);

                if (items != null)
                {
                    fetchedCount = items.Count;
                    targetGrid.SuspendLayout();

                    foreach (var item in items)
                    {
                        try
                        {
                            int rowIdx = targetGrid.Rows.Add();
                            DataGridViewRow row = targetGrid.Rows[rowIdx];
                            row.Cells[tokenColumnName].Value = item.ApiTokenValue;
                            row.Cells[targetColumnName].Value = item.ProfitTargetAmount;
                        }
                        catch (Exception rowEx)
                        {
                            Console.WriteLine($"Error adding row: {rowEx.Message}");
                        }
                    }
                    targetGrid.ResumeLayout();
                }
                else
                {
                    fetchError = $"Failed to fetch items for '{queueType}'.";
                }
            }
            catch (Exception ex)
            {
                fetchError = $"Error fetching '{queueType}': {ex.Message}";
                Console.WriteLine($"EXCEPTION: {ex}");
            }
            finally
            {
                this.Cursor = Cursors.Default;
                if (fetchError != null)
                    MessageBox.Show(fetchError, "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                else
                    Console.WriteLine($"Fetched {fetchedCount} items for '{queueType}'.");
            }
        }

        // --- Helper Method to Extract Data from a Grid ---
        private List<CredentialsWithTarget> ExtractDataFromGrid(DataGridView sourceGrid, string tokenColumnName, string targetColumnName)
        {
            List<CredentialsWithTarget> extractedData = new List<CredentialsWithTarget>();
            foreach (DataGridViewRow row in sourceGrid.Rows)
            {
                if (row.IsNewRow) continue;

                object tokenValueObj = row.Cells[tokenColumnName].Value;
                object targetValueObj = row.Cells[targetColumnName].Value;

                if (tokenValueObj == null || targetValueObj == null)
                {
                    continue;
                }

                string apiToken = tokenValueObj.ToString();
                decimal profitTarget;

                if (decimal.TryParse(targetValueObj.ToString(), out profitTarget))
                {
                    if (!string.IsNullOrEmpty(apiToken))
                    {
                        extractedData.Add(new CredentialsWithTarget { ApiTokenValue = apiToken, ProfitTarget = profitTarget });
                    }
                }
                else
                {
                    MessageBox.Show($"Row {row.Index + 1}: Invalid profit target format ('{targetValueObj}'). Skipping.", "Extract Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            return extractedData;
        }

        // --- Helper Method for Extracting and Loading ---
        private void ExtractAndLoad(DataGridView sourceGrid, string tokenColumnName, string targetColumnName)
        {
            const string hardcodedAppId = "70216"; // Hardcoded AppId
            List<CredentialsWithTarget> extractedData = new List<CredentialsWithTarget>();

            foreach (DataGridViewRow row in sourceGrid.Rows)
            {
                if (row.IsNewRow) continue;

                object tokenValueObj = row.Cells[tokenColumnName].Value;
                object targetValueObj = row.Cells[targetColumnName].Value;

                if (tokenValueObj == null || targetValueObj == null)
                {
                    continue;
                }

                string apiToken = tokenValueObj.ToString();
                decimal profitTarget;

                if (decimal.TryParse(targetValueObj.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out profitTarget))
                {
                    if (!string.IsNullOrEmpty(apiToken))
                    {
                        extractedData.Add(new CredentialsWithTarget
                        {
                            ApiTokenValue = apiToken,
                            ProfitTarget = profitTarget,
                            AppId = hardcodedAppId
                        });
                    }
                }
                else
                {
                    MessageBox.Show($"Row {row.Index + 1}: Invalid profit target format ('{targetValueObj}'). Skipping.", "Extract Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            if (extractedData.Count == 0)
            {
                MessageBox.Show("No valid data extracted from the grid.", "Extract Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (this.Owner is FxWorth mainForm)
            {
                mainForm.Main_Token_Table.Rows.Clear();

                var keysToRemove = mainForm.Storage.Clients.Keys.ToList();

                foreach (var data in extractedData)
                {
                    // --- Modification Start ---
                    var newCreds = new Credentials
                    {
                        Token = data.ApiTokenValue,
                        AppId = data.AppId,
                        ProfitTarget = data.ProfitTarget,
                        IsChecked = true, 
                        Name = data.AppId 
                    };

                    if (!mainForm.Storage.Add(newCreds))
                    {
                        Console.WriteLine($"Warning: Could not add token {newCreds.Token}, might be duplicate.");
                        continue;
                    }

                    mainForm.Main_Token_Table.Rows.Add(newCreds.IsChecked, newCreds.Token, newCreds.AppId, null, null, "Standby");
                }

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Could not find the main application window.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        // --- Event Handlers for "Clear n Fetch" Buttons ---
        private async void Clear_n_Fetch_Paid_BTN_Click(object sender, EventArgs e)
        {
            await FetchAndPopulate("paid", dataGridView_Paid, "Api_Tokens_Paid", "Profit_Targets_Paid");
        }

        private async void Clear_n_Fetch_Trial_BTN_Click(object sender, EventArgs e)
        {
            await FetchAndPopulate("virtual-trial", dataGridView_Trial, "Api_Tokens_Trial", "Profit_Targets_Trial");
        }

        private async void Clear_n_Fetch_OTH_Real_BTN_Click(object sender, EventArgs e)
        {
            await FetchAndPopulate("free-real", dataGridView_OTH_Real, "OTH_CR_Api_Tokens", "OTH_CR_Profit_Targets");
        }

        private async void Clear_n_Fetch_OTH_Virtual_BTN_Click(object sender, EventArgs e)
        {
            await FetchAndPopulate("free-virtual", dataGridView_OTH_Virtual, "OTH_VR_Api_Tokens", "OTH_VR_Profit_Targets");
        }

        // --- Event Handlers for "Extract" Buttons ---
        private void Extract_Paid_BTN_Click(object sender, EventArgs e)
        {
            ExtractAndLoad(dataGridView_Paid, "Api_Tokens_Paid", "Profit_Targets_Paid");
        }

        private void Extract_Trial_BTN_Click(object sender, EventArgs e)
        {
            ExtractAndLoad(dataGridView_Trial, "Api_Tokens_Trial", "Profit_Targets_Trial");
        }

        private void Extract_OTH_Real_BTN_Click(object sender, EventArgs e)
        {
            ExtractAndLoad(dataGridView_OTH_Real, "OTH_CR_Api_Tokens", "OTH_CR_Profit_Targets");
        }

        private void Extract_OTH_Virtual_BTN_Click(object sender, EventArgs e)
        {
            ExtractAndLoad(dataGridView_OTH_Virtual, "OTH_VR_Api_Tokens", "OTH_VR_Profit_Targets");
        }
    }

    public class CredentialsWithTarget
    {
        public string ApiTokenValue { get; set; }
        public decimal ProfitTarget { get; set; }
        public string AppId { get; set; }
    }
}