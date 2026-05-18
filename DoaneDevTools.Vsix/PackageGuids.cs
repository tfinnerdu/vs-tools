using System;

namespace DoaneDevTools
{
    /// <summary>
    /// Centralised GUID constants shared between the VSCT file, the package class,
    /// and every command handler in the DoaneDevTools extension.
    ///
    /// Adding a new command group or package requires adding a corresponding entry
    /// here <em>and</em> in <see cref="PackageIds"/> before wiring up the VSCT.
    /// </summary>
    internal static class PackageGuids
    {
        // ----------------------------------------------------------------
        // Package GUID — must match [Guid] on DoaneDevToolsPackage and the
        // GuidSymbol/@value in DoaneDevToolsCommands.vsct.
        // ----------------------------------------------------------------

        /// <summary>String form of the DoaneDevTools VS package GUID.</summary>
        public const string DoaneDevToolsPackageGuidString = "a1b2c3d4-5e6f-7890-abcd-ef1234567890";

        /// <summary>Parsed <see cref="Guid"/> of the DoaneDevTools VS package.</summary>
        public static readonly Guid DoaneDevToolsPackageGuid = new Guid(DoaneDevToolsPackageGuidString);

        // ----------------------------------------------------------------
        // Command-set GUID — governs all commands defined in the VSCT file.
        // This GUID is used in the GuidSymbol block named
        // "guidDoaneDevToolsCommandSet".
        // ----------------------------------------------------------------

        /// <summary>String form of the DoaneDevTools command-set GUID.</summary>
        public const string DoaneDevToolsCommandSetGuidString = "a1b2c3d4-5e6f-7890-abcd-ef1234567891";

        /// <summary>Parsed <see cref="Guid"/> of the DoaneDevTools command set.</summary>
        public static readonly Guid DoaneDevToolsCommandSetGuid = new Guid(DoaneDevToolsCommandSetGuidString);

        // ----------------------------------------------------------------
        // Image catalog GUID (used by VSCT <Bitmaps> if custom icons are
        // added later via a BitmapStrip).
        // ----------------------------------------------------------------

        /// <summary>String form of the image-catalog GUID for DoaneDevTools toolbar icons.</summary>
        public const string DoaneDevToolsImageCatalogGuidString = "a1b2c3d4-5e6f-7890-abcd-ef1234567892";

        /// <summary>Parsed <see cref="Guid"/> of the DoaneDevTools image catalog.</summary>
        public static readonly Guid DoaneDevToolsImageCatalogGuid = new Guid(DoaneDevToolsImageCatalogGuidString);
    }
}
