namespace VarthexComanda.Application.Configuracao;

public interface IConfiguracaoRepository
{
    string? ObterValor(string chave);
    void Definir(string chave, string valor, DateTime atualizadoEm);
}
