using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace DoaneDevTools.ToolWindows.DependencyMap
{
    /// <summary>
    /// Tool window pane that hosts the Dependency Map UI.
    /// Visualises type/namespace/assembly dependency graphs derived from
    /// Roslyn analysis of the current solution, and detects circular dependencies.
    /// </summary>
    [Guid("c3d4e5f6-0009-0009-0009-000000000009")]
    public class DependencyMapWindow : ToolWindowPane
    {
        public DependencyMapWindow() : base(null)
        {
            Caption = "Dependency Map";
            Content = new DependencyMapWindowControl();
        }
    }
}
