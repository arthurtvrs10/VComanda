namespace VarthexComanda.Application.Backup;

public class ValidarBackup
{
    private readonly IBackupService _backupService;

    public ValidarBackup(IBackupService backupService)
    {
        _backupService = backupService;
    }

    public RelatorioValidacao Executar(string caminhoArquivo) => _backupService.Validar(caminhoArquivo);
}
