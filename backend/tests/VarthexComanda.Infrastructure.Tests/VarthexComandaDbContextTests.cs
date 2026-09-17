using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests;

public class VarthexComandaDbContextTests : IDisposable
{
    private readonly string _dbPath;

    public VarthexComandaDbContextTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"varthex-comanda-tests-{Guid.NewGuid()}.db");
    }

    public void Dispose()
    {
        // No Windows, o Microsoft.Data.Sqlite mantém conexões nativas no pool mesmo após
        // o Dispose() do DbContext/SqliteConnection, o que impede a exclusão do arquivo
        // temporário logo em seguida. Limpar o pool libera o handle do arquivo.
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    private VarthexComandaDbContext CriarContexto()
    {
        var options = new DbContextOptionsBuilder<VarthexComandaDbContext>()
            .UseSqlite($"Data Source={_dbPath};Foreign Keys=True")
            .Options;
        return new VarthexComandaDbContext(options);
    }

    [Fact]
    public void Migrate_EmBancoVazio_CriaTodasAsSeteTabelas()
    {
        using var contexto = CriarContexto();

        contexto.Database.Migrate();

        using var conexao = new SqliteConnection($"Data Source={_dbPath}");
        conexao.Open();
        using var comando = conexao.CreateCommand();
        // A partir do EF Core 9, o SqliteHistoryRepository também cria a tabela de bloqueio
        // "__EFMigrationsLock" (usada para serializar chamadas concorrentes a Migrate()),
        // que permanece no banco após a migração. Ela é uma tabela interna do EF Core, assim
        // como "__EFMigrationsHistory", e não faz parte do schema de domínio da aplicação.
        comando.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' AND name NOT LIKE '__EFMigrations%'";
        using var leitor = comando.ExecuteReader();
        var tabelas = new List<string>();
        while (leitor.Read()) tabelas.Add(leitor.GetString(0));

        Assert.Equal(
            new[] { "backup_registro", "categoria", "comanda", "configuracao", "item_comanda", "produto", "venda" },
            tabelas.OrderBy(t => t).ToArray());
    }

    [Fact]
    public void Migrate_ChamadoDuasVezes_NaoFalhaNemDuplicaEstrutura()
    {
        using (var primeiraExecucao = CriarContexto())
        {
            primeiraExecucao.Database.Migrate();
        }

        using var segundaExecucao = CriarContexto();
        segundaExecucao.Database.Migrate();

        var integridade = segundaExecucao.Database
            .SqlQueryRaw<string>("PRAGMA integrity_check")
            .AsEnumerable()
            .Single();
        Assert.Equal("ok", integridade);
    }

    [Fact]
    public void ComandaNumero_PermiteRepetirQuandoNaoEstaAberta_MasNaoQuandoDuasEstaoAbertas()
    {
        using var contexto = CriarContexto();
        contexto.Database.Migrate();

        contexto.Comandas.Add(new Comanda
        {
            Id = 1,
            Numero = 10,
            Status = StatusComanda.Fechada,
            AbertaEm = DateTime.UtcNow.AddHours(-2),
            FechadaEm = DateTime.UtcNow.AddHours(-1),
            TotalCentavos = 1000
        });
        contexto.Comandas.Add(new Comanda
        {
            Id = 2,
            Numero = 10,
            Status = StatusComanda.Aberta,
            AbertaEm = DateTime.UtcNow,
            FechadaEm = null,
            TotalCentavos = 0
        });
        contexto.SaveChanges();

        contexto.Comandas.Add(new Comanda
        {
            Id = 3,
            Numero = 10,
            Status = StatusComanda.Aberta,
            AbertaEm = DateTime.UtcNow,
            FechadaEm = null,
            TotalCentavos = 0
        });

        Assert.Throws<DbUpdateException>(() => contexto.SaveChanges());
    }

    [Fact]
    public void ForeignKeys_EstaoAtivas_ImpedeExcluirCategoriaComProdutoVinculado()
    {
        var agora = DateTime.UtcNow;

        // Insere categoria + produto vinculado num contexto próprio, que é descartado em
        // seguida. Isso garante que o produto dependente não fique rastreado (tracked) no
        // contexto usado para o Remove abaixo: se ambos estivessem no mesmo change tracker,
        // o próprio EF Core detectaria a relação obrigatória "rompida" em memória e lançaria
        // InvalidOperationException antes mesmo de enviar o DELETE ao SQLite — o que testaria
        // a validação do EF, não a PRAGMA foreign_keys do banco. Usando um contexto novo, sem
        // o produto carregado, o DELETE é enviado de fato ao SQLite, que é quem precisa barrar
        // a exclusão via ON DELETE RESTRICT.
        using (var preparacao = CriarContexto())
        {
            preparacao.Database.Migrate();
            preparacao.Categorias.Add(new Categoria { Id = 1, Nome = "Bebidas", Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
            preparacao.Produtos.Add(new Produto
            {
                Id = 1, CategoriaId = 1, Nome = "Refrigerante", PrecoCentavos = 500,
                Ativo = true, CriadoEm = agora, AtualizadoEm = agora
            });
            preparacao.SaveChanges();
        }

        using var contexto = CriarContexto();
        var categoria = contexto.Categorias.Single(c => c.Id == 1);
        contexto.Categorias.Remove(categoria);

        Assert.Throws<DbUpdateException>(() => contexto.SaveChanges());
    }
}
