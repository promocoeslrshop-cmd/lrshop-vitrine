using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace LrShop.Infrastructure;

public class JsonExportService
{
    private readonly DbFactory _factory;
    private readonly string _outputPath;

    public JsonExportService(DbFactory factory)
    {
        _factory = factory;

        var apiRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "LrShop.Api")
        );

        _outputPath = Path.Combine(apiRoot, "wwwroot", "vitrine", "produtos.json");
    }

    public async Task<int> GerarJsonAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_outputPath)!);

        using var conn = _factory.Create();
        using var cmd = conn.CreateCommand();

        cmd.CommandText = @"
SELECT
    id,
    titulo,
    preco,
    link,
    COALESCE(ImagemUrl, '') as imagem_url,
    ativo,
    score,
    criado_em
FROM produtos
WHERE ativo = 1
ORDER BY score DESC, preco ASC, datetime(criado_em) DESC;
";

        var produtos = new List<object>();

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var id = reader.GetInt32(0);
            var titulo = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var preco = GetDecimalSafe(reader, 2);
            var link = reader.IsDBNull(3) ? "" : reader.GetString(3);
            var imagem = reader.IsDBNull(4) ? "" : reader.GetString(4);
            var ativo = !reader.IsDBNull(5) && reader.GetInt32(5) == 1;
            var score = reader.IsDBNull(6) ? 50 : reader.GetInt32(6);
            var criadoEm = reader.IsDBNull(7) ? "" : reader.GetString(7);

            produtos.Add(new
            {
                id,
                titulo,
                preco,
                link,
                imagem,
                ativo,
                score,
                categoria = "Ofertas",
                desconto = "",
                precoOriginal = (decimal?)null,
                criadoEm
            });
        }

        var json = JsonSerializer.Serialize(produtos, new JsonSerializerOptions
        {
            WriteIndented = true
        });

        await File.WriteAllTextAsync(_outputPath, json, cancellationToken);
        return produtos.Count;
    }

    private static decimal GetDecimalSafe(SqliteDataReader reader, int ordinal)
    {
        if (reader.IsDBNull(ordinal))
            return 0m;

        var value = reader.GetValue(ordinal);

        return value switch
        {
            decimal d => d,
            double db => Convert.ToDecimal(db, CultureInfo.InvariantCulture),
            float f => Convert.ToDecimal(f, CultureInfo.InvariantCulture),
            long l => l,
            int i => i,
            string s when decimal.TryParse(
                s.Replace(",", "."),
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out var parsed
            ) => parsed,
            _ => 0m
        };
    }
}