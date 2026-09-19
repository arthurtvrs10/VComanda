using System.Windows;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Desktop.Backup;
using VarthexComanda.Desktop.Catalogo;
using VarthexComanda.Desktop.Configuracao;

namespace VarthexComanda.Desktop;

public partial class MainWindow : Window
{
    private readonly AtendimentoView _atendimentoView;
    private readonly ProdutosView _produtosView;
    private readonly HistoricoView _historicoView;
    private readonly BackupView _backupView;
    private readonly ConfiguracaoView _configuracaoView;

    public MainWindow(AtendimentoView atendimentoView, ProdutosView produtosView, HistoricoView historicoView, BackupView backupView, ConfiguracaoView configuracaoView)
    {
        InitializeComponent();
        _atendimentoView = atendimentoView;
        _produtosView = produtosView;
        _historicoView = historicoView;
        _backupView = backupView;
        _configuracaoView = configuracaoView;
        ConteudoPrincipal.Content = _atendimentoView;
    }

    private void MostrarAtendimento_Click(object sender, RoutedEventArgs e)
    {
        _atendimentoView.ViewModel.AtualizarCategorias();
        _atendimentoView.ViewModel.AtualizarComandasAbertas();
        ConteudoPrincipal.Content = _atendimentoView;
    }

    private void MostrarProdutos_Click(object sender, RoutedEventArgs e)
    {
        ConteudoPrincipal.Content = _produtosView;
    }

    private void MostrarHistorico_Click(object sender, RoutedEventArgs e)
    {
        _historicoView.ViewModel.AtualizarVendas();
        ConteudoPrincipal.Content = _historicoView;
    }

    private void MostrarBackup_Click(object sender, RoutedEventArgs e)
    {
        _backupView.ViewModel.AtualizarLista();
        ConteudoPrincipal.Content = _backupView;
    }

    private void MostrarConfiguracao_Click(object sender, RoutedEventArgs e)
    {
        _configuracaoView.ViewModel.Carregar();
        ConteudoPrincipal.Content = _configuracaoView;
    }
}
