using VarthexComanda.Application.Configuracao;

namespace VarthexComanda.Application.Tests.Configuracao;

public class FakeConfiguracaoRepository : IConfiguracaoRepository
{
    private readonly Dictionary<string, string> _valores = new();

    public string? ObterValor(string chave) => _valores.TryGetValue(chave, out var valor) ? valor : null;

    public void Definir(string chave, string valor, DateTime atualizadoEm) => _valores[chave] = valor;
}
