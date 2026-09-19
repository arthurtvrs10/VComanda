using System.Globalization;
using Serilog;
using VarthexComanda.Application.Catalogo;

namespace VarthexComanda.Infrastructure.Storage;

public class ArquivoFotoStorage : IFotoStorage
{
    public const long TamanhoMaximoPadraoBytes = 10L * 1024 * 1024;

    private static readonly HashSet<string> ExtensoesPermitidas =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".bmp" };

    private readonly AppPaths _paths;
    private readonly ILogger _logger;
    private readonly long _tamanhoMaximoBytes;

    public ArquivoFotoStorage(AppPaths paths, ILogger logger, long tamanhoMaximoBytes = TamanhoMaximoPadraoBytes)
    {
        _paths = paths;
        _logger = logger;
        _tamanhoMaximoBytes = tamanhoMaximoBytes;
    }

    public string Importar(string caminhoOrigem)
    {
        if (string.IsNullOrWhiteSpace(caminhoOrigem) || !File.Exists(caminhoOrigem))
        {
            throw new FotoInvalidaException("Arquivo de imagem não encontrado.");
        }

        var extensao = Path.GetExtension(caminhoOrigem);
        if (!ExtensoesPermitidas.Contains(extensao))
        {
            throw new FotoInvalidaException("Formato não suportado. Use JPG, PNG ou BMP.");
        }

        if (new FileInfo(caminhoOrigem).Length > _tamanhoMaximoBytes)
        {
            var megabytes = (_tamanhoMaximoBytes / 1024d / 1024d).ToString("0.##", CultureInfo.GetCultureInfo("pt-BR"));
            throw new FotoInvalidaException($"A imagem excede o tamanho máximo de {megabytes} MB.");
        }

        var nome = $"{Guid.NewGuid():N}{extensao.ToLowerInvariant()}";
        Directory.CreateDirectory(_paths.FotosDirectory);
        File.Copy(caminhoOrigem, Path.Combine(_paths.FotosDirectory, nome));
        return nome;
    }

    public void Excluir(string nomeArquivo)
    {
        if (string.IsNullOrWhiteSpace(nomeArquivo))
        {
            return;
        }

        var caminho = Path.Combine(_paths.FotosDirectory, Path.GetFileName(nomeArquivo));
        try
        {
            if (File.Exists(caminho))
            {
                File.Delete(caminho);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(ex, "Não foi possível excluir a foto {Arquivo}", nomeArquivo);
        }
    }
}
