using VarthexComanda.Desktop.Atendimento;

namespace VarthexComanda.Desktop.Tests.Atendimento;

public class FakeConfirmador : IConfirmador
{
    public bool ProximaResposta { get; set; } = true;

    public bool Confirmar(string titulo, string mensagem) => ProximaResposta;
}
