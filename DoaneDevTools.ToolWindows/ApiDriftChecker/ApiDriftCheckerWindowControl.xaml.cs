using System.Windows.Controls;

namespace DoaneDevTools.ToolWindows.ApiDriftChecker
{
    public partial class ApiDriftCheckerWindowControl : UserControl
    {
        public ApiDriftCheckerWindowControl()
        {
            InitializeComponent();
            DataContext = new ApiDriftCheckerViewModel();
        }
    }
}
