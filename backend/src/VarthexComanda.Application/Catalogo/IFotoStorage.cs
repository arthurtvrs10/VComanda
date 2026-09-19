namespace VarthexComanda.Application.Catalogo;

public interface IFotoStorage
{
    /// <summary>Copia a imagem para o armazenamento do app e devolve o nome do arquivo guardado.</summary>
    /// <exception cref="FotoInvalidaException">Arquivo inexistente, formato não suportado ou grande demais.</exception>
    string Importar(string caminhoOrigem);

    /// <summary>Apaga a foto guardada. Nunca lança por falha de IO.</summary>
    void Excluir(string nomeArquivo);
}
