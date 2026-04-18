using Microsoft.Data.Sqlite;
using FileCourier.Core.Models;

namespace FileCourier.Core.Storage;

/// <summary>
/// Persists transfer history records to SQLite (spec Section 5 - Local Data Storage).
/// </summary>
public sealed class TransferHistoryStore : IDisposable
{
    private readonly SqliteConnection _db;
    private readonly object _lock = new();

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
                ItemName          TEXT NOT NULL DEFAULT '',
                ItemPath          TEXT NOT NULL DEFAULT '',
                SourcePaths       TEXT NOT NULL DEFAULT '',
                TotalFiles        INTEGER NOT NULL,
                TotalSize         INTEGER NOT NULL,
                BytesTransferred  INTEGER NOT NULL DEFAULT 0,
                Timestamp         TEXT NOT NULL,
                Status            TEXT NOT NULL
            );
            """;
        cmd.ExecuteNonQuery();

        // Basic migration: add columns if they don't exist
        try { cmd.CommandText = "ALTER TABLE TransferHistory ADD COLUMN ItemName TEXT NOT NULL DEFAULT '';"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE TransferHistory ADD COLUMN ItemPath TEXT NOT NULL DEFAULT '';"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE TransferHistory ADD COLUMN SourcePaths TEXT NOT NULL DEFAULT '';"; cmd.ExecuteNonQuery(); } catch { }
        try { cmd.CommandText = "ALTER TABLE TransferHistory ADD COLUMN BytesTransferred INTEGER NOT NULL DEFAULT 0;"; cmd.ExecuteNonQuery(); } catch { }
    }

    public void AddRecord(TransferHistoryRecord record)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = """
                INSERT INTO TransferHistory
                    (TransferId, CounterpartyId, CounterpartyName, Direction, ItemName, ItemPath, SourcePaths, TotalFiles, TotalSize, BytesTransferred, Timestamp, Status)
                VALUES
                    ($tid, $cid, $cname, $dir, $iname, $ipath, $spaths, $files, $size, $sent, $ts, $status);
                """;
            cmd.Parameters.AddWithValue("$tid", record.TransferId.ToString());
            cmd.Parameters.AddWithValue("$cid", record.CounterpartyId.ToString());
            cmd.Parameters.AddWithValue("$cname", record.CounterpartyName);
            cmd.Parameters.AddWithValue("$dir", record.Direction.ToString());
            cmd.Parameters.AddWithValue("$iname", record.ItemName);
            cmd.Parameters.AddWithValue("$ipath", record.ItemPath);
            cmd.Parameters.AddWithValue("$spaths", record.SourcePaths);
            cmd.Parameters.AddWithValue("$files", record.TotalFiles);
            cmd.Parameters.AddWithValue("$size", record.TotalSize);
            cmd.Parameters.AddWithValue("$sent", record.BytesTransferred);
            cmd.Parameters.AddWithValue("$ts", record.Timestamp.ToString("O"));
            cmd.Parameters.AddWithValue("$status", record.Status.ToString());
            cmd.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<TransferHistoryRecord> GetAll()
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "SELECT * FROM TransferHistory ORDER BY Timestamp DESC;";
            using var reader = cmd.ExecuteReader();
            var list = new List<TransferHistoryRecord>();
            while (reader.Read())
            {
                list.Add(new TransferHistoryRecord
                {
                    TransferId = Guid.Parse(reader.GetString(reader.GetOrdinal("TransferId"))),
                    CounterpartyId = Guid.Parse(reader.GetString(reader.GetOrdinal("CounterpartyId"))),
                    CounterpartyName = reader.GetString(reader.GetOrdinal("CounterpartyName")),
                    Direction = Enum.Parse<TransferDirection>(reader.GetString(reader.GetOrdinal("Direction"))),
                    ItemName = reader.GetString(reader.GetOrdinal("ItemName")),
                    ItemPath = reader.GetString(reader.GetOrdinal("ItemPath")),
                    SourcePaths = reader.GetString(reader.GetOrdinal("SourcePaths")),
                    TotalFiles = reader.GetInt32(reader.GetOrdinal("TotalFiles")),
                    TotalSize = reader.GetInt64(reader.GetOrdinal("TotalSize")),
                    BytesTransferred = reader.GetInt64(reader.GetOrdinal("BytesTransferred")),
                    Timestamp = DateTime.Parse(reader.GetString(reader.GetOrdinal("Timestamp"))),
                    Status = Enum.Parse<TransferStatus>(reader.GetString(reader.GetOrdinal("Status")))
                });
            }
            return list;
        }
    }

    public void UpdateRecord(TransferHistoryRecord record)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "UPDATE TransferHistory SET Status = $status, BytesTransferred = $sent WHERE TransferId = $tid;";
            cmd.Parameters.AddWithValue("$tid", record.TransferId.ToString());
            cmd.Parameters.AddWithValue("$status", record.Status.ToString());
            cmd.Parameters.AddWithValue("$sent", record.BytesTransferred);
            cmd.ExecuteNonQuery();
        }
    }

    public void DeleteRecord(Guid transferId)
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "DELETE FROM TransferHistory WHERE TransferId = $tid;";
            cmd.Parameters.AddWithValue("$tid", transferId.ToString());
            cmd.ExecuteNonQuery();
        }
    }

    public void ClearAll()
    {
        lock (_lock)
        {
            using var cmd = _db.CreateCommand();
            cmd.CommandText = "DELETE FROM TransferHistory;";
            cmd.ExecuteNonQuery();
        }
    }

    public void Dispose() => _db.Dispose();
}
