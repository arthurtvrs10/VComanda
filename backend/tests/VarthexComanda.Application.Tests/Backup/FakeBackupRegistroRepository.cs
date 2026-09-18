using VarthexComanda.Application.Backup;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Tests.Backup;

public class FakeBackupRegistroRepository : IBackupRegistroRepository
{
    private readonly List<BackupRegistro> _registros = new();
    private int _proximoId = 1;

    public bool ExisteBackupHojeRetorno { get; set; }

    public void Registrar(BackupRegistro registro)
    {
        registro.Id = _proximoId++;
        _registros.Add(registro);
    }

    public IReadOnlyList<BackupRegistro> ListarRecentes(int quantidade) =>
        _registros.OrderByDescending(r => r.CriadoEm).Take(quantidade).ToList();

    public bool ExisteBackupHoje(DateTime inicioUtc, DateTime fimUtc) => ExisteBackupHojeRetorno;
}
