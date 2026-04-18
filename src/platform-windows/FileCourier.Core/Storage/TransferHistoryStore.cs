using Microsoft.Data.Sqlite;
using FileCourier.Core.Models;

namespace FileCourier.Core.Storage;

/// <summary>
/// Persists transfer history records to SQLite (spec Section 5 - Local Data Storage).
/// </summary>
public sealed class TransferHistoryStore : IDisposable
{
    private readonly SqliteConnection _db;

    public TransferHistoryStore(string? dbPath = null)
    {
        var cs = dbPath is null ? "Data Source=:memory:" : $"Data Source={dbPath}";
        _db = new SqliteConnection(cs);
        _db.Open();
        InitializeSchema();
    }

    private void InitializeSchema()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS TransferHistory (
                TransferId        TEXT PRIMARY KEY NOT NULL,
                CounterpartyId    TEXT NOT NULL,
                CounterpartyName  TEXT NOT NULL DEFAULT '',
                Direction         TEXT NOT NULL,
                TotalFiles        INTEGER NOT NULL,
                TotalSize         INTEGER NOT NULL,
                Timestamp         TEXT NOT NULL,
                Status            TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();
    }

    public void AddRecord(TransferHistoryRecord record)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO TransferHistory
                (TransferId, CounterpartyId, CounterpartyName, Direction, TotalFiles, TotalSize, Timestamp, Status)
            VALUES
                ($tid, $cid, $cname, $dir, $files, $size, $ts, $status);
            """;
        cmd.Parameters.AddWithValue("$tid", record.TransferId.ToString());
        cmd.Parameters.AddWithValue("$cid", record.CounterpartyId.ToString());
        cmd.Parameters.AddWithValue("$cname", record.CounterpartyName);
        cmd.Parameters.AddWithValue("$dir", record.Direction.ToString());
        cmd.Parameters.AddWithValue("$files", record.TotalFiles);
        cmd.Parameters.AddWithValue("$size", record.TotalSize);
        cmd.Parameters.AddWithValue("$ts", record.Timestamp.ToString("O"));
        cmd.Parameters.AddWithValue("$status", record.Status.ToString());
        cmd.ExecuteNonQuery();
    }

    public IReadOnlyList<TransferHistoryRecord> GetAll()
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT * FROM TransferHistory ORDER BY Timestamp DESC;";
        using var reader = cmd.ExecuteReader();
        var list = new List<TransferHistoryRecord>();
        while (reader.Read())
        {
            list.Add(new TransferHistoryRecord
            {
                TransferId = Guid.Parse(reader.GetString(0)),
                CounterpartyId = Guid.Parse(reader.GetString(1)),
                CounterpartyName = reader.GetString(2),
                Direction = Enum.Parse<TransferDirection>(reader.GetString(3)),
                TotalFiles = reader.GetInt32(4),
                TotalSize = reader.GetInt64(5),
                Timestamp = DateTime.Parse(reader.GetString(6)),
                Status = Enum.Parse<TransferStatus>(reader.GetString(7))
            });
        }
        return list;
    }

    public void Dispose() => _db.Dispose();
}
