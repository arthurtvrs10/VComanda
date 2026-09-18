using System.Globalization;
using System.Windows.Data;
using VarthexComanda.Application.Atendimento;

namespace VarthexComanda.Desktop.Backup;

public class UtcParaDataHoraLocalConverter : IValueConverter
{
    private static readonly CultureInfo CulturaData = CultureInfo.GetCultureInfo("pt-BR");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is DateTime utc ? FusoBrasilia.ParaLocal(utc).ToString("dd/MM/yyyy HH:mm", CulturaData) : string.Empty;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
