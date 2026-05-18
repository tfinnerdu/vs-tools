using System.Windows.Controls;

namespace DoaneDevTools.ToolWindows.TestDataSeeder
{
    public partial class TestDataSeederWindowControl : UserControl
    {
        public TestDataSeederWindowControl()
        {
            InitializeComponent();
            DataContext = new TestDataSeederViewModel();
        }
    }
}
