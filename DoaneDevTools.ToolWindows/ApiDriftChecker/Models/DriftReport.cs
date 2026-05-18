using System.Collections.Generic;
using System.ComponentModel;

namespace DoaneDevTools.ToolWindows.ApiDriftChecker.Models
{
    public enum DriftStatus
    {
        InSync,
        MissingFromSpec,
        MissingFromCode,
        ParameterMismatch,
        ResponseMismatch
    }

    public class DriftItem : INotifyPropertyChanged
    {
        public DriftStatus Status { get; set; }
        public string HttpMethod { get; set; } = string.Empty;
        public string Route { get; set; } = string.Empty;
        public string ControllerName { get; set; } = string.Empty;
        public string ActionName { get; set; } = string.Empty;
        public string? MismatchDetail { get; set; }
        public string FilePath { get; set; } = string.Empty;
        public int LineNumber { get; set; }

        public string StatusIcon => Status switch
        {
            DriftStatus.InSync => "✓",
            DriftStatus.MissingFromSpec => "✗",
            DriftStatus.MissingFromCode => "✗",
            DriftStatus.ParameterMismatch => "⚠",
            DriftStatus.ResponseMismatch => "⚠",
            _ => "?"
        };

        public string StatusColor => Status switch
        {
            DriftStatus.InSync => "#2E7D32",
            DriftStatus.MissingFromSpec => "#C62828",
            DriftStatus.MissingFromCode => "#C62828",
            DriftStatus.ParameterMismatch => "#E65100",
            DriftStatus.ResponseMismatch => "#E65100",
            _ => "#666666"
        };

        public string Description => Status switch
        {
            DriftStatus.InSync => "In sync",
            DriftStatus.MissingFromSpec => "Missing from spec",
            DriftStatus.MissingFromCode => "In spec but no matching controller action",
            DriftStatus.ParameterMismatch => $"Parameter mismatch: {MismatchDetail}",
            DriftStatus.ResponseMismatch => $"Response mismatch: {MismatchDetail}",
            _ => "Unknown"
        };

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class DriftReport
    {
        public string ControllerFile { get; set; } = string.Empty;
        public string SpecFile { get; set; } = string.Empty;
        public List<DriftItem> Items { get; set; } = new();
        public int InSyncCount => Items.Count(i => i.Status == DriftStatus.InSync);
        public int DriftCount => Items.Count - InSyncCount;
    }
}
