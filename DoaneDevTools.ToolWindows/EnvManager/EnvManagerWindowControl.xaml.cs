using System.Windows.Controls;

namespace DoaneDevTools.ToolWindows.EnvManager
{
    /// <summary>
    /// Code-behind for <see cref="EnvManagerWindowControl"/>.
    /// All logic lives in <see cref="EnvManagerViewModel"/>;
    /// this file only sets the DataContext on construction.
    /// </summary>
    public partial class EnvManagerWindowControl : UserControl
    {
        public EnvManagerWindowControl()
        {
            InitializeComponent();
            DataContext = new EnvManagerViewModel();
        }
    }
}
