using System;
using System.Collections.Generic;
using System.Windows.Forms;
using FxBackendClient;
using System.Threading.Tasks;

namespace FxWorth
{
    public partial class Subscriber_Fetch : Form
    {
        private readonly BackendApiService _backendApiService;

        public Subscriber_Fetch(BackendApiService apiService)
        {
            InitializeComponent();
            _backendApiService = apiService ?? throw new ArgumentNullException(nameof(apiService));

            // Optional: Add event handlers
        }

        // --- Helper Method to Fetch and Populate Grid ---
        private async Task FetchAndPopulate(string queueType, DataGridView targetGrid)
        {
            targetGrid.Rows.Clear();
            this.Cursor = Cursors.WaitCursor;
            string fetchError = null;

            try
            {
                // Use the stored _backendApiService instance
                List<TradingQueueItemDto> items = await _backendApiService.FetchTradingQueueAsync(queueType);

                if (items != null)
                {
                    foreach (var item in items)
                    {
                        // Add items to the target grid
                        // Assumes columns are: 0=ApiTokenValue, 1=ProfitTargetAmount
                        targetGrid.Rows.Add(item.ApiTokenValue, item.ProfitTargetAmount);
                    }
                    if (fetchError == null)
                    {
                        Console.WriteLine($"Fetched {items.Count} items for '{queueType}'.");
                    }
                }
                else
                {
                    fetchError = $"Failed to fetch items for '{queueType}'. Backend returned null or error.";
                }
            }
            catch (Exception ex)
            {
                fetchError = $"Error during fetch for '{queueType}': {ex.Message}";
                Console.WriteLine($"EXCEPTION during fetch for '{queueType}': {ex}");
            }
            finally
            {
                this.Cursor = Cursors.Default;
                if (fetchError != null)
                {
                    MessageBox.Show(fetchError, "Fetch Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        // --- Helper Method to Extract Data from a Grid ---
        private List<CredentialsWithTarget> ExtractDataFromGrid(DataGridView sourceGrid)
        {
            List<CredentialsWithTarget> extractedData = new List<CredentialsWithTarget>();

            foreach (DataGridViewRow row in sourceGrid.Rows)
            {
                if (row.IsNewRow) continue;
                string apiToken = row.Cells[0].Value?.ToString();
                decimal profitTarget;

                object tokenValueObj = row.Cells[0].Value;
                object targetValueObj = row.Cells[1].Value;

                if (tokenValueObj == null || targetValueObj == null)
                {
                    MessageBox.Show($"Row {row.Index + 1}: Skipping row with missing data.", "Extract Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    continue;
                }

                apiToken = tokenValueObj.ToString();


                if (decimal.TryParse(targetValueObj.ToString(), out profitTarget))
                {
                    if (!string.IsNullOrEmpty(apiToken))
                    {
                        extractedData.Add(new CredentialsWithTarget { ApiTokenValue = apiToken, ProfitTarget = profitTarget });
                    }
                    else
                    {
                        MessageBox.Show($"Row {row.Index + 1}: Skipping row with empty API Token.", "Extract Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                else
                {
                    MessageBox.Show($"Row {row.Index + 1}: Invalid profit target format ('{targetValueObj}'). Skipping.", "Extract Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            return extractedData;
        }

        // --- Event Handlers for "Clear n Fetch" Buttons ---

        private async void Clear_n_Fetch_Paid_BTN_Click(object sender, EventArgs e)
        {
            await FetchAndPopulate("paid", dataGridView_Paid);
        }

        private async void Clear_n_Fetch_Trial_BTN_Click(object sender, EventArgs e)
        {
            await FetchAndPopulate("virtual-trial", dataGridView_Trial);
        }

        private async void Clear_n_Fetch_OTH_Real_BTN_Click(object sender, EventArgs e)
        {
            await FetchAndPopulate("free-real", dataGridView_OTH_Real);
        }

        private async void Clear_n_Fetch_OTH_Virtual_BTN_Click(object sender, EventArgs e)
        {
            await FetchAndPopulate("free-virtual", dataGridView_OTH_Virtual);
        }

        // --- Event Handlers for "Extract" Buttons ---

        private void Extract_Paid_BTN_Click(object sender, EventArgs e)
        {
            ExtractAndLoad(dataGridView_Paid);
        }

        private void Extract_Trial_BTN_Click(object sender, EventArgs e)
        {
            ExtractAndLoad(dataGridView_Trial);
        }

        private void Extract_OTH_Real_BTN_Click(object sender, EventArgs e)
        {
            ExtractAndLoad(dataGridView_OTH_Real);
        }

        private void Extract_OTH_Virtual_BTN_Click(object sender, EventArgs e)
        {
            ExtractAndLoad(dataGridView_OTH_Virtual);
        }

        // --- Helper Method for Extracting and Loading ---
        private void ExtractAndLoad(DataGridView sourceGrid)
        {
            List<CredentialsWithTarget> extractedData = ExtractDataFromGrid(sourceGrid);

            if (extractedData.Count == 0)
            {
                MessageBox.Show("No valid data extracted from the grid.", "Extract Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (this.Owner is FxWorth mainForm)
            {
                mainForm.LoadTokensFromFetch(extractedData); 
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Could not find the main application window to load tokens.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
    // --- Helper Class for Credentials and Target ---
    public class CredentialsWithTarget
    {
        public string ApiTokenValue { get; set; }
        public decimal ProfitTarget { get; set; }
        // Add other fields if needed, e.g., Name, ID from DTO?
    }
}