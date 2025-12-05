using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Resources.Extensions;

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

            Stake_TXT3.Value = InitialStake.HasValue?Math.Max(Stake_TXT3.Minimum, Math.Min(Stake_TXT3.Maximum, InitialStake.Value)):Stake_TXT3.Minimum;
            Martingale_Level_TXT3.Value = MartingaleLevel.HasValue ? MartingaleLevel.Value : 1;
            Max_Drawdown_TXT3.Value = MaxDrawdown.HasValue ? MaxDrawdown.Value : 1000;  //Default value of 1000
            Barrier_Offset_TXT3.Value = BarrierOffset.HasValue ? BarrierOffset.Value : 0;
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
        }
    }
}