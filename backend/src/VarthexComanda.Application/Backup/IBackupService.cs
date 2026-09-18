using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Backup;

public interface IBackupService
{
    Resultado<BackupRegistro> CriarBackupGerenciado();
    Resultado<BackupRegistro> CriarBackupExterno(string pastaExterna);
    RelatorioValidacao Validar(string caminhoArquivo);
    Resultado<BackupRegistro> RestaurarPara(string caminhoArquivo);
}
