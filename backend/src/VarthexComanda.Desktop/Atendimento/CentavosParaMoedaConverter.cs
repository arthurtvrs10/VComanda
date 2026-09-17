using System.Globalization;
using System.Windows.Data;

namespace VarthexComanda.Desktop.Atendimento;

public class CentavosParaMoedaConverter : IValueConverter
{
    private static readonly CultureInfo CulturaMoeda = CultureInfo.GetCultureInfo("pt-BR");

    public static string Formatar(long centavos) => "R$ " + (centavos / 100m).ToString("0.00", CulturaMoeda);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is long centavos ? Formatar(centavos) : Formatar(0);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
