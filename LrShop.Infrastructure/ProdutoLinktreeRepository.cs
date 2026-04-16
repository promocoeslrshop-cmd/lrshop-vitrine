using Microsoft.Data.Sqlite;
using LrShop.Shared;

namespace LrShop.Infrastructure;

public class ProdutoLinktreeRepository
{
    private readonly DbFactory _factory;

    public ProdutoLinktreeRepository(DbFactory factory)
    {
        _factory = factory;
    }

    public List<ProdutoLinktreeItem> ListarPendentes()
    {
        var lista = new List<ProdutoLinktreeItem>();

        using var conn = _factory.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT 
    id,
    titulo,
    preco,
    link,
    IFNULL(ImagemUrl, ''),
    ativo,
    score,
    criado_em,
    IFNULL(LinktreeStatus, 'Pendente'),
    IFNULL(LinktreeTitle, ''),
    IFNULL(LinktreeSentAt, '')
FROM produtos
WHERE ativo = 1
  AND IFNULL(LinktreeStatus, 'Pendente') <> 'Enviado'
ORDER BY id DESC;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            lista.Add(new ProdutoLinktreeItem
            {
                Id = reader.GetInt32(0),
                Titulo = reader.GetString(1),
                Preco = Convert.ToDecimal(reader.GetValue(2)),
                Link = reader.GetString(3),
                ImagemUrl = reader.GetString(4),
                Ativo = reader.GetInt32(5) == 1,
                Score = reader.GetInt32(6),
                CriadoEm = reader.GetString(7),
                LinktreeStatus = reader.GetString(8),
                LinktreeTitle = reader.GetString(9),
                LinktreeSentAt = reader.GetString(10)
            });
        }

        return lista;
    }

    public ProdutoLinktreeItem? ObterPorId(int produtoId)
    {
        using var conn = _factory.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT 
    id,
    titulo,
    preco,
    link,
    IFNULL(ImagemUrl, ''),
    ativo,
    score,
    criado_em,
    IFNULL(LinktreeStatus, 'Pendente'),
    IFNULL(LinktreeTitle, ''),
    IFNULL(LinktreeSentAt, '')
FROM produtos
WHERE id = @id
LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", produtoId);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        return new ProdutoLinktreeItem
        {
            Id = reader.GetInt32(0),
            Titulo = reader.GetString(1),
            Preco = Convert.ToDecimal(reader.GetValue(2)),
            Link = reader.GetString(3),
            ImagemUrl = reader.GetString(4),
            Ativo = reader.GetInt32(5) == 1,
            Score = reader.GetInt32(6),
            CriadoEm = reader.GetString(7),
            LinktreeStatus = reader.GetString(8),
            LinktreeTitle = reader.GetString(9),
            LinktreeSentAt = reader.GetString(10)
        };
    }

    public void MarcarComoEnviado(int produtoId, string tituloLinktree)
    {
        using var conn = _factory.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
UPDATE produtos
SET LinktreeStatus = @status,
    LinktreeTitle = @title,
    LinktreeSentAt = @sentAt
WHERE id = @id;";
        cmd.Parameters.AddWithValue("@status", "Enviado");
        cmd.Parameters.AddWithValue("@title", tituloLinktree ?? "");
        cmd.Parameters.AddWithValue("@sentAt", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@id", produtoId);
        cmd.ExecuteNonQuery();
    }

    public void MarcarComoPendente(int produtoId)
    {
        using var conn = _factory.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
UPDATE produtos
SET LinktreeStatus = @status,
    LinktreeSentAt = '',
    LinktreeTitle = ''
WHERE id = @id;";
        cmd.Parameters.AddWithValue("@status", "Pendente");
        cmd.Parameters.AddWithValue("@id", produtoId);
        cmd.ExecuteNonQuery();
    }
}
