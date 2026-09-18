using VarthexComanda.Domain;

namespace VarthexComanda.Application.Backup;

public class ListarBackupsRecentes
{
    private readonly IBackupRegistroRepository _registros;

    public ListarBackupsRecentes(IBackupRegistroRepository registros)
    {
        _registros = registros;
    }

    public IReadOnlyList<BackupRegistro> Executar(int quantidade = 20) => _registros.ListarRecentes(quantidade);
}
