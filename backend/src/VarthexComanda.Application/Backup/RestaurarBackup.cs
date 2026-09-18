using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Backup;

public class RestaurarBackup
{
    private readonly IBackupService _backupService;

    public RestaurarBackup(IBackupService backupService)
    {
        _backupService = backupService;
    }

    public Resultado<BackupRegistro> Executar(string caminhoArquivo)
    {
        var relatorio = _backupService.Validar(caminhoArquivo);
        if (!relatorio.Aprovado)
        {
            return Resultado<BackupRegistro>.Falha(relatorio.Motivo);
        }

        return _backupService.RestaurarPara(caminhoArquivo);
    }
}
