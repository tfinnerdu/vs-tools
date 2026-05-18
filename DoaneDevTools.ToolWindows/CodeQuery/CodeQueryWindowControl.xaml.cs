using System.Windows.Controls;

namespace DoaneDevTools.ToolWindows.CodeQuery
{
    public partial class CodeQueryWindowControl : UserControl
    {
        public CodeQueryWindowControl()
        {
            InitializeComponent();
            DataContext = new CodeQueryViewModel();
        }
    }
}
