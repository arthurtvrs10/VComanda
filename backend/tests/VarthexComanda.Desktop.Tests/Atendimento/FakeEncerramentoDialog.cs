using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Atendimento;
using VarthexComanda.Desktop.Atendimento;

namespace VarthexComanda.Desktop.Tests.Atendimento;

public class FakeEncerramentoDialog : IEncerramentoDialog
{
    private readonly EncerrarComanda _encerrarComanda;

    public FakeEncerramentoDialog(IComandaRepository comandas, IClock relogio)
    {
        _encerrarComanda = new EncerrarComanda(comandas, relogio);
    }

    public bool ProximaResposta { get; set; } = true;
    public int? ComandaIdRecebida { get; private set; }

    public bool Abrir(int comandaId)
    {
        ComandaIdRecebida = comandaId;
        if (!ProximaResposta)
        {
            return false;
        }

        var resultado = _encerrarComanda.Executar(comandaId);
        return resultado.Sucesso;
    }
}
