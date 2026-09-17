using VarthexComanda.Application.Abstractions;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Domain;

namespace VarthexComanda.Application.Atendimento;

public class AbrirComanda
{
    private readonly IComandaRepository _comandas;
    private readonly IClock _relogio;

    public AbrirComanda(IComandaRepository comandas, IClock relogio)
    {
        _comandas = comandas;
        _relogio = relogio;
    }

    public Resultado<Comanda> Executar(int numero)
    {
        if (numero <= 0)
        {
            return Resultado<Comanda>.Falha("Informe um número de comanda válido.");
        }

        try
        {
            return Resultado<Comanda>.Ok(_comandas.AbrirComanda(numero, _relogio.UtcNow));
        }
        catch (NumeroComandaOcupadoException ex)
        {
            return Resultado<Comanda>.Falha(ex.Message);
        }
    }
}
