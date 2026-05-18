using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace DoaneDevTools.ToolWindows.ApiDriftChecker
{
    /// <summary>Tool window for detecting drift between C# API controllers and OpenAPI specs.</summary>
    [Guid("b1c2d3e4-0006-0006-0006-000000000006")]
    public class ApiDriftCheckerWindow : ToolWindowPane
    {
        public ApiDriftCheckerWindow() : base(null)
        {
            Caption = "API Drift Checker";
            Content = new ApiDriftCheckerWindowControl();
        }
    }
}
