using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace DoaneDevTools.ToolWindows.SolutionScore
{
    /// <summary>Dashboard showing solution health as a composite 0-100 score with trend.</summary>
    [Guid("b1c2d3e4-0007-0007-0007-000000000007")]
    public class SolutionScoreWindow : ToolWindowPane
    {
        public SolutionScoreWindow() : base(null)
        {
            Caption = "Solution Health Score";
            Content = new SolutionScoreWindowControl();
        }
    }
}
