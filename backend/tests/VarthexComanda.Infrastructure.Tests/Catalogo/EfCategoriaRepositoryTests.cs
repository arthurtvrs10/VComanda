using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Persistence.Catalogo;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Catalogo;

public class EfCategoriaRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _provedor;
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabrica;

    public EfCategoriaRepositoryTests()
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
    public void Salvar_CategoriaNova_AtribuiIdEPersiste()
    {
        var repositorio = new EfCategoriaRepository(_fabrica);
        var agora = DateTime.UtcNow;

        var salva = repositorio.Salvar(new Categoria { Id = 0, Nome = "Bebidas", Ativo = true, CriadoEm = agora, AtualizadoEm = agora });

        Assert.True(salva.Id > 0);
        var carregada = repositorio.BuscarPorId(salva.Id);
        Assert.NotNull(carregada);
        Assert.Equal("Bebidas", carregada!.Nome);
    }

    [Fact]
    public void ListarAtivas_IgnoraInativas_OrdenaPorNome()
    {
        var repositorio = new EfCategoriaRepository(_fabrica);
        var agora = DateTime.UtcNow;
        repositorio.Salvar(new Categoria { Id = 0, Nome = "Sobremesas", Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        repositorio.Salvar(new Categoria { Id = 0, Nome = "Bebidas", Ativo = true, CriadoEm = agora, AtualizadoEm = agora });
        repositorio.Salvar(new Categoria { Id = 0, Nome = "Descontinuada", Ativo = false, CriadoEm = agora, AtualizadoEm = agora });

        var ativas = repositorio.ListarAtivas();

        Assert.Equal(new[] { "Bebidas", "Sobremesas" }, ativas.Select(c => c.Nome).ToArray());
    }

    [Fact]
    public void ExisteNome_ComparaSemDiferenciarMaiusculas_EIgnoraOProprioId()
    {
        var repositorio = new EfCategoriaRepository(_fabrica);
        var agora = DateTime.UtcNow;
        var categoria = repositorio.Salvar(new Categoria { Id = 0, Nome = "Bebidas", Ativo = true, CriadoEm = agora, AtualizadoEm = agora });

        Assert.True(repositorio.ExisteNome("BEBIDAS"));
        Assert.False(repositorio.ExisteNome("BEBIDAS", ignorarId: categoria.Id));
        Assert.False(repositorio.ExisteNome("Sobremesas"));
    }
}
