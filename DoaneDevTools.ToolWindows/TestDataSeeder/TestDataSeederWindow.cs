using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace DoaneDevTools.ToolWindows.TestDataSeeder
{
    /// <summary>Tool window for generating FK-consistent test data in SQL Server databases.</summary>
    [Guid("b1c2d3e4-0005-0005-0005-000000000005")]
    public class TestDataSeederWindow : ToolWindowPane
    {
        public TestDataSeederWindow() : base(null)
        {
            Caption = "Test Data Seeder";
            Content = new TestDataSeederWindowControl();
        }
    }
}
