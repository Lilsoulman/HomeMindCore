using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace HomeMind.CreatorMcp;

internal sealed class CreatorStore
{
    private readonly string _connectionString;

    public CreatorStore(string databasePath)
    {
        var directory = Path.GetDirectoryName(Path.GetFullPath(databasePath));
        if (!string.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = databasePath }.ToString();
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS creator_items (
                source_type TEXT NOT NULL,
                source_id TEXT NOT NULL,
                code TEXT NULL,
                name TEXT NOT NULL,
                category TEXT NULL,
                description TEXT NULL,
                payload_json TEXT NOT NULL,
                contains_sensitive_data INTEGER NOT NULL DEFAULT 0,
                synced_at_utc TEXT NOT NULL,
                PRIMARY KEY (source_type, source_id)
            );
            CREATE TABLE IF NOT EXISTS sync_metadata (
                metadata_key TEXT NOT NULL PRIMARY KEY,
                metadata_value TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS ix_creator_items_search ON creator_items(source_type, category, name);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<SyncSummary> ReplaceAsync(IReadOnlyList<CreatorItem> items, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow.ToString("O");
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var type in new[] { "expert", "group", "skill" })
        {
            await using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM creator_items WHERE source_type = $type";
            delete.Parameters.AddWithValue("$type", type);
            await delete.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var item in items)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = """
                INSERT INTO creator_items (source_type, source_id, code, name, category, description, payload_json, contains_sensitive_data, synced_at_utc)
                VALUES ($type, $id, $code, $name, $category, $description, $payload, $sensitive, $syncedAt)
                """;
            insert.Parameters.AddWithValue("$type", item.Type);
            insert.Parameters.AddWithValue("$id", item.Id);
            insert.Parameters.AddWithValue("$code", (object?)item.Code ?? DBNull.Value);
            insert.Parameters.AddWithValue("$name", item.Name);
            insert.Parameters.AddWithValue("$category", (object?)item.Category ?? DBNull.Value);
            insert.Parameters.AddWithValue("$description", (object?)item.Description ?? DBNull.Value);
            insert.Parameters.AddWithValue("$payload", item.PayloadJson);
            insert.Parameters.AddWithValue("$sensitive", item.ContainsSensitiveData ? 1 : 0);
            insert.Parameters.AddWithValue("$syncedAt", now);
            await insert.ExecuteNonQueryAsync(cancellationToken);
            counts[item.Type] = counts.GetValueOrDefault(item.Type) + 1;
        }

        await SetMetadataAsync(connection, transaction, "last_successful_sync_utc", now, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new SyncSummary(counts.GetValueOrDefault("expert"), counts.GetValueOrDefault("group"), counts.GetValueOrDefault("skill"), now);
    }

    public async Task<IReadOnlyList<CreatorItemSummary>> SearchAsync(string? query, string? type, string? category, int limit, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT source_type, source_id, code, name, category, description, synced_at_utc
            FROM creator_items
            WHERE ($type IS NULL OR source_type = $type)
              AND ($category IS NULL OR category = $category)
              AND ($query IS NULL OR name LIKE $likeQuery OR code LIKE $likeQuery OR description LIKE $likeQuery)
            ORDER BY source_type, name
            LIMIT $limit
            """;
        command.Parameters.AddWithValue("$type", (object?)type ?? DBNull.Value);
        command.Parameters.AddWithValue("$category", (object?)category ?? DBNull.Value);
        command.Parameters.AddWithValue("$query", string.IsNullOrWhiteSpace(query) ? DBNull.Value : query);
        command.Parameters.AddWithValue("$likeQuery", $"%{query?.Trim()}%");
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 100));

        var result = new List<CreatorItemSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(new CreatorItemSummary(
                reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6)));
        }
        return result;
    }

    public async Task<CreatorItem?> GetAsync(string type, string id, CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT source_type, source_id, code, name, category, description, payload_json, contains_sensitive_data FROM creator_items WHERE source_type = $type AND source_id = $id";
        command.Parameters.AddWithValue("$type", type);
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? new CreatorItem(reader.GetString(0), reader.GetString(1), reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4), reader.IsDBNull(5) ? null : reader.GetString(5), reader.GetString(6), reader.GetInt32(7) == 1)
            : null;
    }

    public async Task<object> GetStatusAsync(CancellationToken cancellationToken)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT source_type, COUNT(*) FROM creator_items GROUP BY source_type";
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) counts[reader.GetString(0)] = reader.GetInt32(1);

        await using var metadata = connection.CreateCommand();
        metadata.CommandText = "SELECT metadata_value FROM sync_metadata WHERE metadata_key = 'last_successful_sync_utc'";
        var lastSync = await metadata.ExecuteScalarAsync(cancellationToken) as string;
        return new { lastSuccessfulSyncUtc = lastSync, experts = counts.GetValueOrDefault("expert"), groups = counts.GetValueOrDefault("group"), skills = counts.GetValueOrDefault("skill") };
    }

    private async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static async Task SetMetadataAsync(SqliteConnection connection, SqliteTransaction transaction, string key, string value, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT INTO sync_metadata (metadata_key, metadata_value) VALUES ($key, $value) ON CONFLICT(metadata_key) DO UPDATE SET metadata_value = excluded.metadata_value";
        command.Parameters.AddWithValue("$key", key);
        command.Parameters.AddWithValue("$value", value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

internal sealed record CreatorItem(string Type, string Id, string? Code, string Name, string? Category, string? Description, string PayloadJson, bool ContainsSensitiveData);
internal sealed record CreatorItemSummary(string Type, string Id, string? Code, string Name, string? Category, string? Description, string SyncedAtUtc);
internal sealed record SyncSummary(int Experts, int Groups, int Skills, string SyncedAtUtc);
