using System.Diagnostics;
using System.Globalization;

namespace WorldClockWidget.Services;

/// <summary>
/// Minimal file logger. The Widgets Board gives almost no feedback when a provider misbehaves,
/// so a small trace in the package's local folder is the fastest way to see what happened.
/// The file is truncated once it grows past <see cref="MaxBytes"/>.
/// </summary>
internal static class DiagnosticLog
{
    private const long MaxBytes = 512 * 1024;
    private static readonly Lock s_gate = new();
    private static string? s_path;

    public static void Initialize(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            s_path = Path.Combine(directory, "provider.log");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            s_path = null;
        }
    }

    public static void Info(string message) => Write("INFO", message);

    public static void Error(string message, Exception? exception = null) =>
        Write("ERROR", exception is null ? message : $"{message}{Environment.NewLine}{exception}");

    private static void Write(string level, string message)
    {
        string line = string.Create(CultureInfo.InvariantCulture, $"{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz} [{level}] {message}");
        Debug.WriteLine(line);

        if (s_path is null)
        {
            return;
        }

        lock (s_gate)
        {
            try
            {
                var file = new FileInfo(s_path);
                if (file.Exists && file.Length > MaxBytes)
                {
                    file.Delete();
                }

                File.AppendAllText(s_path, line + Environment.NewLine);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Logging must never take the provider down.
            }
        }
    }
}
