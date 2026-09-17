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
            DatabaseBackupService.BackupIfExists(paths.DatabasePath, paths.BackupsDirectory, clock.UtcNow);

            var connection = new SqliteConnection($"Data Source={paths.DatabasePath}");
            connection.Open();
            using (var pragma = connection.CreateCommand())
            {
                pragma.CommandText = "PRAGMA foreign_keys = ON;";
                pragma.ExecuteNonQuery();
            }

            var options = new DbContextOptionsBuilder<VarthexComandaDbContext>()
                .UseSqlite(connection, contextOwnsConnection: true)
                .Options;
            using var dbContext = new VarthexComandaDbContext(options);
            dbContext.Database.Migrate();

            var integridade = dbContext.Database
                .SqlQueryRaw<string>("PRAGMA integrity_check")
                .AsEnumerable()
                .Single();
            if (integridade != "ok")
            {
                _logger.Error("PRAGMA integrity_check retornou {Resultado}", integridade);
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
