using Serilog;
using VarthexComanda.Application.Catalogo;
using VarthexComanda.Infrastructure.Storage;
using Xunit;

namespace VarthexComanda.Infrastructure.Tests.Storage;

public class ArquivoFotoStorageTests : IDisposable
{
    private readonly string _raiz = Path.Combine(Path.GetTempPath(), $"varthex-fotos-tests-{Guid.NewGuid()}");
    private readonly AppPaths _paths;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    public ArquivoFotoStorageTests()
    {
        _paths = new AppPaths(_raiz);
        _paths.EnsureCreated();
    }

    public void Dispose()
    {
        if (Directory.Exists(_raiz))
        {
            Directory.Delete(_raiz, true);
        }
    }

    private string CriarArquivoOrigem(string nome, int bytes = 16)
    {
        var caminho = Path.Combine(_raiz, nome);
        File.WriteAllBytes(caminho, new byte[bytes]);
        return caminho;
    }

    [Fact]
    public void Importar_ArquivoValido_CopiaParaPastaDeFotosERetornaNome()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var origem = CriarArquivoOrigem("foto.png");

        var nome = storage.Importar(origem);

        Assert.EndsWith(".png", nome);
        Assert.True(File.Exists(Path.Combine(_paths.FotosDirectory, nome)));
        Assert.True(File.Exists(origem));
    }

    [Fact]
    public void Importar_ExtensaoMaiuscula_NomeGuardadoEmMinusculas()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var origem = CriarArquivoOrigem("FOTO.JPG");

        var nome = storage.Importar(origem);

        Assert.EndsWith(".jpg", nome);
    }

    [Fact]
    public void Importar_MesmoArquivoDuasVezes_GeraNomesDiferentes()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var origem = CriarArquivoOrigem("foto.png");

        var primeiro = storage.Importar(origem);
        var segundo = storage.Importar(origem);

        Assert.NotEqual(primeiro, segundo);
    }

    [Fact]
    public void Importar_ExtensaoNaoSuportada_LancaFotoInvalida()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var origem = CriarArquivoOrigem("documento.pdf");

        var excecao = Assert.Throws<FotoInvalidaException>(() => storage.Importar(origem));

        Assert.Equal("Formato não suportado. Use JPG, PNG ou BMP.", excecao.Message);
    }

    [Fact]
    public void Importar_ArquivoInexistente_LancaFotoInvalida()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);

        var excecao = Assert.Throws<FotoInvalidaException>(() => storage.Importar(Path.Combine(_raiz, "nao-existe.png")));

        Assert.Equal("Arquivo de imagem não encontrado.", excecao.Message);
    }

    [Fact]
    public void Importar_ArquivoMaiorQueOLimite_LancaFotoInvalida()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger, tamanhoMaximoBytes: 8);
        var origem = CriarArquivoOrigem("grande.png", bytes: 16);

        Assert.Throws<FotoInvalidaException>(() => storage.Importar(origem));
    }

    [Fact]
    public void Excluir_ArquivoExistente_Remove()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var nome = storage.Importar(CriarArquivoOrigem("foto.png"));

        storage.Excluir(nome);

        Assert.False(File.Exists(Path.Combine(_paths.FotosDirectory, nome)));
    }

    [Fact]
    public void Excluir_ArquivoInexistente_NaoLanca()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);

        var excecao = Record.Exception(() => storage.Excluir("nao-existe.png"));

        Assert.Null(excecao);
    }

    [Fact]
    public void Excluir_NomeComTravessiaDeDiretorio_UsaSoONomeDoArquivo()
    {
        var storage = new ArquivoFotoStorage(_paths, _logger);
        var foraDaPasta = CriarArquivoOrigem("fora.jpg");

        storage.Excluir("..\\fora.jpg");

        Assert.True(File.Exists(foraDaPasta));
    }
}
