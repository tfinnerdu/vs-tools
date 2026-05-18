using System;

namespace DoaneDevTools.ToolWindows.TodoDashboard.Models
{
    /// <summary>
    /// Represents a single TODO / FIXME / HACK / NOTE comment found in the solution.
    /// </summary>
    public class TodoItem
    {
        /// <summary>Tag type: TODO, FIXME, HACK, or NOTE.</summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>The comment text that follows the tag.</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Absolute path of the file containing the comment.</summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>1-based line number within the file.</summary>
        public int LineNumber { get; set; }

        /// <summary>
        /// Commit date from <c>git blame</c>, or null if git is unavailable / the
        /// file is untracked.
        /// </summary>
        public DateTime? CommitDate { get; set; }

        /// <summary>Author name from git blame, or null.</summary>
        public string? Author { get; set; }

        /// <summary>
        /// Age in calendar days.  Calculated from <see cref="CommitDate"/> when
        /// available; otherwise from the file's last-write time.
        /// </summary>
        public int AgeDays { get; set; }

        /// <summary>True when the item is more than 30 days old.</summary>
        public bool IsOld => AgeDays > 30;

        /// <summary>True when the item is more than 90 days old.</summary>
        public bool IsVeryOld => AgeDays > 90;

        /// <summary>Short file name without directory path, used in the grid.</summary>
        public string FileName => System.IO.Path.GetFileNameWithoutExtension(FilePath);

        /// <summary>Age label: "&lt;1d", "3d", "47d ⚠", "120d ⚠⚠".</summary>
        public string AgeLabel
        {
            get
            {
                if (AgeDays == 0) return "<1d";
                var label = $"{AgeDays}d";
                if (IsVeryOld) return label + " ⚠⚠";
                if (IsOld)     return label + " ⚠";
                return label;
            }
        }

        public override string ToString() =>
            $"[{Type}] {Message}  ({FileName}:{LineNumber})";
    }
}
