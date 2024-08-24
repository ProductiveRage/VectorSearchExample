using System.Runtime.CompilerServices;
using Npgsql;
using NpgsqlTypes;
using Pgvector;

namespace VectorSearchExample;

public sealed class PostgresPostDataStore : IAsyncDisposable
{
    private readonly NpgsqlConnection _connection;
    private PostgresPostDataStore(NpgsqlConnection connection) => _connection = connection;

    public static async Task<PostgresPostDataStore> Get(string connectionString, int embeddingDimensions, CancellationToken cancellationToken = default)
    {
        if (embeddingDimensions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(embeddingDimensions));
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        await using var dataSource = dataSourceBuilder.Build();

        var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        using var command = new NpgsqlCommand();
        command.Connection = connection;

        command.CommandText = "CREATE EXTENSION IF NOT EXISTS vector";
        await command.ExecuteNonQueryAsync(cancellationToken);
        await connection.ReloadTypesAsync();

        command.CommandText = $"CREATE TABLE IF NOT EXISTS posts (id integer NOT NULL UNIQUE, parent integer, type integer NOT NULL, title text, body text NOT NULL, embedding vector({embeddingDimensions}) NOT NULL)";
        await command.ExecuteNonQueryAsync(cancellationToken);

        return new PostgresPostDataStore(connection);
    }

    public async ValueTask DisposeAsync() => await _connection.DisposeAsync();

    public async Task<bool> DoesPostExist(int id, CancellationToken cancellationToken = default)
    {
        using var existsCommand = new NpgsqlCommand();
        existsCommand.Connection = _connection;
        existsCommand.CommandText = "SELECT EXISTS(SELECT 1 FROM posts WHERE id = @id LIMIT 1)";
        existsCommand.Parameters.AddWithValue("@id", id);
        return (bool)(await existsCommand.ExecuteScalarAsync(cancellationToken))!;
    }

    public async Task Insert(Post post, ReadOnlyMemory<float> embedding, CancellationToken cancellationToken = default)
    {
        using var insertCommand = new NpgsqlCommand();
        insertCommand.Connection = _connection;
        insertCommand.CommandText = "INSERT INTO posts (id, parent, type, title, body, embedding) VALUES (@id, @parent, @type, @title, @body, @embedding)";
        insertCommand.Parameters.AddWithValue("@id", post.Id);
        insertCommand.Parameters.AddWithValue("@parent", post.ParentId.HasValue ? post.ParentId : DBNull.Value);
        insertCommand.Parameters.AddWithValue("@type", (int)post.Type);
        if (post.Title is null)
        {
            insertCommand.Parameters.AddWithValue("@title", NpgsqlDbType.Text, DBNull.Value);
        }
        else
        {
            insertCommand.Parameters.AddWithValue("@title", post.Title);
        }
        insertCommand.Parameters.AddWithValue("@body", post.BodyHtml);
        insertCommand.Parameters.AddWithValue("@embedding", embedding);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    public async IAsyncEnumerable<(Post Post, double Distance)> SearchForAnswers(ReadOnlyMemory<float> embedding, int maxNumberOfResults, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (maxNumberOfResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxNumberOfResults));
        }

        // The pgvector "<==>" operator is cosine distance (so 0 is a perfect match, 1 is as different as possible),
        // it's not cosine similarity (where 0 is not similar at all, and 1 is a perfect match)
        using var queryCommand = new NpgsqlCommand();
        queryCommand.Connection = _connection;
        queryCommand.CommandText = $"SELECT id, parent, title, body, embedding <=> @embedding FROM posts WHERE type = {(int)PostType.Answer} ORDER BY embedding <=> @embedding LIMIT @limit";
        queryCommand.Parameters.AddWithValue("@embedding", new Vector(embedding));
        queryCommand.Parameters.AddWithValue("@limit", maxNumberOfResults);

        using var reader = await queryCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var post = new Post(
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetInt32(1),
                PostType.Answer,
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.GetString(3));

            var distance = reader.GetDouble(4);

            yield return (post, distance);
        }
    }
}