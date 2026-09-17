using System.Windows.Controls;

namespace VarthexComanda.Desktop.Atendimento;

public partial class AtendimentoView : UserControl
{
    public AtendimentoViewModel ViewModel { get; }

    public AtendimentoView(AtendimentoViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }
}
