using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VarthexComanda.Infrastructure.Configuracao;
using VarthexComanda.Infrastructure.Persistence;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Configuracao;

public class EfConfiguracaoRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _provedor;
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabrica;

    public EfConfiguracaoRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"varthex-configuracao-tests-{Guid.NewGuid()}.db");

        var servicos = new ServiceCollection();
        servicos.AddDbContextFactory<VarthexComandaDbContext>(options =>
            options.UseSqlite($"Data Source={_dbPath};Foreign Keys=True"));
        _provedor = servicos.BuildServiceProvider();
        _fabrica = _provedor.GetRequiredService<IDbContextFactory<VarthexComandaDbContext>>();

        using var contexto = _fabrica.CreateDbContext();
        contexto.Database.Migrate();
    }

    public void Dispose()
    {
        _provedor.Dispose();
        SqliteConnection.ClearAllPools();
        if (File.Exists(_dbPath)) File.Delete(_dbPath);
    }

    [Fact]
    public void ObterValor_ChaveInexistente_RetornaNull()
    {
        var repositorio = new EfConfiguracaoRepository(_fabrica);

        var valor = repositorio.ObterValor("chave.qualquer");

        Assert.Null(valor);
    }

    [Fact]
    public void Definir_GravaEObterValorLeDeVolta()
    {
        var repositorio = new EfConfiguracaoRepository(_fabrica);

        repositorio.Definir("estabelecimento.nome", "Lanchonete do Zé", DateTime.UtcNow);
        var valor = repositorio.ObterValor("estabelecimento.nome");

        Assert.Equal("Lanchonete do Zé", valor);
    }

    [Fact]
    public void Definir_ChamadoDuasVezesNaMesmaChave_AtualizaSemDuplicar()
    {
        var repositorio = new EfConfiguracaoRepository(_fabrica);

        repositorio.Definir("estabelecimento.nome", "Nome Antigo", DateTime.UtcNow);
        repositorio.Definir("estabelecimento.nome", "Nome Novo", DateTime.UtcNow);

        Assert.Equal("Nome Novo", repositorio.ObterValor("estabelecimento.nome"));

        using var contexto = _fabrica.CreateDbContext();
        Assert.Single(contexto.Configuracoes.Where(c => c.Chave == "estabelecimento.nome"));
    }
}
