using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Desktop.Catalogo;
using VarthexComanda.Infrastructure.Concurrency;
using VarthexComanda.Infrastructure.Logging;
using VarthexComanda.Infrastructure.Persistence;
using VarthexComanda.Infrastructure.Persistence.Atendimento;
using VarthexComanda.Infrastructure.Persistence.Catalogo;
using VarthexComanda.Infrastructure.Storage;
using VarthexComanda.Infrastructure.Time;

namespace VarthexComanda.Desktop;

public partial class App : System.Windows.Application
{
    private SingleInstanceGuard? _guard;
    private ILogger? _logger;
    private ServiceProvider? _serviceProvider;

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

        DispatcherUnhandledException += (sender, args) =>
        {
            _logger?.Error(args.Exception, "Erro nao tratado na interface");
            MessageBox.Show(
                "Ocorreu um erro inesperado no Varthex Comanda. Consulte os logs.",
                "Varthex Comanda",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        var services = new ServiceCollection();
        services.AddSingleton(paths);
        services.AddSingleton(_logger);
        services.AddSingleton<IClock, SystemClock>();
        services.AddDbContextFactory<VarthexComandaDbContext>(options =>
            options.UseSqlite($"Data Source={paths.DatabasePath};Foreign Keys=True"));
        services.AddTransient<ICategoriaRepository, EfCategoriaRepository>();
        services.AddTransient<IProdutoRepository, EfProdutoRepository>();
        services.AddTransient<CadastrarCategoria>();
        services.AddTransient<AlterarCategoria>();
        services.AddTransient<ListarCategoriasAtivas>();
        services.AddTransient<CadastrarProduto>();
        services.AddTransient<AlterarProduto>();
        services.AddTransient<DesativarProduto>();
        services.AddTransient<PesquisarProdutos>();
        services.AddTransient<IComandaRepository, EfComandaRepository>();
        services.AddTransient<AbrirComanda>();
        services.AddTransient<AdicionarItem>();
        services.AddTransient<AlterarQuantidade>();
        services.AddTransient<RemoverItem>();
        services.AddTransient<CancelarComanda>();
        services.AddTransient<IConfirmador, MessageBoxConfirmador>();
        services.AddTransient<AtendimentoViewModel>();
        services.AddTransient<AtendimentoView>();
        services.AddTransient<ProdutosViewModel>();
        services.AddTransient<ProdutosView>();
        services.AddTransient<MainWindow>();

        _serviceProvider = services.BuildServiceProvider();

        try
        {
            var factory = _serviceProvider.GetRequiredService<IDbContextFactory<VarthexComandaDbContext>>();
            var clock = _serviceProvider.GetRequiredService<IClock>();
            using var dbContext = factory.CreateDbContext();

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

            _serviceProvider.GetRequiredService<MainWindow>().Show();
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
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Information("Encerrando Varthex Comanda");
        _serviceProvider?.Dispose();
        (_logger as IDisposable)?.Dispose();
        _guard?.Dispose();
        base.OnExit(e);
    }
}
