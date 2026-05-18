using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace DoaneDevTools.ToolWindows.EnvManager
{
    /// <summary>
    /// Tool window pane that hosts the Environment Manager UI.
    /// Registered in <c>DoaneDevToolsPackage</c> and invoked via
    /// <c>OpenEnvManagerCommand</c>.
    /// </summary>
    [Guid("b1c2d3e4-0001-0001-0001-000000000001")]
    public class EnvManagerWindow : ToolWindowPane
    {
        public EnvManagerWindow() : base(null)
        {
            Caption = "Environment Manager";
            Content = new EnvManagerWindowControl();
        }
    }
}
