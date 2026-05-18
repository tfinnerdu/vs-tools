using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DoaneDevTools.ToolWindows.TodoDashboard.Models;
using LibGit2Sharp;

namespace DoaneDevTools.ToolWindows.TodoDashboard.Services
{
    /// <summary>
    /// Scans source files in a directory tree for TODO / FIXME / HACK / NOTE comments.
    ///
    /// Supported file extensions: .cs, .py, .ts, .js, .xaml
    ///
    /// Supported comment prefixes:
    ///   C-style:     //  TODO ...   //  FIXME ...   //  HACK ...   //  NOTE ...
    ///   Python/ps1:  #   TODO ...   #   FIXME ...
    ///   XML/XAML:    &lt;!-- TODO ... --&gt;
    ///
    /// When the directory is inside a git repository, LibGit2Sharp is used to
    /// retrieve the commit date and author for each matched line via git blame.
    /// </summary>
    public class TodoScannerService
    {
        // -----------------------------------------------------------------------
        // Patterns
        // -----------------------------------------------------------------------

        private static readonly string[] _extensions =
            { ".cs", ".py", ".ts", ".js", ".xaml", ".ps1" };

        // Matches: // TODO: message  |  # FIXME message  |  <!-- NOTE: msg -->
        // Groups: tag, message
        private static readonly Regex _todoPattern = new Regex(
            @"(?://|#|<!--|/\*)\s*(?<tag>TODO|FIXME|HACK|NOTE)\s*[:\-]?\s*(?<msg>[^\r\n*/>]*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Scans all supported files under <paramref name="rootDirectory"/> and
        /// returns a list of <see cref="TodoItem"/> records.
        /// </summary>
        public List<TodoItem> ScanDirectory(string rootDirectory)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentNullException(nameof(rootDirectory));
            if (!Directory.Exists(rootDirectory))
                return new List<TodoItem>();

            var results = new List<TodoItem>();

            // Discover the git repo root (may be null if not a git repository).
            string? repoRoot = TryFindGitRoot(rootDirectory);

            // Walk all files matching supported extensions.
            var files = Directory
                .EnumerateFiles(rootDirectory, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                {
                    var ext = Path.GetExtension(f).ToLowerInvariant();
                    if (!_extensions.Contains(ext)) return false;

                    // Skip bin / obj / node_modules / .git / .venv
                    var parts = f.Split(Path.DirectorySeparatorChar,
                                        Path.AltDirectorySeparatorChar);
                    return !parts.Any(p =>
                        p.Equals("bin",          StringComparison.OrdinalIgnoreCase) ||
                        p.Equals("obj",          StringComparison.OrdinalIgnoreCase) ||
                        p.Equals("node_modules", StringComparison.OrdinalIgnoreCase) ||
                        p.StartsWith(".git",     StringComparison.OrdinalIgnoreCase) ||
                        p.Equals(".venv",        StringComparison.OrdinalIgnoreCase) ||
                        p.Equals("__pycache__",  StringComparison.OrdinalIgnoreCase));
                });

            foreach (var filePath in files)
            {
                try
                {
                    results.AddRange(ScanFile(filePath, repoRoot));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[TodoScanner] Error scanning {filePath}: {ex.Message}");
                }
            }

            return results.OrderByDescending(i => i.AgeDays).ThenBy(i => i.FilePath).ToList();
        }

        /// <summary>Scans a single file and returns matching TODO items.</summary>
        public List<TodoItem> ScanFile(string filePath, string? repoRoot = null)
        {
            var lines   = File.ReadAllLines(filePath);
            var results = new List<TodoItem>();

            for (int i = 0; i < lines.Length; i++)
            {
                var match = _todoPattern.Match(lines[i]);
                if (!match.Success) continue;

                var tag = match.Groups["tag"].Value.ToUpperInvariant();
                var msg = match.Groups["msg"].Value.Trim();

                var item = new TodoItem
                {
                    Type       = tag,
                    Message    = msg,
                    FilePath   = filePath,
                    LineNumber = i + 1,   // 1-based
                };

                // Enrich with git blame if possible.
                if (repoRoot != null)
                    TryEnrichFromGitBlame(item, repoRoot);

                // Fall back to file modification date if blame is unavailable.
                if (item.CommitDate == null)
                {
                    var lastWrite = File.GetLastWriteTimeUtc(filePath);
                    item.CommitDate = lastWrite;
                    item.AgeDays    = (int)(DateTime.UtcNow - lastWrite).TotalDays;
                }

                results.Add(item);
            }

            return results;
        }

        // -----------------------------------------------------------------------
        // Git blame integration
        // -----------------------------------------------------------------------

        private static string? TryFindGitRoot(string startDir)
        {
            try
            {
                return Repository.Discover(startDir);
            }
            catch
            {
                return null;
            }
        }

        private static void TryEnrichFromGitBlame(TodoItem item, string repoRoot)
        {
            try
            {
                using var repo = new Repository(repoRoot);

                // Get the path relative to the repo working directory.
                var workDir    = repo.Info.WorkingDirectory;
                var relativePath = Path.GetRelativePath(workDir, item.FilePath)
                                       .Replace('\\', '/');

                // Run git blame for the specific line.
                var blame = repo.Blame(relativePath, new BlameOptions
                {
                    StartingAt = repo.Head.Tip
                });

                if (blame == null) return;

                // Find the hunk containing our 1-based line number.
                foreach (var hunk in blame)
                {
                    int hunkStart = hunk.InitialStartLineNumber + 1;  // LibGit2Sharp is 0-based
                    int hunkEnd   = hunkStart + hunk.LineCount - 1;

                    if (item.LineNumber < hunkStart || item.LineNumber > hunkEnd)
                        continue;

                    var sig = hunk.FinalCommit?.Author ?? hunk.InitialCommit?.Author;
                    if (sig == null) break;

                    item.CommitDate = sig.When.UtcDateTime;
                    item.Author     = sig.Name;
                    item.AgeDays    = (int)(DateTime.UtcNow - item.CommitDate.Value).TotalDays;
                    break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TodoScanner] git blame failed for {item.FilePath}:{item.LineNumber} — {ex.Message}");
            }
        }
    }
}
