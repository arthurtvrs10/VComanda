using VarthexComanda.Application.Catalogo;

namespace VarthexComanda.Application.Tests.Catalogo;

public class FakeFotoStorage : IFotoStorage
{
    private int _contador;

    public List<string> Importados { get; } = new();
    public List<string> Excluidos { get; } = new();
    public string? MensagemRejeicao { get; set; }

    public string Importar(string caminhoOrigem)
    {
        if (MensagemRejeicao is not null)
        {
            throw new FotoInvalidaException(MensagemRejeicao);
        }

        var nome = $"foto{++_contador}.jpg";
        Importados.Add(nome);
        return nome;
    }

    public void Excluir(string nomeArquivo) => Excluidos.Add(nomeArquivo);
}
