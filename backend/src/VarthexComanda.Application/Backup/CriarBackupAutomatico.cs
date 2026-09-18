using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Atendimento;

namespace VarthexComanda.Application.Backup;

public class CriarBackupAutomatico
{
    private readonly IBackupService _backupService;
    private readonly IBackupRegistroRepository _registros;
    private readonly IClock _relogio;

    public CriarBackupAutomatico(IBackupService backupService, IBackupRegistroRepository registros, IClock relogio)
    {
        _backupService = backupService;
        _registros = registros;
        _relogio = relogio;
    }

    public void Executar(bool incondicional = false)
    {
        try
        {
            if (!incondicional)
            {
                var hoje = FusoBrasilia.ParaLocal(_relogio.UtcNow).Date;
                var inicioUtc = FusoBrasilia.ParaUtc(hoje);
                var fimUtc = FusoBrasilia.ParaUtc(hoje.AddDays(1));
                if (_registros.ExisteBackupHoje(inicioUtc, fimUtc))
                {
                    return;
                }
            }

            _backupService.CriarBackupGerenciado();
        }
        catch (Exception)
        {
            // backup automático nunca deve interromper o app
        }
    }
}
