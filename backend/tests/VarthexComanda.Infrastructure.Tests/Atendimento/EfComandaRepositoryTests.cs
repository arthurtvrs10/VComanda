using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Atendimento;

public class EfComandaRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _provedor;
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabrica;

    public EfComandaRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"varthex-comanda-tests-{Guid.NewGuid()}.db");

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
    public void AbrirComanda_NumeroLivre_CriaComandaAberta()
    {
        var repositorio = new EfComandaRepository(_fabrica);

        var comanda = repositorio.AbrirComanda(10, DateTime.UtcNow);

        Assert.True(comanda.Id > 0);
        Assert.Equal(StatusComanda.Aberta, comanda.Status);
        Assert.Equal(0, comanda.TotalCentavos);
    }

    [Fact]
    public void AbrirComanda_MesmoNumeroDuasVezes_SegundaLancaExcecao()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        repositorio.AbrirComanda(10, DateTime.UtcNow);

        Assert.Throws<VarthexComanda.Application.Atendimento.NumeroComandaOcupadoException>(
            () => repositorio.AbrirComanda(10, DateTime.UtcNow));
    }

    [Fact]
    public void ListarAbertas_RetornaSomenteAbertasOrdenadasPorNumero()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        repositorio.AbrirComanda(20, DateTime.UtcNow);
        repositorio.AbrirComanda(10, DateTime.UtcNow);

        var abertas = repositorio.ListarAbertas();

        Assert.Equal(new[] { 10, 20 }, abertas.Select(c => c.Numero).ToArray());
    }

    [Fact]
    public void BuscarComItens_ComandaSemItens_RetornaListaVazia()
    {
        var repositorio = new EfComandaRepository(_fabrica);
        var comanda = repositorio.AbrirComanda(10, DateTime.UtcNow);

        var detalhe = repositorio.BuscarComItens(comanda.Id);

        Assert.NotNull(detalhe);
        Assert.Empty(detalhe!.Itens);
    }

    [Fact]
    public void BuscarComItens_ComandaInexistente_RetornaNull()
    {
        var repositorio = new EfComandaRepository(_fabrica);

        var detalhe = repositorio.BuscarComItens(999);

        Assert.Null(detalhe);
    }
}
