using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace VarthexComanda.Desktop.Backup;

public partial class BackupView : UserControl
{
    public BackupViewModel ViewModel { get; }

    public BackupView(BackupViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        ViewModel.SolicitouReinicio += ViewModel_SolicitouReinicio;
    }

    private void EscolherPastaExterna_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFolderDialog { Title = "Escolher pasta externa para o backup" };
        if (dialogo.ShowDialog() == true)
        {
            ViewModel.CriarBackupCommand.Execute(dialogo.FolderName);
        }
    }

    private void SelecionarArquivo_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new OpenFileDialog
        {
            Title = "Selecionar arquivo de backup",
            Filter = "Banco de dados (*.db)|*.db|Todos os arquivos (*.*)|*.*"
        };
        if (dialogo.ShowDialog() == true)
        {
            ViewModel.RestaurarArquivoExterno(dialogo.FileName);
        }
    }

    private void ViewModel_SolicitouReinicio(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "Backup restaurado com sucesso. O Varthex Comanda vai reiniciar agora.",
            "Varthex Comanda",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        ((App)System.Windows.Application.Current).ReiniciarAplicativo();
    }
}
