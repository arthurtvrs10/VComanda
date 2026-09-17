using System.Windows.Controls;

namespace VarthexComanda.Desktop.Catalogo;

public partial class ProdutosView : UserControl
{
    public ProdutosView(ProdutosViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
