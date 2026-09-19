using System.Globalization;
using System.IO;
using System.Windows.Data;

namespace VarthexComanda.Desktop.Catalogo;

/// <summary>
/// Converte um caminho ABSOLUTO (arquivo escolhido pelo proprio usuario) em imagem.
/// Nao restringe o diretorio, de proposito: diferente de <see cref="FotoArquivoParaImagemConverter"/>.
/// </summary>
public class CaminhoArquivoParaImagemConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string caminho || string.IsNullOrWhiteSpace(caminho) || !File.Exists(caminho))
        {
            return null;
        }

        return FotoArquivoParaImagemConverter.CarregarImagem(caminho);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
