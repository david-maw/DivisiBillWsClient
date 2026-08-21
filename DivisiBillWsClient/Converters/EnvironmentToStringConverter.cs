using DivisiBillWsClient.ViewModels;
using System.Globalization;

namespace DivisiBillWsClient.Converters;

public class EnvironmentToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is EnvironmentOption environment)
        {
            return environment.ToString();
        }
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is string str && Enum.TryParse<EnvironmentOption>(str, out var environment))
        {
            return environment;
        }
        return EnvironmentOption.Development;
    }
}
