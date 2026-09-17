using System.Windows.Controls;

namespace VarthexComanda.Desktop.Atendimento;

public partial class AtendimentoView : UserControl
{
    public AtendimentoView(AtendimentoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
