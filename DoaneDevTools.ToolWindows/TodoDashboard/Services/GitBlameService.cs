using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DoaneDevTools.ToolWindows.TodoDashboard.Services
{
    /// <summary>Retrieves git blame data for file lines using LibGit2Sharp.</summary>
    public class GitBlameService
    {
        private readonly string _repoPath;

        public GitBlameService(string repoPath)
        {
            _repoPath = repoPath;
        }

        /// <summary>
        /// Returns blame info for specific line numbers in a file.
        /// Returns null if the repo can't be opened or LibGit2Sharp is unavailable.
        /// </summary>
        public Dictionary<int, BlameEntry>? GetBlameLinesForFile(string filePath, IEnumerable<int> lineNumbers)
        {
            try
            {
                // LibGit2Sharp reference — gracefully degrade if not available at runtime
                return GetBlameLinesInternal(filePath, lineNumbers);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private Dictionary<int, BlameEntry> GetBlameLinesInternal(string filePath, IEnumerable<int> lineNumbers)
        {
            var result = new Dictionary<int, BlameEntry>();
            var lineSet = new HashSet<int>(lineNumbers);

            using var repo = new LibGit2Sharp.Repository(_repoPath);

            var relativePath = GetRelativePath(filePath, _repoPath);
            var blame = repo.Blame(relativePath);

            foreach (var hunk in blame)
            {
                for (int i = 0; i < hunk.LineCount; i++)
                {
                    var lineNum = hunk.FinalStartLineNumber + i + 1; // 1-based
                    if (!lineSet.Contains(lineNum)) continue;

                    result[lineNum] = new BlameEntry
                    {
                        Author = hunk.FinalCommit?.Author?.Name ?? "Unknown",
                        Email = hunk.FinalCommit?.Author?.Email ?? string.Empty,
                        CommitDate = hunk.FinalCommit?.Author?.When.DateTime ?? DateTime.MinValue,
                        CommitSha = hunk.FinalCommit?.Sha?.Substring(0, 7) ?? string.Empty,
                        CommitMessage = hunk.FinalCommit?.MessageShort ?? string.Empty
                    };
                }
            }

            return result;
        }

        private static string GetRelativePath(string fullPath, string basePath)
        {
            var fileUri = new Uri(fullPath);
            var baseUri = new Uri(basePath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar);
            return Uri.UnescapeDataString(baseUri.MakeRelativeUri(fileUri).ToString()
                .Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>Finds the root of the git repository containing the given path.</summary>
        public static string? FindRepoRoot(string startPath)
        {
            var dir = Directory.Exists(startPath) ? startPath : Path.GetDirectoryName(startPath);
            while (!string.IsNullOrEmpty(dir))
            {
                if (Directory.Exists(Path.Combine(dir, ".git")))
                    return dir;
                dir = Path.GetDirectoryName(dir);
            }
            return null;
        }
    }

    public class BlameEntry
    {
        public string Author { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CommitDate { get; set; }
        public string CommitSha { get; set; } = string.Empty;
        public string CommitMessage { get; set; } = string.Empty;
        public int AgeDays => (int)(DateTime.UtcNow - CommitDate).TotalDays;
    }
}
