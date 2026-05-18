namespace DoaneDevTools
{
    /// <summary>
    /// Integer command-ID constants that correspond to the IDSymbol entries in
    /// <c>DoaneDevToolsCommands.vsct</c>.
    ///
    /// IDs are grouped by category.  The high byte is reserved for future
    /// command-group expansion (menus start at 0x0000, commands at 0x0100).
    /// Every constant here must have a matching IDSymbol in the VSCT file.
    /// </summary>
    internal static class PackageIds
    {
        // ----------------------------------------------------------------
        // Top-level menu group inserted into the VS "Tools" menu
        // ----------------------------------------------------------------

        /// <summary>ID of the DoaneDevTools group placed inside the VS Tools menu.</summary>
        public const int DoaneDevToolsMenuGroup = 0x0000;

        /// <summary>ID of the "Doane Dev Tools" submenu node.</summary>
        public const int DoaneDevToolsSubMenu = 0x0010;

        /// <summary>ID of the command group that lives inside the submenu.</summary>
        public const int DoaneDevToolsSubMenuGroup = 0x0020;

        // ----------------------------------------------------------------
        // Command IDs — sequential from 0x0100
        // ----------------------------------------------------------------

        /// <summary>Opens the Environment Manager tool window.</summary>
        public const int OpenEnvManagerCommandId = 0x0100;

        /// <summary>Opens the Template Builder tool window.</summary>
        public const int OpenTemplateBuilderCommandId = 0x0101;

        /// <summary>Opens the TODO Dashboard tool window.</summary>
        public const int OpenTodoDashboardCommandId = 0x0102;

        /// <summary>Opens the Dependency Map tool window.</summary>
        public const int OpenDependencyMapCommandId = 0x0103;

        /// <summary>Opens the Assembly Inspector tool window.</summary>
        public const int OpenAssemblyInspectorCommandId = 0x0104;

        /// <summary>Opens the CSS Layout Inspector tool window.</summary>
        public const int OpenCssInspectorCommandId = 0x0105;

        /// <summary>Opens the Test Data Seeder tool window.</summary>
        public const int OpenTestDataSeederCommandId = 0x0106;

        /// <summary>Opens the API Drift Checker tool window.</summary>
        public const int OpenApiDriftCheckerCommandId = 0x0107;

        /// <summary>Opens the Solution Health Score tool window.</summary>
        public const int OpenSolutionScoreCommandId = 0x0108;

        /// <summary>Opens the Code Query (Roslyn Scripting) tool window.</summary>
        public const int OpenCodeQueryCommandId = 0x0109;
    }
}
