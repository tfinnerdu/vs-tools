using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DoaneDevTools.ToolWindows.TestDataSeeder.Models;

namespace DoaneDevTools.ToolWindows.TestDataSeeder.Services
{
    /// <summary>Executes the seeding INSERT statements with transaction support.</summary>
    public class SqlSeedService
    {
        private readonly string _connectionString;
        private readonly FakerDataService _faker = new();

        public SqlSeedService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public event Action<string>? LogMessage;

        /// <summary>Seeds all enabled tables in topological order. Wraps in a single transaction.</summary>
        public async Task SeedAsync(
            List<TableSeedConfig> tables,
            bool previewOnly,
            CancellationToken cancellationToken = default)
        {
            var (sorted, cycles) = TopologicalSortService.Sort(tables.Where(t => t.IsEnabled));

            foreach (var cycle in cycles)
                LogMessage?.Invoke($"[WARN] {cycle}");

            if (previewOnly)
            {
                foreach (var table in sorted)
                {
                    var preview = GenerateInsertPreview(table, previewRows: 3);
                    LogMessage?.Invoke(preview);
                }
                return;
            }

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            using var tx = conn.BeginTransaction(IsolationLevel.ReadCommitted);

            try
            {
                foreach (var table in sorted)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var seededKeys = await SeedTableAsync(conn, tx, table, cancellationToken);
                    _faker.RegisterSeededKeys(table.TableName, seededKeys);
                    LogMessage?.Invoke($"[OK] Seeded {seededKeys.Count} rows into {table.FullName}");
                }

                tx.Commit();
                LogMessage?.Invoke("[DONE] All tables seeded successfully.");
            }
            catch (Exception ex)
            {
                tx.Rollback();
                LogMessage?.Invoke($"[ERROR] Seed failed, rolled back: {ex.Message}");
                throw;
            }
        }

        private async Task<List<object>> SeedTableAsync(
            SqlConnection conn, SqlTransaction tx,
            TableSeedConfig table, CancellationToken ct)
        {
            var insertedKeys = new List<object>();
            var pkColumn = table.Columns.FirstOrDefault(c => c.IsPrimaryKey);
            var hasScopeIdentity = pkColumn != null &&
                pkColumn.DataType.Contains("int", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < table.SeedCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                var insertSql = BuildInsertSql(table, out var paramValues);
                using var cmd = new SqlCommand(
                    hasScopeIdentity ? insertSql + "; SELECT SCOPE_IDENTITY();" : insertSql,
                    conn, tx);

                foreach (var (paramName, value) in paramValues)
                    cmd.Parameters.AddWithValue(paramName, value ?? DBNull.Value);

                if (hasScopeIdentity)
                {
                    var id = await cmd.ExecuteScalarAsync(ct);
                    if (id != null && id != DBNull.Value)
                        insertedKeys.Add(id);
                }
                else
                {
                    await cmd.ExecuteNonQueryAsync(ct);
                }
            }

            return insertedKeys;
        }

        private string BuildInsertSql(TableSeedConfig table, out List<(string, object?)> paramValues)
        {
            paramValues = new List<(string, object?)>();
            var columns = table.Columns.Where(c => !c.IsPrimaryKey ||
                !c.DataType.Contains("int", StringComparison.OrdinalIgnoreCase)).ToList();

            var colNames = string.Join(", ", columns.Select(c => $"[{c.Name}]"));
            var paramNames = new List<string>();

            for (int i = 0; i < columns.Count; i++)
            {
                var paramName = $"@p{i}";
                paramNames.Add(paramName);
                paramValues.Add((paramName, _faker.GenerateValue(columns[i], table.Strategy)));
            }

            return $"INSERT INTO [{table.SchemaName}].[{table.TableName}] ({colNames}) VALUES ({string.Join(", ", paramNames)})";
        }

        private string GenerateInsertPreview(TableSeedConfig table, int previewRows)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"-- Preview: {table.FullName} ({table.SeedCount} rows total, showing {previewRows})");

            for (int i = 0; i < previewRows; i++)
            {
                var sql = BuildInsertSql(table, out var paramValues);
                // Inline the values for preview display
                foreach (var (name, value) in paramValues)
                {
                    var display = value == null ? "NULL" :
                        value is string s ? $"'{s.Replace("'", "''")}'" :
                        value is DateTime dt ? $"'{dt:yyyy-MM-dd HH:mm:ss}'" :
                        value.ToString();
                    sql = sql.Replace(name, display ?? "NULL");
                }
                sb.AppendLine(sql + ";");
            }

            return sb.ToString();
        }

        /// <summary>Exports all planned INSERT statements as a SQL script file.</summary>
        public async Task<string> ExportSqlScriptAsync(List<TableSeedConfig> tables)
        {
            var sb = new StringBuilder();
            sb.AppendLine("-- Generated by DoaneDevTools Test Data Seeder");
            sb.AppendLine($"-- {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine("BEGIN TRANSACTION;");
            sb.AppendLine();

            var (sorted, _) = TopologicalSortService.Sort(tables.Where(t => t.IsEnabled));

            foreach (var table in sorted)
            {
                sb.AppendLine($"-- {table.FullName}: {table.SeedCount} rows");
                sb.Append(GenerateInsertPreview(table, table.SeedCount));
                sb.AppendLine();
            }

            sb.AppendLine("COMMIT;");
            return await System.Threading.Tasks.Task.FromResult(sb.ToString());
        }
    }
}
