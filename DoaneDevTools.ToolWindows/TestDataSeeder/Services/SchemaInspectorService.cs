using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using DoaneDevTools.ToolWindows.TestDataSeeder.Models;

namespace DoaneDevTools.ToolWindows.TestDataSeeder.Services
{
    /// <summary>Reads SQL Server schema metadata for seed planning.</summary>
    public class SchemaInspectorService
    {
        private readonly string _connectionString;

        public SchemaInspectorService(string connectionString)
        {
            _connectionString = connectionString;
        }

        /// <summary>Loads all user tables with FK dependencies.</summary>
        public async Task<List<TableSeedConfig>> GetTablesAsync()
        {
            var tables = new List<TableSeedConfig>();

            const string sql = @"
                SELECT
                    t.TABLE_SCHEMA, t.TABLE_NAME
                FROM INFORMATION_SCHEMA.TABLES t
                WHERE t.TABLE_TYPE = 'BASE TABLE'
                ORDER BY t.TABLE_SCHEMA, t.TABLE_NAME";

            using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();
            using var cmd = new SqlCommand(sql, conn);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                tables.Add(new TableSeedConfig
                {
                    SchemaName = reader.GetString(0),
                    TableName = reader.GetString(1)
                });
            }

            reader.Close();

            foreach (var table in tables)
            {
                table.Columns = await GetColumnsAsync(conn, table.SchemaName, table.TableName);
                table.ForeignKeyDependencies = await GetForeignKeyDepsAsync(conn, table.SchemaName, table.TableName);
            }

            return tables;
        }

        private async Task<List<ColumnInfo>> GetColumnsAsync(SqlConnection conn, string schema, string table)
        {
            var columns = new List<ColumnInfo>();

            const string sql = @"
                SELECT
                    c.COLUMN_NAME,
                    c.DATA_TYPE,
                    c.IS_NULLABLE,
                    c.CHARACTER_MAXIMUM_LENGTH,
                    CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END AS IS_PK,
                    fk.REFERENCED_TABLE,
                    fk.REFERENCED_COLUMN
                FROM INFORMATION_SCHEMA.COLUMNS c
                LEFT JOIN (
                    SELECT ku.COLUMN_NAME
                    FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE ku
                        ON tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
                    WHERE tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
                      AND tc.TABLE_SCHEMA = @schema AND tc.TABLE_NAME = @table
                ) pk ON c.COLUMN_NAME = pk.COLUMN_NAME
                LEFT JOIN (
                    SELECT
                        col.name AS COLUMN_NAME,
                        reftab.name AS REFERENCED_TABLE,
                        refcol.name AS REFERENCED_COLUMN
                    FROM sys.foreign_key_columns fkc
                    JOIN sys.columns col ON fkc.parent_object_id = col.object_id AND fkc.parent_column_id = col.column_id
                    JOIN sys.tables reftab ON fkc.referenced_object_id = reftab.object_id
                    JOIN sys.columns refcol ON fkc.referenced_object_id = refcol.object_id AND fkc.referenced_column_id = refcol.column_id
                    JOIN sys.tables t ON fkc.parent_object_id = t.object_id
                    JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE s.name = @schema AND t.name = @table
                ) fk ON c.COLUMN_NAME = fk.COLUMN_NAME
                WHERE c.TABLE_SCHEMA = @schema AND c.TABLE_NAME = @table
                ORDER BY c.ORDINAL_POSITION";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@schema", schema);
            cmd.Parameters.AddWithValue("@table", table);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                columns.Add(new ColumnInfo
                {
                    Name = reader.GetString(0),
                    DataType = reader.GetString(1),
                    IsNullable = reader.GetString(2) == "YES",
                    MaxLength = reader.IsDBNull(3) ? null : (int?)reader.GetInt32(3),
                    IsPrimaryKey = reader.GetInt32(4) == 1,
                    IsForeignKey = !reader.IsDBNull(5),
                    ReferencedTable = reader.IsDBNull(5) ? null : reader.GetString(5),
                    ReferencedColumn = reader.IsDBNull(6) ? null : reader.GetString(6)
                });
            }

            return columns;
        }

        private async Task<List<string>> GetForeignKeyDepsAsync(SqlConnection conn, string schema, string table)
        {
            var deps = new List<string>();

            const string sql = @"
                SELECT DISTINCT reftab.name AS REFERENCED_TABLE
                FROM sys.foreign_key_columns fkc
                JOIN sys.tables t ON fkc.parent_object_id = t.object_id
                JOIN sys.schemas s ON t.schema_id = s.schema_id
                JOIN sys.tables reftab ON fkc.referenced_object_id = reftab.object_id
                WHERE s.name = @schema AND t.name = @table";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@schema", schema);
            cmd.Parameters.AddWithValue("@table", table);
            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
                deps.Add(reader.GetString(0));

            return deps;
        }
    }
}
