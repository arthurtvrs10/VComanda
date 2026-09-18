using System.Globalization;
using System.Windows.Data;
using VarthexComanda.Application.Atendimento;

namespace VarthexComanda.Desktop.Atendimento;

public class UtcParaHorarioLocalConverter : IValueConverter
{
    private static readonly CultureInfo CulturaHorario = CultureInfo.GetCultureInfo("pt-BR");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTime utc ? FusoBrasilia.ParaLocal(utc).ToString("HH:mm", CulturaHorario) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
