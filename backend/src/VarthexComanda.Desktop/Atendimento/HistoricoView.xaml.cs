using System.Windows.Controls;

namespace VarthexComanda.Desktop.Atendimento;

public partial class HistoricoView : UserControl
{
    public HistoricoViewModel ViewModel { get; }

    public HistoricoView(HistoricoViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }
}
