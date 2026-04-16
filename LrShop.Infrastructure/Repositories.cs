using System.Text.RegularExpressions;
using LrShop.Shared;

namespace LrShop.Infrastructure;

public class ProdutoRepository
{
    private readonly DbFactory _db;

    public ProdutoRepository(DbFactory db)
    {
        _db = db;
    }

    public bool ExistePorHash(string hashUnico)
    {
        if (string.IsNullOrWhiteSpace(hashUnico))
            return false;

        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM produtos WHERE HashUnico = @hash;";
        cmd.Parameters.AddWithValue("@hash", hashUnico);

        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public bool ExistePorLink(string link)
    {
        if (string.IsNullOrWhiteSpace(link))
            return false;

        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM produtos WHERE link = @link;";
        cmd.Parameters.AddWithValue("@link", link);

        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    public bool ExisteTituloParecido(string titulo, decimal preco, decimal toleranciaPercentual = 0.08m)
    {
        if (string.IsNullOrWhiteSpace(titulo))
            return false;

        var tituloNormalizado = NormalizarTitulo(titulo);
        if (string.IsNullOrWhiteSpace(tituloNormalizado))
            return false;

        var precoMin = preco * (1 - toleranciaPercentual);
        var precoMax = preco * (1 + toleranciaPercentual);

        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT titulo, preco
FROM produtos
WHERE preco BETWEEN @precoMin AND @precoMax;";

        cmd.Parameters.AddWithValue("@precoMin", precoMin);
        cmd.Parameters.AddWithValue("@precoMax", precoMax);

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            var tituloBanco = reader.IsDBNull(0) ? "" : reader.GetString(0);
            var tituloBancoNormalizado = NormalizarTitulo(tituloBanco);

            if (tituloBancoNormalizado == tituloNormalizado)
                return true;
        }

        return false;
    }

    public IEnumerable<object> ListarComStats()
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
SELECT 
    p.id,
    p.titulo,
    p.preco,
    p.link,
    p.ImagemUrl,
    p.VideoUrl,
    p.ativo,
    p.score,
    p.criado_em,
    p.PrecoOriginal,
    p.Fonte,
    p.Categoria,
    p.HashUnico,
    COUNT(l.id) as total_postagens,
    MAX(l.criado_em) as ultima_postagem
FROM produtos p
LEFT JOIN logs_postagem l ON l.produto_id = p.id
GROUP BY p.id
ORDER BY p.id DESC;";

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            yield return new
            {
                Id = reader.GetInt32(0),
                Titulo = reader.GetString(1),
                Preco = Convert.ToDecimal(reader.GetDouble(2)),
                Link = reader.GetString(3),
                ImagemUrl = reader.IsDBNull(4) ? "" : reader.GetString(4),
                VideoUrl = reader.IsDBNull(5) ? "" : reader.GetString(5),
                Ativo = reader.GetInt32(6) == 1,
                Score = reader.GetInt32(7),
                CriadoEm = reader.GetString(8),
                PrecoOriginal = reader.IsDBNull(9) ? (decimal?)null : Convert.ToDecimal(reader.GetDouble(9)),
                Fonte = reader.IsDBNull(10) ? "Manual" : reader.GetString(10),
                Categoria = reader.IsDBNull(11) ? "" : reader.GetString(11),
                HashUnico = reader.IsDBNull(12) ? "" : reader.GetString(12),
                TotalPostagens = reader.IsDBNull(13) ? 0 : reader.GetInt32(13),
                UltimaPostagem = reader.IsDBNull(14) ? null : reader.GetString(14)
            };
        }
    }

    public Produto? ObterPrioritario(int repeticaoMinimaHoras)
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
SELECT 
    p.id,
    p.titulo,
    p.preco,
    p.link,
    p.ImagemUrl,
    p.VideoUrl,
    p.ativo,
    p.score,
    p.criado_em,
    p.PrecoOriginal,
    p.Fonte,
    p.Categoria,
    p.HashUnico,
    COALESCE(MAX(l.criado_em), '') as ultima_postagem,
    COUNT(l.id) as total_postagens
FROM produtos p
LEFT JOIN logs_postagem l 
    ON l.produto_id = p.id AND l.sucesso = 1
WHERE p.ativo = 1
GROUP BY p.id;";

        using var reader = cmd.ExecuteReader();

        Produto? melhorProduto = null;
        double melhorNota = double.MinValue;

        while (reader.Read())
        {
            var produto = new Produto
            {
                Id = reader.GetInt32(0),
                Titulo = reader.GetString(1),
                Preco = Convert.ToDecimal(reader.GetDouble(2)),
                Link = reader.GetString(3),
                ImagemUrl = reader.IsDBNull(4) ? "" : reader.GetString(4),
                VideoUrl = reader.IsDBNull(5) ? "" : reader.GetString(5),
                Ativo = reader.GetInt32(6) == 1,
                Score = reader.GetInt32(7),
                CriadoEm = DateTime.Parse(reader.GetString(8)),
                PrecoOriginal = reader.IsDBNull(9) ? null : Convert.ToDecimal(reader.GetDouble(9)),
                Fonte = reader.IsDBNull(10) ? "Manual" : reader.GetString(10),
                Categoria = reader.IsDBNull(11) ? "" : reader.GetString(11),
                HashUnico = reader.IsDBNull(12) ? "" : reader.GetString(12)
            };

            var ultimaPostagemTexto = reader.IsDBNull(13) ? "" : reader.GetString(13);
            var totalPostagens = reader.IsDBNull(14) ? 0 : reader.GetInt32(14);

            double nota = produto.Score;

            if (!string.IsNullOrWhiteSpace(ultimaPostagemTexto) &&
                DateTime.TryParse(ultimaPostagemTexto, out var ultimaPostagem))
            {
                var horasSemPostar = (DateTime.UtcNow - ultimaPostagem).TotalHours;

                nota += Math.Min(horasSemPostar * 2, 50);

                if (horasSemPostar < repeticaoMinimaHoras)
                    nota -= 1000;
            }
            else
            {
                nota += 40;
            }

            nota -= totalPostagens * 0.5;

            if (nota > melhorNota)
            {
                melhorNota = nota;
                melhorProduto = produto;
            }
        }

        return melhorProduto;
    }

    public IEnumerable<Produto> Listar()
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT 
    id, titulo, preco, link, ImagemUrl, VideoUrl, ativo, score, criado_em,
    PrecoOriginal, Fonte, Categoria, HashUnico
FROM produtos
ORDER BY id ASC;";

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            yield return Map(reader);
        }
    }

    public Produto? ObterPorId(int id)
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT 
    id, titulo, preco, link, ImagemUrl, VideoUrl, ativo, score, criado_em,
    PrecoOriginal, Fonte, Categoria, HashUnico
FROM produtos
WHERE id = @id
LIMIT 1;";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read())
            return null;

        return Map(reader);
    }

    public int Criar(Produto produto)
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO produtos (
    titulo, preco, link, ImagemUrl, VideoUrl, ativo, score, criado_em,
    PrecoOriginal, Fonte, Categoria, HashUnico
)
VALUES (
    @titulo, @preco, @link, @imagemUrl, @videoUrl, @ativo, @score, @criadoEm,
    @precoOriginal, @fonte, @categoria, @hashUnico
);
SELECT last_insert_rowid();";

        cmd.Parameters.AddWithValue("@titulo", produto.Titulo);
        cmd.Parameters.AddWithValue("@preco", produto.Preco);
        cmd.Parameters.AddWithValue("@link", produto.Link);
        cmd.Parameters.AddWithValue("@imagemUrl", produto.ImagemUrl ?? "");
        cmd.Parameters.AddWithValue("@videoUrl", produto.VideoUrl ?? "");
        cmd.Parameters.AddWithValue("@ativo", produto.Ativo ? 1 : 0);
        cmd.Parameters.AddWithValue("@score", produto.Score);
        cmd.Parameters.AddWithValue("@criadoEm", produto.CriadoEm.ToString("o"));
        cmd.Parameters.AddWithValue("@precoOriginal", (object?)produto.PrecoOriginal ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@fonte", produto.Fonte ?? "Manual");
        cmd.Parameters.AddWithValue("@categoria", produto.Categoria ?? "");
        cmd.Parameters.AddWithValue("@hashUnico", produto.HashUnico ?? "");

        return Convert.ToInt32((long)cmd.ExecuteScalar()!);
    }

    public bool Atualizar(Produto produto)
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
UPDATE produtos
SET titulo = @titulo,
    preco = @preco,
    link = @link,
    ImagemUrl = @imagemUrl,
    VideoUrl = @videoUrl,
    ativo = @ativo,
    score = @score,
    PrecoOriginal = @precoOriginal,
    Fonte = @fonte,
    Categoria = @categoria,
    HashUnico = @hashUnico
WHERE id = @id;";

        cmd.Parameters.AddWithValue("@id", produto.Id);
        cmd.Parameters.AddWithValue("@titulo", produto.Titulo);
        cmd.Parameters.AddWithValue("@preco", produto.Preco);
        cmd.Parameters.AddWithValue("@link", produto.Link);
        cmd.Parameters.AddWithValue("@imagemUrl", produto.ImagemUrl ?? "");
        cmd.Parameters.AddWithValue("@videoUrl", produto.VideoUrl ?? "");
        cmd.Parameters.AddWithValue("@ativo", produto.Ativo ? 1 : 0);
        cmd.Parameters.AddWithValue("@score", produto.Score);
        cmd.Parameters.AddWithValue("@precoOriginal", (object?)produto.PrecoOriginal ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@fonte", produto.Fonte ?? "Manual");
        cmd.Parameters.AddWithValue("@categoria", produto.Categoria ?? "");
        cmd.Parameters.AddWithValue("@hashUnico", produto.HashUnico ?? "");

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool Excluir(int id)
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "DELETE FROM produtos WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool AlterarStatus(int id, bool ativo)
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE produtos SET ativo = @ativo WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@ativo", ativo ? 1 : 0);

        return cmd.ExecuteNonQuery() > 0;
    }

    public int ContarAtivos()
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM produtos WHERE ativo = 1;";
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    private Produto Map(System.Data.IDataRecord reader)
    {
        return new Produto
        {
            Id = reader.GetInt32(0),
            Titulo = reader.GetString(1),
            Preco = Convert.ToDecimal(reader.GetDouble(2)),
            Link = reader.GetString(3),
            ImagemUrl = reader.IsDBNull(4) ? "" : reader.GetString(4),
            VideoUrl = reader.IsDBNull(5) ? "" : reader.GetString(5),
            Ativo = reader.GetInt32(6) == 1,
            Score = reader.GetInt32(7),
            CriadoEm = DateTime.Parse(reader.GetString(8)),
            PrecoOriginal = reader.IsDBNull(9) ? null : Convert.ToDecimal(reader.GetDouble(9)),
            Fonte = reader.IsDBNull(10) ? "Manual" : reader.GetString(10),
            Categoria = reader.IsDBNull(11) ? "" : reader.GetString(11),
            HashUnico = reader.IsDBNull(12) ? "" : reader.GetString(12)
        };
    }

    private static string NormalizarTitulo(string texto)
    {
        texto = texto.ToLowerInvariant().Trim();
        texto = Regex.Replace(texto, @"[^\p{L}\p{Nd}\s]", " ");
        texto = Regex.Replace(texto, @"\s+", " ");
        return texto;
    }
}

public class UsuarioRepository
{
    private readonly DbFactory _db;

    public UsuarioRepository(DbFactory db)
    {
        _db = db;
    }

    public Usuario? Autenticar(string email, string senha)
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT id, email, senha, data_expiracao
FROM usuarios
WHERE email = @email AND senha = @senha;";

        cmd.Parameters.AddWithValue("@email", email);
        cmd.Parameters.AddWithValue("@senha", senha);

        using var reader = cmd.ExecuteReader();

        if (!reader.Read())
            return null;

        var usuario = new Usuario
        {
            Id = reader.GetInt32(0),
            Email = reader.GetString(1),
            Senha = reader.GetString(2),
            DataExpiracao = DateTime.Parse(reader.GetString(3))
        };

        return usuario.DataExpiracao >= DateTime.UtcNow ? usuario : null;
    }
}

public class LogRepository
{
    private readonly DbFactory _db;

    public LogRepository(DbFactory db)
    {
        _db = db;
    }

    public void Criar(LogPostagem log)
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO logs_postagem (produto_id, canal, mensagem, criado_em, sucesso)
VALUES (@produtoId, @canal, @mensagem, @criadoEm, @sucesso);";

        cmd.Parameters.AddWithValue("@produtoId", log.ProdutoId);
        cmd.Parameters.AddWithValue("@canal", log.Canal);
        cmd.Parameters.AddWithValue("@mensagem", log.Mensagem);
        cmd.Parameters.AddWithValue("@criadoEm", log.CriadoEm.ToString("o"));
        cmd.Parameters.AddWithValue("@sucesso", log.Sucesso ? 1 : 0);

        cmd.ExecuteNonQuery();
    }

    public IEnumerable<LogPostagem> ListarRecentes(int limite = 50)
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT id, produto_id, canal, mensagem, criado_em, sucesso
FROM logs_postagem
ORDER BY id DESC
LIMIT @limite;";

        cmd.Parameters.AddWithValue("@limite", limite);

        using var reader = cmd.ExecuteReader();

        while (reader.Read())
        {
            yield return new LogPostagem
            {
                Id = reader.GetInt32(0),
                ProdutoId = reader.GetInt32(1),
                Canal = reader.GetString(2),
                Mensagem = reader.GetString(3),
                CriadoEm = DateTime.Parse(reader.GetString(4)),
                Sucesso = reader.GetInt32(5) == 1
            };
        }
    }

    public int? ObterUltimoProdutoPostado()
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT produto_id
FROM logs_postagem
WHERE sucesso = 1
ORDER BY id DESC
LIMIT 1;";

        var result = cmd.ExecuteScalar();
        if (result == null || result == DBNull.Value)
            return null;

        return Convert.ToInt32(result);
    }
}

public class CliqueRepository
{
    private readonly DbFactory _db;

    public CliqueRepository(DbFactory db)
    {
        _db = db;
    }

    public void Criar(int produtoId, string origem = "")
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO cliques (produto_id, criado_em, origem)
VALUES (@produtoId, @criadoEm, @origem);";

        cmd.Parameters.AddWithValue("@produtoId", produtoId);
        cmd.Parameters.AddWithValue("@criadoEm", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("@origem", origem);

        cmd.ExecuteNonQuery();
    }

    public int ContarPorProduto(int produtoId)
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM cliques WHERE produto_id = @produtoId;";
        cmd.Parameters.AddWithValue("@produtoId", produtoId);

        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    public Dictionary<int, int> ContarTodos()
    {
        using var conn = _db.Create();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
SELECT produto_id, COUNT(*) as total
FROM cliques
GROUP BY produto_id;";

        using var reader = cmd.ExecuteReader();

        var dict = new Dictionary<int, int>();

        while (reader.Read())
        {
            dict[reader.GetInt32(0)] = reader.GetInt32(1);
        }

        return dict;
    }
}