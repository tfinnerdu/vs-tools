using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace DoaneDevTools.ToolWindows.CodeQuery
{
    /// <summary>
    /// Interactive Roslyn Scripting query surface — the CQLinq equivalent.
    /// Write C# LINQ queries over the live Roslyn compilation:
    ///   from m in Methods where m.Parameters.Length > 5 select m.Name
    /// </summary>
    [Guid("b1c2d3e4-0008-0008-0008-000000000008")]
    public class CodeQueryWindow : ToolWindowPane
    {
        public CodeQueryWindow() : base(null)
        {
            Caption = "Code Query (Roslyn)";
            Content = new CodeQueryWindowControl();
        }
    }
}
