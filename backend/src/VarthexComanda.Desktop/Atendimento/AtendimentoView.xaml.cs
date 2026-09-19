using System.ComponentModel;
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
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AtendimentoViewModel.ComandaAtual) && ViewModel.ComandaAtual is not null)
        {
            Dispatcher.BeginInvoke(new Action(() => PainelComanda.Focus()));
        }
    }
}
