using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace DoaneDevTools.ToolWindows.AssemblyInspector
{
    /// <summary>
    /// Tool window pane that hosts the Assembly Inspector UI.
    /// Allows developers to browse, search, and decompile types and methods
    /// from any managed assembly referenced in the current solution.
    /// </summary>
    [Guid("a2b3c4d5-0003-0003-0003-000000000003")]
    public class AssemblyInspectorWindow : ToolWindowPane
    {
        public AssemblyInspectorWindow() : base(null)
        {
            Caption = "Assembly Inspector";
            Content = new AssemblyInspectorWindowControl();
        }
    }
}
