using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Backup;

public class CriarBackupManual
{
    private readonly IBackupService _backupService;

    public CriarBackupManual(IBackupService backupService)
    {
        _backupService = backupService;
    }

    public Resultado<BackupRegistro> Executar(string? pastaExterna)
    {
        var resultado = _backupService.CriarBackupGerenciado();
        if (pastaExterna is not null && resultado.Sucesso)
        {
            try
            {
                _backupService.CriarBackupExterno(pastaExterna);
            }
            catch (Exception)
            {
                // falha na cópia externa não invalida o backup gerenciado, que já teve sucesso
            }
        }

        return resultado;
    }
}
