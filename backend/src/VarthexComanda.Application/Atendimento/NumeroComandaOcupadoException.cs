namespace VarthexComanda.Application.Atendimento;

public class NumeroComandaOcupadoException : Exception
{
    public NumeroComandaOcupadoException() : base("Já existe uma comanda aberta com esse número.")
    {
    }
}
