using Microsoft.Data.Sqlite;

namespace LrShop.Infrastructure;

public class CrawlerStateService
{
    private readonly DbFactory _db;

    public CrawlerStateService(DbFactory db)
    {
        _db = db;
    }

    public bool EstaAtivo()
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = "SELECT valor FROM config WHERE chave = 'crawler_ativo'";
        var result = cmd.ExecuteScalar()?.ToString();

        return result != "false";
    }

    public void Definir(bool ativo)
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
            INSERT INTO config (chave, valor)
            VALUES ('crawler_ativo', $valor)
            ON CONFLICT(chave) DO UPDATE SET valor = $valor
        ";

        cmd.Parameters.AddWithValue("$valor", ativo ? "true" : "false");

        cmd.ExecuteNonQuery();
    }

    public bool Alternar()
    {
        var atual = EstaAtivo();
        var novo = !atual;

        Definir(novo);

        return novo;
    }
}