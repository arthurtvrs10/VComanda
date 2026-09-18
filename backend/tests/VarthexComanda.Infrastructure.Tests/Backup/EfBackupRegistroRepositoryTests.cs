using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Backup;
using VarthexComanda.Infrastructure.Persistence;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Backup;

public class EfBackupRegistroRepositoryTests : IDisposable
{
    private readonly string _dbPath;
    private readonly ServiceProvider _provedor;
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabrica;

    public EfBackupRegistroRepositoryTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"varthex-backup-tests-{Guid.NewGuid()}.db");

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

    private static BackupRegistro CriarRegistro(DateTime criadoEmUtc, StatusBackup status = StatusBackup.Sucesso) => new()
    {
        Id = 0,
        Arquivo = $"varthex-comanda-{criadoEmUtc:yyyy-MM-dd-HHmmss}.db",
        Destino = "C:\\backups",
        CriadoEm = criadoEmUtc,
        Status = status,
        Checksum = "abc123",
        Mensagem = null
    };

    [Fact]
    public void Registrar_AtribuiIdEPersiste()
    {
        var repositorio = new EfBackupRegistroRepository(_fabrica);
        var registro = CriarRegistro(DateTime.UtcNow);

        repositorio.Registrar(registro);

        Assert.True(registro.Id > 0);
        Assert.Single(repositorio.ListarRecentes(10));
    }

    [Fact]
    public void ListarRecentes_OrdenaPorCriadoEmDescendente_ELimitaQuantidade()
    {
        var repositorio = new EfBackupRegistroRepository(_fabrica);
        repositorio.Registrar(CriarRegistro(new DateTime(2026, 9, 16, 8, 0, 0, DateTimeKind.Utc)));
        repositorio.Registrar(CriarRegistro(new DateTime(2026, 9, 18, 8, 0, 0, DateTimeKind.Utc)));
        repositorio.Registrar(CriarRegistro(new DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc)));

        var recentes = repositorio.ListarRecentes(2);

        Assert.Equal(2, recentes.Count);
        Assert.Equal(new DateTime(2026, 9, 18, 8, 0, 0, DateTimeKind.Utc), recentes[0].CriadoEm);
        Assert.Equal(new DateTime(2026, 9, 17, 8, 0, 0, DateTimeKind.Utc), recentes[1].CriadoEm);
    }

    [Fact]
    public void ExisteBackupHoje_ComRegistroNoIntervalo_RetornaTrue()
    {
        var repositorio = new EfBackupRegistroRepository(_fabrica);
        repositorio.Registrar(CriarRegistro(new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc)));

        var existe = repositorio.ExisteBackupHoje(
            new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.True(existe);
    }

    [Fact]
    public void ExisteBackupHoje_SemRegistroNoIntervalo_RetornaFalse()
    {
        var repositorio = new EfBackupRegistroRepository(_fabrica);
        repositorio.Registrar(CriarRegistro(new DateTime(2026, 9, 17, 14, 0, 0, DateTimeKind.Utc)));

        var existe = repositorio.ExisteBackupHoje(
            new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.False(existe);
    }

    [Fact]
    public void ExisteBackupHoje_ConsideraSomenteRegistrosDeSucesso()
    {
        var repositorio = new EfBackupRegistroRepository(_fabrica);
        repositorio.Registrar(CriarRegistro(new DateTime(2026, 9, 18, 14, 0, 0, DateTimeKind.Utc), StatusBackup.Falha));

        var existe = repositorio.ExisteBackupHoje(
            new DateTime(2026, 9, 18, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 19, 0, 0, 0, DateTimeKind.Utc));

        Assert.False(existe);
    }
}
