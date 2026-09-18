using VarthexComanda.Domain;

namespace VarthexComanda.Application.Backup;

public interface IBackupRegistroRepository
{
    void Registrar(BackupRegistro registro);
    IReadOnlyList<BackupRegistro> ListarRecentes(int quantidade);
    bool ExisteBackupHoje(DateTime inicioUtc, DateTime fimUtc);
}
