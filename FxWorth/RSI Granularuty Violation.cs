using System.Windows.Forms;

namespace FxWorth
{
    public partial class RSI_Close_Interval : Form
    {
        private readonly string indicatorName;
        private readonly string indicatorName2;

        public RSI_Close_Interval(string indicatorName, string indicatorName2)
        {
            this.indicatorName = indicatorName;
            this.indicatorName2 = indicatorName2;
            InitializeComponent();
        }
    }
}