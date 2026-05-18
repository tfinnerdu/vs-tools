using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace DoaneDevTools.ToolWindows.TemplateBuilder
{
    /// <summary>
    /// Tool window pane that hosts the Template Builder / scaffolding wizard.
    /// Registered in <c>DoaneDevToolsPackage</c> via <c>[ProvideToolWindow]</c>.
    /// </summary>
    [Guid("c2d3e4f5-0002-0002-0002-000000000002")]
    public class TemplateBuilderWindow : ToolWindowPane
    {
        public TemplateBuilderWindow() : base(null)
        {
            Caption = "Template Builder";
            Content = new TemplateBuilderWindowControl();
        }
    }
}
