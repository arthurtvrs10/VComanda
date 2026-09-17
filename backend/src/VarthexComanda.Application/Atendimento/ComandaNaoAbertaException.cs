namespace VarthexComanda.Application.Atendimento;

public class ComandaNaoAbertaException : Exception
{
    public ComandaNaoAbertaException() : base("A comanda não está aberta.")
    {
    }
}
