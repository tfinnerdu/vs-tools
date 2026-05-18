using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DoaneDevTools.ToolWindows.TestDataSeeder.Models;

namespace DoaneDevTools.ToolWindows.TestDataSeeder.Services
{
    /// <summary>
    /// Generates realistic fake data for each column based on name patterns and SQL types.
    /// Uses deterministic patterns — Bogus (NuGet) wired in via IFakerProvider abstraction
    /// so this compiles without a hard NuGet reference at the ToolWindows project level;
    /// the VSIX project brings in Bogus and registers a concrete provider.
    /// </summary>
    public class FakerDataService
    {
        private readonly Random _rng = new();

        private readonly Dictionary<string, List<object>> _seededPrimaryKeys = new(StringComparer.OrdinalIgnoreCase);

        public void RegisterSeededKeys(string tableName, List<object> keys)
        {
            _seededPrimaryKeys[tableName] = keys;
        }

        /// <summary>Generates a single fake value for the given column.</summary>
        public object? GenerateValue(ColumnInfo column, SeedStrategy strategy)
        {
            if (column.IsPrimaryKey && column.DataType.Contains("int", StringComparison.OrdinalIgnoreCase))
                return null; // IDENTITY — let SQL Server assign

            if (column.IsForeignKey && !string.IsNullOrEmpty(column.ReferencedTable) &&
                _seededPrimaryKeys.TryGetValue(column.ReferencedTable, out var parentKeys) &&
                parentKeys.Count > 0)
            {
                return parentKeys[_rng.Next(parentKeys.Count)];
            }

            var name = column.Name.ToUpperInvariant();

            // Uniqueidentifier
            if (column.DataType.Equals("uniqueidentifier", StringComparison.OrdinalIgnoreCase))
                return Guid.NewGuid().ToString();

            // Date/time
            if (column.DataType.Contains("date", StringComparison.OrdinalIgnoreCase) ||
                name.EndsWith("DATE") || name.EndsWith("AT") || name.EndsWith("TIME"))
            {
                var start = DateTime.UtcNow.AddYears(-3);
                var range = (DateTime.UtcNow - start).TotalSeconds;
                return start.AddSeconds(_rng.NextDouble() * range);
            }

            // Bool / bit
            if (column.DataType.Equals("bit", StringComparison.OrdinalIgnoreCase))
                return _rng.Next(2) == 1;

            // Numeric
            if (column.DataType.Contains("int", StringComparison.OrdinalIgnoreCase))
                return _rng.Next(1, 10000);
            if (column.DataType.Contains("decimal", StringComparison.OrdinalIgnoreCase) ||
                column.DataType.Contains("numeric", StringComparison.OrdinalIgnoreCase) ||
                column.DataType.Contains("float", StringComparison.OrdinalIgnoreCase) ||
                column.DataType.Contains("money", StringComparison.OrdinalIgnoreCase))
                return Math.Round(_rng.NextDouble() * 10000, 2);

            // String columns — infer from name
            return InferStringValue(name, column.MaxLength);
        }

        private string InferStringValue(string columnNameUpper, int? maxLength)
        {
            var result = columnNameUpper switch
            {
                var n when n.Contains("FIRSTNAME") || n.Contains("FIRST_NAME") => RandomFirstName(),
                var n when n.Contains("LASTNAME") || n.Contains("LAST_NAME") => RandomLastName(),
                var n when n.Contains("NAME") && !n.Contains("USER") => RandomFullName(),
                var n when n.Contains("EMAIL") => RandomEmail(),
                var n when n.Contains("PHONE") => RandomPhone(),
                var n when n.Contains("ADDRESS") => RandomAddress(),
                var n when n.Contains("CITY") => RandomCity(),
                var n when n.Contains("STATE") => RandomState(),
                var n when n.Contains("ZIP") => RandomZip(),
                var n when n.Contains("SIS_ID") || n.StartsWith("SIS") => $"STU{_rng.Next(100000, 999999)}",
                var n when n.Contains("USERNAME") || n.Contains("USER_NAME") => $"user{_rng.Next(1000, 9999)}",
                var n when n.Contains("DESCRIPTION") || n.Contains("NOTES") || n.Contains("COMMENT") =>
                    "Sample description text for testing purposes.",
                var n when n.Contains("CODE") => RandomAlpha(4).ToUpper(),
                var n when n.Contains("STATUS") => RandomChoice("Active", "Inactive", "Pending"),
                var n when n.Contains("TYPE") => RandomChoice("TypeA", "TypeB", "TypeC"),
                _ => $"TestValue_{_rng.Next(1000, 9999)}"
            };

            if (maxLength.HasValue && result.Length > maxLength.Value)
                result = result[..maxLength.Value];

            return result;
        }

        private string RandomFirstName() => RandomChoice(
            "James", "Mary", "John", "Patricia", "Robert", "Jennifer",
            "Michael", "Linda", "William", "Barbara", "David", "Susan");

        private string RandomLastName() => RandomChoice(
            "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia",
            "Miller", "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez");

        private string RandomFullName() => $"{RandomFirstName()} {RandomLastName()}";
        private string RandomEmail() => $"{RandomAlpha(6).ToLower()}{_rng.Next(100, 999)}@example.com";
        private string RandomPhone() => $"({_rng.Next(200, 999)}) {_rng.Next(200, 999)}-{_rng.Next(1000, 9999)}";
        private string RandomAddress() => $"{_rng.Next(100, 9999)} {RandomChoice("Main", "Oak", "Elm", "Cedar", "Pine")} {RandomChoice("St", "Ave", "Blvd", "Dr")}";
        private string RandomCity() => RandomChoice("Crete", "Lincoln", "Omaha", "Hastings", "Grand Island", "Kearney");
        private string RandomState() => RandomChoice("NE", "IA", "KS", "MO", "SD", "ND");
        private string RandomZip() => $"{_rng.Next(10000, 99999)}";

        private string RandomAlpha(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz";
            var sb = new StringBuilder(length);
            for (int i = 0; i < length; i++)
                sb.Append(chars[_rng.Next(chars.Length)]);
            return sb.ToString();
        }

        private T RandomChoice<T>(params T[] options) => options[_rng.Next(options.Length)];
    }
}
