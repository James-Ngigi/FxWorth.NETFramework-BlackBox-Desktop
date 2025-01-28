using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace FxWorth
{
    public partial class Layer_Configuration : Form
    {
        private readonly FxWorth mainForm;

        public decimal? InitialStake { get; set; }
        public int? MartingaleLevel { get; set; }
        public decimal? MaxDrawdown { get; set; }
        public decimal? BarrierOffset { get; set; }
        public int? HierarchyLevels { get; set; }


        public Layer_Configuration(FxWorth mainForm)
        {
            InitializeComponent();
            this.mainForm = mainForm;
        }

        public ComboBox LayerComboBox => Select_Layer_CMBX;

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Initialize controls with potentially null values
            Stake_TXT3.Value = InitialStake.HasValue ? InitialStake.Value : 1;
            Martingale_Level_TXT3.Value = MartingaleLevel.HasValue ? MartingaleLevel.Value : 1;
            Max_Drawdown_TXT3.Value = MaxDrawdown.HasValue ? MaxDrawdown.Value : 1000;
            Barrier_Offset_TXT3.Value = BarrierOffset.HasValue ? BarrierOffset.Value : 30;
            Hierarchy_Levels_TXT2.Value = HierarchyLevels.HasValue ? HierarchyLevels.Value : 2;

        }

        public CustomLayerConfig SaveCustomLayerConfig()
        {
            int selectedLayer = (int)Select_Layer_CMBX.SelectedItem;

            return new CustomLayerConfig
            {
                LayerNumber = selectedLayer,
                HierarchyLevels = (int?)Hierarchy_Levels_TXT2.Value,
                InitialStake = Stake_TXT3.Value,
                MartingaleLevel = (int?)Martingale_Level_TXT3.Value,
                MaxDrawdown = Max_Drawdown_TXT3.Value,
                BarrierOffset = Barrier_Offset_TXT3.Value
            };
        }

        private void Load_Layer_Config_BTN_Click(object sender, EventArgs e)
        {
            CustomLayerConfig config = SaveCustomLayerConfig();

            // Access customLayerConfigs through the mainForm instance
            mainForm.customLayerConfigs[config.LayerNumber] = config;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}