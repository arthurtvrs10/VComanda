using System;
using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VarthexComanda.Application.Backup;
using VarthexComanda.Desktop.Atendimento;
using VarthexComanda.Domain;

namespace VarthexComanda.Desktop.Backup;

public partial class BackupViewModel : ObservableObject
{
    private readonly CriarBackupManual _criarBackupManual;
    private readonly RestaurarBackup _restaurarBackup;
    private readonly ListarBackupsRecentes _listarBackupsRecentes;
    private readonly IConfirmador _confirmador;

    public event EventHandler? SolicitouReinicio;

    public BackupViewModel(
        CriarBackupManual criarBackupManual,
        RestaurarBackup restaurarBackup,
        ListarBackupsRecentes listarBackupsRecentes,
        IConfirmador confirmador)
    {
        _criarBackupManual = criarBackupManual;
        _restaurarBackup = restaurarBackup;
        _listarBackupsRecentes = listarBackupsRecentes;
        _confirmador = confirmador;

        Backups = new ObservableCollection<BackupRegistro>();
        AtualizarLista();
    }

    public ObservableCollection<BackupRegistro> Backups { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RestaurarCommand))]
    private BackupRegistro? backupSelecionado;

    [ObservableProperty]
    private string mensagem = string.Empty;

    public void AtualizarLista()
    {
        Backups.Clear();
        foreach (var registro in _listarBackupsRecentes.Executar(20))
        {
            Backups.Add(registro);
        }
    }

    [RelayCommand]
    private void CriarBackup(string? pastaExterna)
    {
        try
        {
            var resultado = _criarBackupManual.Executar(pastaExterna);
            Mensagem = resultado.Sucesso
                ? "Backup criado com sucesso."
                : string.Join(" ", resultado.Erros);
            AtualizarLista();
        }
        catch (Exception)
        {
            Mensagem = "Não foi possível criar o backup. Tente novamente.";
        }
    }

    [RelayCommand(CanExecute = nameof(PodeRestaurar))]
    private void Restaurar()
    {
        if (BackupSelecionado is null)
        {
            return;
        }

        var caminho = Path.Combine(BackupSelecionado.Destino, BackupSelecionado.Arquivo);
        var mensagemConfirmacao =
            $"Isso vai substituir todos os dados atuais pelo backup de {BackupSelecionado.CriadoEm:dd/MM/yyyy HH:mm}. " +
            "Uma cópia de segurança da base atual será criada antes.";
        if (!_confirmador.Confirmar("Restaurar backup", mensagemConfirmacao))
        {
            return;
        }

        try
        {
            var resultado = _restaurarBackup.Executar(caminho);
            if (!resultado.Sucesso)
            {
                Mensagem = string.Join(" ", resultado.Erros);
                return;
            }

            Mensagem = string.Empty;
            SolicitouReinicio?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            Mensagem = "Não foi possível restaurar o backup. Tente novamente.";
        }
    }

    private bool PodeRestaurar() => BackupSelecionado is not null;
}
