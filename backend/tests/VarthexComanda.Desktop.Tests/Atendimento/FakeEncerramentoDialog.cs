using VarthexComanda.Desktop.Atendimento;

namespace VarthexComanda.Desktop.Tests.Atendimento;

public class FakeEncerramentoDialog : IEncerramentoDialog
{
    public bool ProximaResposta { get; set; } = true;
    public int? ComandaIdRecebida { get; private set; }

    public bool Abrir(int comandaId)
    {
        ComandaIdRecebida = comandaId;
        return ProximaResposta;
    }
}
