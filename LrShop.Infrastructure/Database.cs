using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using LrShop.Shared;

namespace LrShop.Infrastructure;

public class DbFactory
{
    private readonly string _connectionString;

    public DbFactory(IOptions<AppSettings> options)
    {
        var path = options.Value.Database.Path;
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        _connectionString = $"Data Source={path}";
    }

    public SqliteConnection Create()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }
}

public static class DatabaseInitializer
{
    public static void Initialize(DbFactory factory)
    {
        using var conn = factory.Create();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS produtos (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    titulo TEXT NOT NULL,
    preco REAL NOT NULL,
    link TEXT NOT NULL,
    ImagemUrl TEXT NOT NULL DEFAULT '',
    VideoUrl TEXT NOT NULL DEFAULT '',
    ativo INTEGER NOT NULL DEFAULT 1,
    score INTEGER NOT NULL DEFAULT 50,
    criado_em TEXT NOT NULL,
    PrecoOriginal REAL NULL,
    Fonte TEXT NOT NULL DEFAULT 'Shopee',
    Categoria TEXT NOT NULL DEFAULT '',
    HashUnico TEXT NOT NULL DEFAULT '',
    LinktreeStatus TEXT NOT NULL DEFAULT 'Pendente',
    LinktreeTitle TEXT NOT NULL DEFAULT '',
    LinktreeSentAt TEXT NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS usuarios (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    email TEXT NOT NULL UNIQUE,
    senha TEXT NOT NULL,
    data_expiracao TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS logs_postagem (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    produto_id INTEGER NOT NULL,
    canal TEXT NOT NULL,
    mensagem TEXT NOT NULL,
    criado_em TEXT NOT NULL,
    sucesso INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS cliques (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    produto_id INTEGER NOT NULL,
    criado_em TEXT NOT NULL,
    origem TEXT
);

CREATE TABLE IF NOT EXISTS config (
    chave TEXT PRIMARY KEY,
    valor TEXT
);";
        cmd.ExecuteNonQuery();

        // Garante colunas novas sem quebrar bancos antigos
        EnsureColumn(conn, "produtos", "ImagemUrl", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "produtos", "VideoUrl", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "produtos", "PrecoOriginal", "REAL NULL");
        EnsureColumn(conn, "produtos", "Fonte", "TEXT NOT NULL DEFAULT 'Shopee'");
        EnsureColumn(conn, "produtos", "Categoria", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "produtos", "HashUnico", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "produtos", "LinktreeStatus", "TEXT NOT NULL DEFAULT 'Pendente'");
        EnsureColumn(conn, "produtos", "LinktreeTitle", "TEXT NOT NULL DEFAULT ''");
        EnsureColumn(conn, "produtos", "LinktreeSentAt", "TEXT NOT NULL DEFAULT ''");

        using var checkCmd = conn.CreateCommand();
        checkCmd.CommandText = "SELECT COUNT(*) FROM usuarios WHERE email='admin@lr.com';";
        var count = Convert.ToInt32(checkCmd.ExecuteScalar());

        if (count == 0)
        {
            using var insertCmd = conn.CreateCommand();
            insertCmd.CommandText = @"
INSERT INTO usuarios (email, senha, data_expiracao) 
VALUES ('admin@lr.com', '123', @data);";
            insertCmd.Parameters.AddWithValue("@data", DateTime.UtcNow.AddYears(5).ToString("o"));
            insertCmd.ExecuteNonQuery();
        }

        using var checkConfigCmd = conn.CreateCommand();
        checkConfigCmd.CommandText = "SELECT COUNT(*) FROM config WHERE chave = 'crawler_ativo';";
        var configCount = Convert.ToInt32(checkConfigCmd.ExecuteScalar());

        if (configCount == 0)
        {
            using var insertConfigCmd = conn.CreateCommand();
            insertConfigCmd.CommandText = @"
INSERT INTO config (chave, valor)
VALUES ('crawler_ativo', 'true');";
            insertConfigCmd.ExecuteNonQuery();
        }
    }

    private static void EnsureColumn(SqliteConnection conn, string table, string column, string definition)
    {
        using var check = conn.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table});";

        using var reader = check.ExecuteReader();
        var exists = false;

        while (reader.Read())
        {
            var columnName = reader.GetString(1);
            if (string.Equals(columnName, column, StringComparison.OrdinalIgnoreCase))
            {
                exists = true;
                break;
            }
        }

        reader.Close();

        if (exists)
            return;

        using var alter = conn.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
    }
}

