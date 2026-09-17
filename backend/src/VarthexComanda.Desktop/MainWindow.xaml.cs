using System.Windows;
using VarthexComanda.Desktop.Catalogo;

namespace VarthexComanda.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(ProdutosViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
