using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DoaneDevTools.ToolWindows.TodoDashboard.Models;

namespace DoaneDevTools.ToolWindows.TodoDashboard.Services
{
    /// <summary>
    /// Exports <see cref="TodoItem"/> collections to CSV format.
    ///
    /// Column order: Type, Message, File, Line, Author, Age (days), CommitDate
    /// </summary>
    public class TodoExportService
    {
        // -----------------------------------------------------------------------
        // Public API
        // -----------------------------------------------------------------------

        /// <summary>
        /// Writes <paramref name="items"/> to a CSV file at <paramref name="outputPath"/>.
        /// Uses UTF-8 with BOM so Excel opens the file correctly without an import wizard.
        /// </summary>
        public void ExportToCsv(IEnumerable<TodoItem> items, string outputPath)
        {
            if (items == null) throw new ArgumentNullException(nameof(items));
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path must not be empty.", nameof(outputPath));

            // Ensure the parent directory exists.
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // UTF-8 with BOM so Excel auto-detects the encoding.
            using var writer = new StreamWriter(outputPath, append: false,
                encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

            // Header row.
            writer.WriteLine(
                "Type,Message,File,FullPath,Line,Author,Age (days),CommitDate");

            foreach (var item in items)
            {
                writer.WriteLine(BuildCsvRow(item));
            }
        }

        /// <summary>
        /// Returns the CSV content as a string (for clipboard / preview use cases).
        /// </summary>
        public string ToCsvString(IEnumerable<TodoItem> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Type,Message,File,FullPath,Line,Author,Age (days),CommitDate");

            foreach (var item in items ?? Enumerable.Empty<TodoItem>())
                sb.AppendLine(BuildCsvRow(item));

            return sb.ToString();
        }

        // -----------------------------------------------------------------------
        // Private helpers
        // -----------------------------------------------------------------------

        private static string BuildCsvRow(TodoItem item)
        {
            return string.Join(",", new[]
            {
                CsvEscape(item.Type),
                CsvEscape(item.Message),
                CsvEscape(Path.GetFileName(item.FilePath)),
                CsvEscape(item.FilePath),
                item.LineNumber.ToString(),
                CsvEscape(item.Author ?? string.Empty),
                item.AgeDays.ToString(),
                item.CommitDate.HasValue
                    ? item.CommitDate.Value.ToString("yyyy-MM-dd")
                    : string.Empty
            });
        }

        /// <summary>
        /// Escapes a value for inclusion in a CSV cell:
        /// wraps in double-quotes if the value contains a comma, double-quote, or newline;
        /// doubles any embedded double-quotes.
        /// </summary>
        private static string CsvEscape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            bool needsQuoting = value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            if (!needsQuoting) return value;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
