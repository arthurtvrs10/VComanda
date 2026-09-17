namespace VarthexComanda.Application.Catalogo;

public class Resultado<T> where T : class
{
    private Resultado(bool sucesso, T? valor, IReadOnlyList<string> erros)
    {
        Sucesso = sucesso;
        Valor = valor;
        Erros = erros;
    }

    public bool Sucesso { get; }
    public T? Valor { get; }
    public IReadOnlyList<string> Erros { get; }

    public static Resultado<T> Ok(T valor) => new(true, valor, Array.Empty<string>());
    public static Resultado<T> Falha(params string[] erros) => new(false, default, erros);
}
