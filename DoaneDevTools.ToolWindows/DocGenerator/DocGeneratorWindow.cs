using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace DoaneDevTools.ToolWindows.DocGenerator
{
    /// <summary>
    /// Tool window pane that hosts the Documentation Generator UI.
    /// Generates XML doc comments for undocumented members via Roslyn and
    /// produces a static HTML documentation site from the project's XML output.
    /// </summary>
    [Guid("e5f6a7b8-0006-0006-0006-000000000006")]
    public class DocGeneratorWindow : ToolWindowPane
    {
        public DocGeneratorWindow() : base(null)
        {
            Caption = "Documentation Generator";
            Content = new DocGeneratorWindowControl();
        }
    }
}
