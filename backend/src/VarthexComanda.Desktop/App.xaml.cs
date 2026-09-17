using System.Windows;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog;
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Infrastructure.Concurrency;
using VarthexComanda.Infrastructure.Logging;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Storage;
using VarthexComanda.Infrastructure.Time;

namespace VarthexComanda.Desktop;

public partial class App : System.Windows.Application
{
    private SingleInstanceGuard? _guard;
    private ILogger? _logger;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _guard = new SingleInstanceGuard("VarthexComanda.SingleInstance");
        if (!_guard.TryAcquire())
        {
            MessageBox.Show(
                "O Varthex Comanda já está aberto neste computador.",
                "Varthex Comanda",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            Shutdown();
            return;
        }

        var paths = new AppPaths();
        paths.EnsureCreated();

        _logger = LoggingConfigurator.CreateLogger(paths.LogsDirectory);
        _logger.Information("Iniciando Varthex Comanda");

        IClock clock = new SystemClock();

        try
        {
            var connection = new SqliteConnection($"Data Source={paths.DatabasePath};Foreign Keys=True");
            connection.Open();

            var options = new DbContextOptionsBuilder<VarthexComandaDbContext>()
                .UseSqlite(connection, contextOwnsConnection: true)
                .Options;
            using var dbContext = new VarthexComandaDbContext(options);

            var pendentes = dbContext.Database.GetPendingMigrations().ToList();
            if (pendentes.Count > 0)
            {
                var backupCriado = DatabaseBackupService.BackupIfExists(paths.DatabasePath, paths.BackupsDirectory, clock.UtcNow);
                if (backupCriado is not null)
                {
                    _logger.Information("Backup preventivo criado em {Caminho} antes de aplicar {Quantidade} migração(ões) pendente(s)", backupCriado, pendentes.Count);
                }
            }

            dbContext.Database.Migrate();

            var linhas = dbContext.Database
                .SqlQueryRaw<string>("PRAGMA integrity_check")
                .AsEnumerable()
                .ToList();
            if (linhas.Count != 1 || linhas[0] != "ok")
            {
                _logger.Error("PRAGMA integrity_check retornou {Linhas}", string.Join("; ", linhas));
                MessageBox.Show(
                    "O banco de dados do Varthex Comanda está corrompido. Restaure um backup antes de continuar.",
                    "Varthex Comanda",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown();
                return;
            }

            _logger.Information("Banco pronto em {Caminho}", paths.DatabasePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Falha ao preparar o banco de dados");
            MessageBox.Show(
                "Não foi possível preparar o banco de dados do Varthex Comanda. Consulte os logs.",
                "Varthex Comanda",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown();
            return;
        }

        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Information("Encerrando Varthex Comanda");
        (_logger as IDisposable)?.Dispose();
        _guard?.Dispose();
        base.OnExit(e);
    }
}
