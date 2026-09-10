using System.Text.Json;
using System.Text.Json.Serialization;
using WorldClockWidget.Core.Models;

namespace WorldClockWidget.Core.Settings;

/// <summary>
/// Loads and saves <see cref="ClockSettings"/> as a JSON file. Reads are forgiving (a missing
/// or corrupt file yields the defaults); writes are atomic so a crash mid-save cannot leave a
/// half-written file behind.
/// </summary>
public sealed class SettingsStore
{
    public const string FileName = "settings.json";

    private readonly string _path;
    private readonly Func<TimeZoneInfo> _localZone;
    private readonly Lock _gate = new();

    /// <param name="directory">Folder the settings file lives in. Created on first save.</param>
    /// <param name="localZone">Supplies the local zone used to pick defaults; defaults to <see cref="TimeZoneInfo.Local"/>.</param>
    public SettingsStore(string directory, Func<TimeZoneInfo>? localZone = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        _path = Path.Combine(directory, FileName);
        _localZone = localZone ?? (() => TimeZoneInfo.Local);
    }

    /// <summary>Full path of the settings file, mainly for diagnostics.</summary>
    public string FilePath => _path;

    public ClockSettings Load()
    {
        lock (_gate)
        {
            try
            {
                if (File.Exists(_path))
                {
                    using FileStream stream = File.OpenRead(_path);
                    ClockSettings? loaded = JsonSerializer.Deserialize(stream, SettingsJsonContext.Default.ClockSettings);
                    if (loaded is not null && loaded.SchemaVersion == ClockSettings.CurrentSchemaVersion)
                    {
                        Sanitise(loaded);
                        return loaded;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                // Fall through to defaults; a bad settings file should never break the widget.
            }

            return ClockSettings.CreateDefault(_localZone());
        }
    }

    public void Save(ClockSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (_gate)
        {
            string? directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string tempPath = _path + ".tmp";
            using (FileStream stream = File.Create(tempPath))
            {
                JsonSerializer.Serialize(stream, settings, SettingsJsonContext.Default.ClockSettings);
            }

            File.Move(tempPath, _path, overwrite: true);
        }
    }

    private static void Sanitise(ClockSettings settings)
    {
        settings.Clocks ??= [];
        settings.Clocks.RemoveAll(entry => entry is null || string.IsNullOrWhiteSpace(entry.TimeZoneId));

        if (settings.Clocks.Count > ClockSettings.MaxClocks)
        {
            settings.Clocks.RemoveRange(ClockSettings.MaxClocks, settings.Clocks.Count - ClockSettings.MaxClocks);
        }
    }
}

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ClockSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext
{
}
