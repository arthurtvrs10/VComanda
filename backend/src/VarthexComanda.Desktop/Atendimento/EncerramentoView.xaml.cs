using System.Windows;

namespace VarthexComanda.Desktop.Atendimento;

public partial class EncerramentoView : Window
{
    public EncerramentoView(EncerramentoViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        viewModel.Concluido += (sender, sucesso) =>
        {
            DialogResult = sucesso;
            Close();
        };
    }
}
