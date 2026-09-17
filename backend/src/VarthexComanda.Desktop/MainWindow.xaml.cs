using System.Windows;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Desktop.Catalogo;

namespace VarthexComanda.Desktop;

public partial class MainWindow : Window
{
    private readonly AtendimentoView _atendimentoView;
    private readonly ProdutosView _produtosView;

    public MainWindow(AtendimentoView atendimentoView, ProdutosView produtosView)
    {
        InitializeComponent();
        _atendimentoView = atendimentoView;
        _produtosView = produtosView;
        ConteudoPrincipal.Content = _atendimentoView;
    }

    private void MostrarAtendimento_Click(object sender, RoutedEventArgs e)
    {
        _atendimentoView.ViewModel.AtualizarCategorias();
        ConteudoPrincipal.Content = _atendimentoView;
    }

    private void MostrarProdutos_Click(object sender, RoutedEventArgs e)
    {
        ConteudoPrincipal.Content = _produtosView;
    }
}
