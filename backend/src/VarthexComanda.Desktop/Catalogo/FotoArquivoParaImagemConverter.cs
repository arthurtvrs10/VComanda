using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace VarthexComanda.Desktop.Catalogo;

public class FotoArquivoParaImagemConverter : IValueConverter
{
    public static string? DiretorioFotos { get; set; }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string nomeArquivo || string.IsNullOrWhiteSpace(nomeArquivo) || string.IsNullOrEmpty(DiretorioFotos))
        {
            return null;
        }

        var caminho = Path.Combine(DiretorioFotos, Path.GetFileName(nomeArquivo));
        if (!File.Exists(caminho))
        {
            return null;
        }

        try
        {
            var imagem = new BitmapImage();
            imagem.BeginInit();
            imagem.CacheOption = BitmapCacheOption.OnLoad;
            imagem.DecodePixelWidth = 320;
            imagem.UriSource = new Uri(caminho, UriKind.Absolute);
            imagem.EndInit();
            imagem.Freeze();
            return imagem;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or FileFormatException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
