using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace VarthexComanda.Desktop.Configuracao;

public partial class ConfiguracaoView : UserControl
{
    public ConfiguracaoViewModel ViewModel { get; }

    public ConfiguracaoView(ConfiguracaoViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
    }

    private void EscolherPasta_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFolderDialog { Title = "Escolher pasta de backup externa" };
        if (dialogo.ShowDialog() == true)
        {
            ViewModel.DefinirPastaBackupExterna(dialogo.FolderName);
        }
    }

    private void LimparPasta_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.DefinirPastaBackupExterna(null);
    }
}
