using Microsoft.EntityFrameworkCore;
using VarthexComanda.Application.Backup;
using VarthexComanda.Domain;
using VarthexComanda.Infrastructure.Persistence;

namespace VarthexComanda.Infrastructure.Backup;

public class EfBackupRegistroRepository : IBackupRegistroRepository
{
    private readonly IDbContextFactory<VarthexComandaDbContext> _fabricaContexto;

    public EfBackupRegistroRepository(IDbContextFactory<VarthexComandaDbContext> fabricaContexto)
    {
        _fabricaContexto = fabricaContexto;
    }

    public void Registrar(BackupRegistro registro)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        registro.Id = 0;
        contexto.BackupRegistros.Add(registro);
        contexto.SaveChanges();
    }

    public IReadOnlyList<BackupRegistro> ListarRecentes(int quantidade)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.BackupRegistros
            .OrderByDescending(b => b.CriadoEm)
            .Take(quantidade)
            .ToList();
    }

    public bool ExisteBackupHoje(DateTime inicioUtc, DateTime fimUtc)
    {
        using var contexto = _fabricaContexto.CreateDbContext();
        return contexto.BackupRegistros.Any(b =>
            b.Status == StatusBackup.Sucesso &&
            b.CriadoEm >= inicioUtc &&
            b.CriadoEm < fimUtc);
    }
}
