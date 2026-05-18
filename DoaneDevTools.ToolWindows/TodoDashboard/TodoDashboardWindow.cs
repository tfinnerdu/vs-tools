using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace DoaneDevTools.ToolWindows.TodoDashboard
{
    /// <summary>
    /// Tool window pane that hosts the TODO Dashboard.
    /// Registered in <c>DoaneDevToolsPackage</c> docked next to the Error List.
    /// </summary>
    [Guid("d3e4f5a6-0003-0003-0003-000000000003")]
    public class TodoDashboardWindow : ToolWindowPane
    {
        public TodoDashboardWindow() : base(null)
        {
            Caption = "TODO Dashboard";
            Content = new TodoDashboardWindowControl();
        }
    }
}
