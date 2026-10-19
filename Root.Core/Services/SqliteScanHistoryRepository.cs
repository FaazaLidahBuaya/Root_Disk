using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Root.Core.Models;

namespace Root.Core.Services;

/// <summary>
/// Implementasi repositori riwayat pemindaian sistem berkas berbasis basis data SQLite.
/// </summary>
public class SqliteScanHistoryRepository : IScanHistoryRepository
{
    private readonly string _connectionString;
    private readonly string? _dbDirectory;
    private bool _isInitialized;

    public SqliteScanHistoryRepository(string? databasePath = null)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _dbDirectory = Path.Combine(appData, "ROOT");
            databasePath = Path.Combine(_dbDirectory, "root_history.db");
        }
        else
        {
            _dbDirectory = Path.GetDirectoryName(databasePath);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        if (!string.IsNullOrEmpty(_dbDirectory) && !Directory.Exists(_dbDirectory))
        {
            Directory.CreateDirectory(_dbDirectory);
        }

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            CREATE TABLE IF NOT EXISTS scan_history (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                target_path TEXT NOT NULL,
                target_name TEXT NOT NULL,
                scanned_at TEXT NOT NULL,
                duration_ms INTEGER NOT NULL,
                total_size_bytes INTEGER NOT NULL,
                total_files INTEGER NOT NULL,
                total_directories INTEGER NOT NULL,
                skipped_count INTEGER NOT NULL,
                status INTEGER NOT NULL,
                is_partial INTEGER NOT NULL,
                drive_capacity_bytes INTEGER NOT NULL,
                drive_free_bytes INTEGER NOT NULL,
                drive_used_bytes INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_scan_target ON scan_history(target_path);
            CREATE INDEX IF NOT EXISTS idx_scan_id_desc ON scan_history(id DESC);
        ";

        using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync();

        _isInitialized = true;
    }

    public async Task<ScanHistoryRecordModel> SaveRecordAsync(ScanHistoryRecordModel record)
    {
        await InitializeAsync();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            INSERT INTO scan_history (
                target_path, target_name, scanned_at, duration_ms,
                total_size_bytes, total_files, total_directories, skipped_count,
                status, is_partial, drive_capacity_bytes, drive_free_bytes, drive_used_bytes
            ) VALUES (
                @target_path, @target_name, @scanned_at, @duration_ms,
                @total_size_bytes, @total_files, @total_directories, @skipped_count,
                @status, @is_partial, @drive_capacity_bytes, @drive_free_bytes, @drive_used_bytes
            );
            SELECT last_insert_rowid();
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@target_path", record.TargetPath);
        command.Parameters.AddWithValue("@target_name", record.TargetName);
        command.Parameters.AddWithValue("@scanned_at", record.ScannedAt.ToString("o", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@duration_ms", (long)record.Duration.TotalMilliseconds);
        command.Parameters.AddWithValue("@total_size_bytes", record.TotalSizeBytes);
        command.Parameters.AddWithValue("@total_files", record.TotalFiles);
        command.Parameters.AddWithValue("@total_directories", record.TotalDirectories);
        command.Parameters.AddWithValue("@skipped_count", record.SkippedCount);
        command.Parameters.AddWithValue("@status", (int)record.Status);
        command.Parameters.AddWithValue("@is_partial", record.IsPartial ? 1 : 0);
        command.Parameters.AddWithValue("@drive_capacity_bytes", record.DriveCapacityBytes);
        command.Parameters.AddWithValue("@drive_free_bytes", record.DriveFreeBytes);
        command.Parameters.AddWithValue("@drive_used_bytes", record.DriveUsedBytes);

        var result = await command.ExecuteScalarAsync();
        if (result != null && long.TryParse(result.ToString(), out long newId))
        {
            record.Id = newId;
        }

        return record;
    }

    public async Task<List<ScanHistoryRecordModel>> GetHistoryAsync(int limit = 50)
    {
        await InitializeAsync();

        var list = new List<ScanHistoryRecordModel>();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = @"
            SELECT id, target_path, target_name, scanned_at, duration_ms,
                   total_size_bytes, total_files, total_directories, skipped_count,
                   status, is_partial, drive_capacity_bytes, drive_free_bytes, drive_used_bytes
            FROM scan_history
            ORDER BY id DESC
            LIMIT @limit;
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@limit", limit);

        using var reader = await command.ExecuteReaderAsync();
        bool isFirst = true;
        while (await reader.ReadAsync())
        {
            var record = MapReader(reader);
            // Catatan yang bukan paling pertama (atau seluruh riwayat tersimpan) diberi label historis
            record.IsStale = !isFirst;
            list.Add(record);
            isFirst = false;
        }

        return list;
    }

    public async Task<ScanHistoryRecordModel?> GetPreviousScanAsync(string targetPath, long currentRecordId = 0)
    {
        await InitializeAsync();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        string sql = @"
            SELECT id, target_path, target_name, scanned_at, duration_ms,
                   total_size_bytes, total_files, total_directories, skipped_count,
                   status, is_partial, drive_capacity_bytes, drive_free_bytes, drive_used_bytes
            FROM scan_history
            WHERE target_path = @target_path AND id != @current_id
            ORDER BY id DESC
            LIMIT 1;
        ";

        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@target_path", targetPath);
        command.Parameters.AddWithValue("@current_id", currentRecordId);

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var record = MapReader(reader);
            record.IsStale = true;
            return record;
        }

        return null;
    }

    public async Task DeleteRecordAsync(long id)
    {
        await InitializeAsync();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = "DELETE FROM scan_history WHERE id = @id;";
        using var command = new SqliteCommand(sql, connection);
        command.Parameters.AddWithValue("@id", id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task ClearHistoryAsync()
    {
        await InitializeAsync();

        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = "DELETE FROM scan_history;";
        using var command = new SqliteCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static ScanHistoryRecordModel MapReader(SqliteDataReader reader)
    {
        DateTime scannedAt = DateTime.UtcNow;
        if (DateTime.TryParse(reader.GetString(3), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsedDate))
        {
            scannedAt = parsedDate;
        }

        return new ScanHistoryRecordModel
        {
            Id = reader.GetInt64(0),
            TargetPath = reader.GetString(1),
            TargetName = reader.GetString(2),
            ScannedAt = scannedAt,
            Duration = TimeSpan.FromMilliseconds(reader.GetInt64(4)),
            TotalSizeBytes = reader.GetInt64(5),
            TotalFiles = reader.GetInt64(6),
            TotalDirectories = reader.GetInt64(7),
            SkippedCount = reader.GetInt32(8),
            Status = (ScanStatus)reader.GetInt32(9),
            IsPartial = reader.GetInt32(10) == 1,
            DriveCapacityBytes = reader.GetInt64(11),
            DriveFreeBytes = reader.GetInt64(12),
            DriveUsedBytes = reader.GetInt64(13)
        };
    }
}
