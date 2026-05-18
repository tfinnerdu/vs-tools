using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace DoaneDevTools.ToolWindows.CssInspector
{
    /// <summary>
    /// Tool window pane that hosts the CSS Layout Inspector UI.
    /// Provides a live WebView2 preview of HTML/CSS files from the current
    /// solution, with interactive element selection and live CSS editing.
    /// </summary>
    [Guid("d4e5f6a7-0005-0005-0005-000000000005")]
    public class CssInspectorWindow : ToolWindowPane
    {
        public CssInspectorWindow() : base(null)
        {
            Caption = "CSS Layout Inspector";
            Content = new CssInspectorWindowControl();
        }
    }
}
