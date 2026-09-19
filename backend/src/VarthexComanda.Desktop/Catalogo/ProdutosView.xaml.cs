using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace VarthexComanda.Desktop.Catalogo;

public partial class ProdutosView : UserControl
{
    public ProdutosView(ProdutosViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void EscolherFoto_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Escolher foto do produto",
            Filter = "Imagens (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp"
        };

        if (dialogo.ShowDialog() == true && DataContext is ProdutosViewModel viewModel)
        {
            viewModel.DefinirFoto(dialogo.FileName);
        }
    }
}
