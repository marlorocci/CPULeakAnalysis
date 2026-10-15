using CPULeakAnalysis.Models;
using Microsoft.Data.Sqlite;
using System.IO;

namespace CPULeakAnalysis.Services;

public sealed class DatabaseService
{
    public string DatabasePath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CPULeakAnalysis", "cpu-history.db");
    private string ConnectionString => new SqliteConnectionStringBuilder { DataSource = DatabasePath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();
    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(DatabasePath)!);

        await using var connection =
            new SqliteConnection(ConnectionString);

        await connection.OpenAsync();

        var command = connection.CreateCommand();

        command.CommandText = """
        CREATE TABLE IF NOT EXISTS CpuSamples (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            TimestampUtc TEXT NOT NULL,
            ProcessId INTEGER NOT NULL,
            ProcessName TEXT NOT NULL,
            CpuPercent REAL NOT NULL,
            WorkingSetBytes INTEGER NOT NULL,
            PrivateBytes INTEGER NOT NULL DEFAULT 0,
            HandleCount INTEGER NOT NULL DEFAULT 0,
            ThreadCount INTEGER NOT NULL DEFAULT 0
        );

        CREATE INDEX IF NOT EXISTS
            IX_CpuSamples_TimestampUtc
        ON CpuSamples(TimestampUtc);
        """;

        await command.ExecuteNonQueryAsync();

        await AddColumnIfMissing(
            connection,
            "PrivateBytes",
            "INTEGER NOT NULL DEFAULT 0");

        await AddColumnIfMissing(
            connection,
            "HandleCount",
            "INTEGER NOT NULL DEFAULT 0");

        await AddColumnIfMissing(
            connection,
            "ThreadCount",
            "INTEGER NOT NULL DEFAULT 0");
    }

    private static async Task AddColumnIfMissing(
        SqliteConnection connection,
        string columnName,
        string definition)
    {
        var pragma = connection.CreateCommand();
        pragma.CommandText =
            "PRAGMA table_info(CpuSamples);";

        bool exists = false;

        await using var reader =
            await pragma.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            string existingColumn =
                reader.GetString(1);

            if (existingColumn.Equals(
                    columnName,
                    StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }

        if (exists)
            return;

        var alter = connection.CreateCommand();

        alter.CommandText =
            $"ALTER TABLE CpuSamples ADD COLUMN {columnName} {definition};";

        await alter.ExecuteNonQueryAsync();
    }
    public async Task InsertAsync(IEnumerable<CpuSample> samples)
    {
        var items = samples.ToList(); if (items.Count == 0) return;
        await using var connection = new SqliteConnection(ConnectionString); await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();
        foreach (var s in items)
        {
            var c = connection.CreateCommand(); c.Transaction = (SqliteTransaction)tx;
            c.CommandText = "INSERT INTO CpuSamples(TimestampUtc,ProcessId,ProcessName,CpuPercent,WorkingSetBytes,PrivateBytes,HandleCount,ThreadCount) VALUES($t,$p,$n,$c,$w,$pr,$h,$th)";
            c.Parameters.AddWithValue("$t", s.TimestampUtc.ToString("O")); c.Parameters.AddWithValue("$p", s.ProcessId);
            c.Parameters.AddWithValue("$n", s.ProcessName); c.Parameters.AddWithValue("$c", s.CpuPercent); c.Parameters.AddWithValue("$w", s.WorkingSetBytes);
            c.Parameters.AddWithValue("$pr", s.PrivateBytes); c.Parameters.AddWithValue("$h", s.HandleCount); c.Parameters.AddWithValue("$th", s.ThreadCount);
            await c.ExecuteNonQueryAsync();
        }
        await tx.CommitAsync();
    }
    public async Task<IReadOnlyList<CpuSample>> LoadRecentAsync(int hours)
    {
        var result = new List<CpuSample>();
        await using var connection = new SqliteConnection(ConnectionString); await connection.OpenAsync();
        var c = connection.CreateCommand();
        c.CommandText = "SELECT TimestampUtc,ProcessId,ProcessName,CpuPercent,WorkingSetBytes,PrivateBytes,HandleCount,ThreadCount FROM CpuSamples WHERE TimestampUtc >= $cutoff ORDER BY TimestampUtc";
        c.Parameters.AddWithValue("$cutoff", DateTime.UtcNow.AddHours(-hours).ToString("O"));
        await using var r = await c.ExecuteReaderAsync();
        while (await r.ReadAsync()) 
            result.Add(new CpuSample(
    DateTime.Parse(
        r.GetString(0),
        null,
        System.Globalization.DateTimeStyles.RoundtripKind),
    r.GetInt32(1),
    r.GetString(2),
    r.GetDouble(3),
    r.GetInt64(4),
    r.GetInt64(5),
    r.GetInt32(6),
    r.GetInt32(7)));
        return result;
    }
    public async Task ClearAsync()
    {
        await using var connection = new SqliteConnection(ConnectionString); await connection.OpenAsync();
        var c = connection.CreateCommand(); c.CommandText = "DELETE FROM CpuSamples; VACUUM;"; await c.ExecuteNonQueryAsync();
    }

    public async Task<List<CpuSample>>
    GetSamplesByProcessIdAsync(
        int processId)
    {
        var results = new List<CpuSample>();

        await using var connection =
            new SqliteConnection(ConnectionString);

        await connection.OpenAsync();

        var command = connection.CreateCommand();

        command.CommandText =
        """
    SELECT
        TimestampUtc,
        ProcessId,
        ProcessName,
        CpuPercent,
        WorkingSetBytes,
        PrivateBytes,
        HandleCount,
        ThreadCount
    FROM CpuSamples
    WHERE ProcessId = $pid
    ORDER BY TimestampUtc
    """;

        command.Parameters.AddWithValue(
            "$pid",
            processId);

        await using var reader =
            await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(
                new CpuSample(
                    DateTime.Parse(reader.GetString(0)),
                    reader.GetInt32(1),
                    reader.GetString(2),
                    reader.GetDouble(3),
                    reader.GetInt64(4),
                    reader.GetInt64(5),
                    reader.GetInt32(6),
                    reader.GetInt32(7)));
        }

        return results;
    }

}