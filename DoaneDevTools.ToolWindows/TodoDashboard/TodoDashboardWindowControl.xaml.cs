using System.Windows.Controls;
using System.Windows.Input;
using DoaneDevTools.ToolWindows.TodoDashboard.Models;

namespace DoaneDevTools.ToolWindows.TodoDashboard
{
    /// <summary>
    /// Code-behind for <see cref="TodoDashboardWindowControl"/>.
    /// Logic lives in <see cref="TodoDashboardViewModel"/>.
    /// </summary>
    public partial class TodoDashboardWindowControl : UserControl
    {
        public TodoDashboardWindowControl()
        {
            InitializeComponent();
            DataContext = new TodoDashboardViewModel();
        }

        /// <summary>
        /// Double-click a grid row to navigate to the file/line in the VS editor.
        /// </summary>
        private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is TodoDashboardViewModel vm &&
                TodoGrid.SelectedItem is TodoItem item)
            {
                vm.NavigateToItemCommand.Execute(item);
            }
        }
    }
}
