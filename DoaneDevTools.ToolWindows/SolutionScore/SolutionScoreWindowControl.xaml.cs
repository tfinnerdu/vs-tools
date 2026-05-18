using System.Windows.Controls;

namespace DoaneDevTools.ToolWindows.SolutionScore
{
    public partial class SolutionScoreWindowControl : UserControl
    {
        public SolutionScoreWindowControl()
        {
            InitializeComponent();
            DataContext = new SolutionScoreViewModel();
        }
    }
}
