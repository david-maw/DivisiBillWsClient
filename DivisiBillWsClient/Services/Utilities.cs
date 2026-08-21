using CommunityToolkit.Mvvm.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace DivisiBillWsClient.Services;

internal static class Utilities
{
#if DEBUG
    public static readonly bool IsDebug = true;
#else
    public static readonly bool IsDebug = false;
#endif


#if WINDOWS
    public static readonly bool IsWinUI = true;
#else
    public static readonly bool IsWinUI = false;
#endif

    internal static void RecordMsg(string s) => Debug.WriteLine(s);
    internal static void ReportCrash(this Exception ex, string? message = null)
    {
        Debug.WriteLine($"Crash: {message} {ex}");
    }
    internal static string NameFromDateTime(DateTime dateTime) => dateTime.ToString("yyyyMMddHHmmss");
    public static void DebugMsg(string s) => Debug.WriteLine(s);

    [Conditional("DEBUG")]
    public static void DebugExamineStream(Stream streamParameter)
    {
        if (Debugger.IsAttached)
        {
            //Testing - normally used to allow stored XML to be examined in myString
            long savedPosition = streamParameter.Position;
            streamParameter.Position = 0;
            StreamReader sr = new(streamParameter);
#pragma warning disable IDE0059 // Unnecessary assignment of a value
            string myString = sr.ReadToEnd();
#pragma warning restore IDE0059 // Unnecessary assignment of a value
            streamParameter.Position = savedPosition;
        }
    }
    public static bool AreEqual(byte[] a, byte[] b)
    {
        if (a == null || b == null) return false;
        return a.SequenceEqual(b);
    }
    public static byte[] StreamToByteArray(Stream input)
    {
        var pos = input.Position;
        using var memoryStream = new MemoryStream();
        input.CopyTo(memoryStream);
        input.Position = pos; // restore the position of the input stream
        return memoryStream.ToArray();
    }
    internal static DateTime DateTimeFromName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return DateTime.MinValue;

        string s = Path.GetFileNameWithoutExtension(name);
        if (s.Length == 14
            && int.TryParse(s[..4], out int y)
            && y > 2010 && y < 2030
            && int.TryParse(s.AsSpan(4, 2), out int m)
            && m >= 1 && m <= 12
            && int.TryParse(s.AsSpan(6, 2), out int d)
            && d >= 1 && d <= 31
            && int.TryParse(s.AsSpan(8, 2), out int hh)
            && hh >= 0 && hh <= 23
            && int.TryParse(s.AsSpan(10, 2), out int mm)
            && mm >= 0 && mm < 60
            && int.TryParse(s.AsSpan(12, 2), out int ss)
            && ss >= 0 && ss < 60)
            return new DateTime(y, m, d, hh, mm, ss); // Plausible date
        else
            return DateTime.MinValue;
    }
    public static Task AlertAsync(string title, string message, string cancel = "OK") => Shell.Current.DisplayAlertAsync(title, message, cancel);
}
public class UtcToLocalDateTimeConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is DateTimeOffset utcOffset)
        {
            // Normalize to UTC, then convert to local time
            var localDateTime = utcOffset.ToUniversalTime().ToLocalTime().DateTime;
            return localDateTime;
        }

        return value; // fallback
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException("Reverse conversion is not supported.");
    }
}
public class BlobItemInformation
{
    public string? Name { get; set; }
    public string? ContentType { get; set; }
    public long Size { get; set; }
    public DateTimeOffset LastModified { get; set; }
}

/// <summary>
/// The public representation of data from the storage web service. Each remote item is either a
/// Person List, a Venue List or a Meal. The Name is the unique identifier for each item.
/// </summary>
public partial class RemoteItemInfo : ObservableObject
{
    public string? Name { get; set; }
    public long Size { get; set; }
    public string SizeText => $"{Size / 1000.0:f1} kB";
    public string? Description { get; set; } // An alias for the Summary field
    public bool HasRemoteImage { get; set; } = false; // This will be set to true if the image exists in blob storage
    public bool ReplaceRequested { get; set; } = false;

    [ObservableProperty]
    public partial bool IsEncrypted { get; set; } = false;

    [ObservableProperty]
    public partial bool Selected { get; set; } = false;

    public DateTime DateFromName => Utilities.DateTimeFromName(Name);
}
internal class InvertBoolConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => !(bool?)value ?? false;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => !(bool?)value ?? false;
}
