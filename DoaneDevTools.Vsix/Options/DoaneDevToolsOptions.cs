using System.ComponentModel;
using Microsoft.VisualStudio.Shell;

namespace DoaneDevTools.Options
{
    public class DoaneDevToolsOptions : DialogPage
    {
        [Category("Git Blame Margin")]
        [DisplayName("Show Git Blame Margin")]
        [Description("Display author and date for each line in a left editor margin.")]
        [DefaultValue(true)]
        public bool ShowGitBlameMargin { get; set; } = true;

        [Category("Performance Hotspot Marker")]
        [DisplayName("Highlight High-Complexity Methods")]
        [Description("Highlight method names that exceed cyclomatic complexity thresholds.")]
        [DefaultValue(true)]
        public bool HighlightHotspots { get; set; } = true;

        [Category("Performance Hotspot Marker")]
        [DisplayName("Warning Threshold")]
        [Description("CC at which the warning (yellow) highlight is applied.")]
        [DefaultValue(11)]
        public int HotspotWarningThreshold { get; set; } = 11;

        [Category("Performance Hotspot Marker")]
        [DisplayName("Critical Threshold")]
        [Description("CC at which the critical (red) highlight is applied.")]
        [DefaultValue(16)]
        public int HotspotCriticalThreshold { get; set; } = 16;
    }
}
