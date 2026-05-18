using System.Windows.Controls;
using DoaneDevTools.ToolWindows.TemplateBuilder.Models;

namespace DoaneDevTools.ToolWindows.TemplateBuilder
{
    /// <summary>
    /// Code-behind for <see cref="TemplateBuilderWindowControl"/>.
    /// Logic lives in <see cref="TemplateBuilderViewModel"/>.
    /// </summary>
    public partial class TemplateBuilderWindowControl : UserControl
    {
        public TemplateBuilderWindowControl()
        {
            InitializeComponent();
            DataContext = new TemplateBuilderViewModel();
        }

        /// <summary>
        /// Handles TreeView selection changes and informs the ViewModel of the
        /// selected template.  The TreeView ItemTemplate uses a heterogeneous
        /// data model (category nodes vs leaf template nodes), so we inspect the
        /// selected item type here instead of binding directly.
        /// </summary>
        private void OnTemplateSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is TemplateBuilderViewModel vm && e.NewValue is TemplateTreeNode node)
                vm.SelectedTreeNode = node;
        }
    }
}
