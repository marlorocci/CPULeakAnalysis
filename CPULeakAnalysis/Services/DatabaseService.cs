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
        Directory.CreateDirectory(Path.GetDirectoryName(DatabasePath)!);
        await using var connection = new SqliteConnection(ConnectionString);
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
            PrivateBytes INTEGER NOT NULL,
            HandleCount INTEGER NOT NULL,
            ThreadCount INTEGER NOT NULL
        );
        
        CREATE INDEX IF NOT EXISTS IX_CpuSamples_TimestampUtc ON CpuSamples(TimestampUtc);
        """;
        await command.ExecuteNonQueryAsync();
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
}