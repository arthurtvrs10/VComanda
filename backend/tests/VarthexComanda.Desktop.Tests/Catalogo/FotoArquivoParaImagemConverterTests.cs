using System.Globalization;
using System.Windows.Media.Imaging;
using VarthexComanda.Desktop.Catalogo;
using Xunit;

namespace VarthexComanda.Desktop.Tests.Catalogo;

public class FotoArquivoParaImagemConverterTests : IDisposable
{
    private const string PngUmPixelBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg==";

    private readonly string _pasta = Path.Combine(Path.GetTempPath(), $"varthex-conv-tests-{Guid.NewGuid()}");
    private readonly string? _diretorioOriginal = FotoArquivoParaImagemConverter.DiretorioFotos;
    private readonly FotoArquivoParaImagemConverter _conversor = new();

    public FotoArquivoParaImagemConverterTests()
    {
        Directory.CreateDirectory(_pasta);
        FotoArquivoParaImagemConverter.DiretorioFotos = _pasta;
    }

    public void Dispose()
    {
        FotoArquivoParaImagemConverter.DiretorioFotos = _diretorioOriginal;
        if (Directory.Exists(_pasta))
        {
            Directory.Delete(_pasta, true);
        }
    }

    private object? Converter(object? valor) =>
        _conversor.Convert(valor, typeof(System.Windows.Media.ImageSource), null!, CultureInfo.InvariantCulture);

    [Fact]
    public void Convert_ValorNuloOuVazio_RetornaNulo()
    {
        Assert.Null(Converter(null));
        Assert.Null(Converter(string.Empty));
    }

    [Fact]
    public void Convert_DiretorioNaoDefinido_RetornaNulo()
    {
        FotoArquivoParaImagemConverter.DiretorioFotos = null;

        Assert.Null(Converter("foto.png"));
    }

    [Fact]
    public void Convert_ArquivoInexistente_RetornaNulo()
    {
        Assert.Null(Converter("nao-existe.png"));
    }

    [Fact]
    public void Convert_ArquivoCorrompido_RetornaNulo()
    {
        File.WriteAllBytes(Path.Combine(_pasta, "quebrada.png"), new byte[] { 1, 2, 3, 4 });

        Assert.Null(Converter("quebrada.png"));
    }

    [Fact]
    public void Convert_ImagemValida_RetornaBitmapCongelado()
    {
        File.WriteAllBytes(Path.Combine(_pasta, "ok.png"), System.Convert.FromBase64String(PngUmPixelBase64));

        var resultado = Converter("ok.png");

        var imagem = Assert.IsType<BitmapImage>(resultado);
        Assert.True(imagem.IsFrozen);
    }

    [Fact]
    public void Convert_ImagemValida_NaoTravaOArquivo()
    {
        var caminho = Path.Combine(_pasta, "livre.png");
        File.WriteAllBytes(caminho, System.Convert.FromBase64String(PngUmPixelBase64));

        Converter("livre.png");

        var excecao = Record.Exception(() => File.Delete(caminho));
        Assert.Null(excecao);
    }

    private static object? ConverterCaminho(object? valor) =>
        new CaminhoArquivoParaImagemConverter().Convert(valor, typeof(System.Windows.Media.ImageSource), null!, CultureInfo.InvariantCulture);

    [Fact]
    public void Caminho_NuloOuVazio_RetornaNulo()
    {
        Assert.Null(ConverterCaminho(null));
        Assert.Null(ConverterCaminho(string.Empty));
        Assert.Null(ConverterCaminho("   "));
    }

    [Fact]
    public void Caminho_ArquivoInexistente_RetornaNulo()
    {
        Assert.Null(ConverterCaminho(Path.Combine(_pasta, "nao-existe.png")));
    }

    [Fact]
    public void Caminho_ArquivoCorrompido_RetornaNulo()
    {
        var caminho = Path.Combine(_pasta, "quebrada.png");
        File.WriteAllBytes(caminho, new byte[] { 1, 2, 3, 4 });

        Assert.Null(ConverterCaminho(caminho));
    }

    [Fact]
    public void Caminho_ImagemValida_RetornaBitmapCongelado()
    {
        var caminho = Path.Combine(_pasta, "ok.png");
        File.WriteAllBytes(caminho, System.Convert.FromBase64String(PngUmPixelBase64));

        var imagem = Assert.IsType<BitmapImage>(ConverterCaminho(caminho));

        Assert.True(imagem.IsFrozen);
    }

    [Fact]
    public void Caminho_ImagemValida_NaoTravaOArquivo()
    {
        var caminho = Path.Combine(_pasta, "livre.png");
        File.WriteAllBytes(caminho, System.Convert.FromBase64String(PngUmPixelBase64));

        ConverterCaminho(caminho);

        var excecao = Record.Exception(() => File.Delete(caminho));
        Assert.Null(excecao);
    }
}
