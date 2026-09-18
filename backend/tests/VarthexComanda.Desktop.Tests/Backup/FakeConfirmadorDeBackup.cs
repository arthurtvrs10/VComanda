using VarthexComanda.Desktop.Atendimento;

namespace VarthexComanda.Desktop.Tests.Backup;

public class FakeConfirmadorDeBackup : IConfirmador
{
    public bool ProximaResposta { get; set; } = true;

    public bool Confirmar(string titulo, string mensagem) => ProximaResposta;
}
