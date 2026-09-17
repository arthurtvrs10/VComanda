using System.Windows;
using VarthexComanda.Desktop.Catalogo;

namespace VarthexComanda.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(ProdutosView produtosView)
    {
        InitializeComponent();
        ConteudoPrincipal.Content = produtosView;
    }
}
