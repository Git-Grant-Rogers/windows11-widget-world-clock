using System.Globalization;
using System.Text.Json;
using Microsoft.Windows.Widgets;
using Microsoft.Windows.Widgets.Providers;
using Windows.Storage;
using Windows.System;
using WorldClockWidget.Core.Cards;
using WorldClockWidget.Core.Clock;
using WorldClockWidget.Core.Models;
using WorldClockWidget.Core.Settings;
using WorldClockWidget.Core.TimeZones;
using WorldClockWidget.Widgets;
using CoreWidgetSize = WorldClockWidget.Core.Cards.WidgetSize;

namespace WorldClockWidget.Services;

/// <summary>
/// The real widget logic, shared by every <see cref="WidgetProvider"/> COM instance the host
/// creates. Owns the settings, the pinned-widget registry and the once-a-minute refresh, and
/// translates card actions into settings changes.
/// </summary>
internal sealed class WidgetService : IDisposable
{
    /// <summary>The manifest <c>Definition Id</c> this provider serves.</summary>
    public const string DefinitionId = "WorldClock_Widget";

    /// <summary>
    /// If the host starts us but never pins a widget, exit after this long rather than linger.
    /// </summary>
    private static readonly TimeSpan s_emptyIdleTimeout = TimeSpan.FromMinutes(2);

    private static readonly Lazy<WidgetService> s_singleton = new(() => new WidgetService());

    private readonly Lock _gate = new();
    private readonly Dictionary<string, WidgetInstance> _widgets = new(StringComparer.Ordinal);
    private readonly SettingsStore _store;
    private readonly MinuteRefreshTimer _refreshTimer;
    private readonly ManualResetEventSlim _shutdownRequested = new(false);
    private readonly Timer _emptyIdleTimer;
    private readonly ClockSettings _settings;
    private bool _recovered;

    private WidgetService()
    {
        string dataFolder = ResolveDataFolder();
        DiagnosticLog.Initialize(Path.Combine(dataFolder, "logs"));
        DiagnosticLog.Info($"Provider starting. Data folder: {dataFolder}");

        _store = new SettingsStore(dataFolder);
        _settings = _store.Load();
        _refreshTimer = new MinuteRefreshTimer(OnMinuteTick);
        _emptyIdleTimer = new Timer(_ => OnEmptyIdleTimeout(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
    }

    public static WidgetService Instance => s_singleton.Value;

    /// <summary>Signalled when the process should exit: no widgets remain pinned.</summary>
    public WaitHandle ShutdownRequested => _shutdownRequested.WaitHandle;

    /// <summary>
    /// Re-attaches to widgets that were pinned before this process started (after a reboot,
    /// an update or a crash) and pushes fresh content to each of them.
    /// </summary>
    public void RecoverRunningWidgets()
    {
        lock (_gate)
        {
            if (_recovered)
            {
                return;
            }

            _recovered = true;
        }

        try
        {
            WidgetManager manager = WidgetManager.GetDefault();
            foreach (WidgetInfo info in manager.GetWidgetInfos())
            {
                WidgetContext context = info.WidgetContext;
                if (!string.Equals(context.DefinitionId, DefinitionId, StringComparison.Ordinal))
                {
                    DiagnosticLog.Info($"Deleting widget {context.Id} with unknown definition '{context.DefinitionId}'.");
                    manager.DeleteWidget(context.Id);
                    continue;
                }

                WidgetInstance instance = Register(context.Id, ToCoreSize(context.Size));
                DiagnosticLog.Info($"Recovered widget {context.Id} ({instance.Size}).");
                Push(instance, includeTemplate: true);
            }
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Failed to recover running widgets.", ex);
        }

        ArmEmptyIdleTimerIfNeeded();
    }

    public void CreateWidget(WidgetContext context)
    {
        WidgetInstance instance = Register(context.Id, ToCoreSize(context.Size));
        DiagnosticLog.Info($"CreateWidget {context.Id} ({instance.Size}).");
        Push(instance, includeTemplate: true);
    }

    public void DeleteWidget(string widgetId)
    {
        bool anyLeft;
        lock (_gate)
        {
            _widgets.Remove(widgetId);
            anyLeft = _widgets.Count > 0;
            UpdateRefreshTimerLocked();
        }

        DiagnosticLog.Info($"DeleteWidget {widgetId}. Remaining: {(anyLeft ? "some" : "none")}.");

        if (!anyLeft)
        {
            RequestShutdown("last widget unpinned");
        }
    }

    public void Activate(WidgetContext context)
    {
        // The user may have changed their time zone since we last looked.
        TimeZoneInfo.ClearCachedData();

        WidgetInstance? instance;
        lock (_gate)
        {
            if (!_widgets.TryGetValue(context.Id, out instance))
            {
                instance = Register(context.Id, ToCoreSize(context.Size));
            }

            instance.IsActive = true;
            instance.Size = ToCoreSize(context.Size);
            UpdateRefreshTimerLocked();
        }

        Push(instance, includeTemplate: false);
    }

    public void Deactivate(string widgetId)
    {
        lock (_gate)
        {
            if (_widgets.TryGetValue(widgetId, out WidgetInstance? instance))
            {
                instance.IsActive = false;
            }

            UpdateRefreshTimerLocked();
        }
    }

    public void OnWidgetContextChanged(WidgetContextChangedArgs args)
    {
        WidgetContext context = args.WidgetContext;
        WidgetInstance? instance;
        lock (_gate)
        {
            if (!_widgets.TryGetValue(context.Id, out instance))
            {
                return;
            }

            instance.Size = ToCoreSize(context.Size);
        }

        DiagnosticLog.Info($"Widget {context.Id} resized to {instance.Size}.");
        Push(instance, includeTemplate: false);
    }

    public void OnCustomizationRequested(WidgetCustomizationRequestedArgs args)
    {
        string widgetId = args.WidgetContext.Id;
        WidgetInstance? instance;
        lock (_gate)
        {
            if (!_widgets.TryGetValue(widgetId, out instance))
            {
                return;
            }

            instance.InCustomization = true;
        }

        DiagnosticLog.Info($"Widget {widgetId} entered customization.");
        Push(instance, includeTemplate: true);
    }

    public void OnActionInvoked(WidgetActionInvokedArgs args)
    {
        string widgetId = args.WidgetContext.Id;
        string verb = args.Verb ?? string.Empty;
        DiagnosticLog.Info($"Action '{verb}' on widget {widgetId}.");

        if (string.Equals(verb, WidgetVerbs.OpenClockApp, StringComparison.Ordinal))
        {
            OpenClockApp();
            return;
        }

        WidgetInstance? instance;
        lock (_gate)
        {
            if (!_widgets.TryGetValue(widgetId, out instance))
            {
                return;
            }
        }

        ActionData data = ActionData.Parse(args.Data);
        bool settingsChanged = ApplyCustomizationAction(verb, data);

        if (string.Equals(verb, WidgetVerbs.ExitCustomization, StringComparison.Ordinal))
        {
            lock (_gate)
            {
                instance.InCustomization = false;
            }
        }

        if (settingsChanged)
        {
            SaveSettings();
            PushAll(includeTemplate: false, except: instance);
        }

        // The acting widget always gets a full refresh so the card reflects the new state
        // (or switches back from the customization template).
        Push(instance, includeTemplate: true);
    }

    public void Dispose()
    {
        _refreshTimer.Dispose();
        _emptyIdleTimer.Dispose();
        _shutdownRequested.Dispose();
    }

    private bool ApplyCustomizationAction(string verb, ActionData data)
    {
        bool changed = false;

        lock (_gate)
        {
            // Every customization action carries the toggle's current value, so persist it each time.
            if (data.Use24Hour is bool use24Hour && _settings.Use24HourClock != use24Hour)
            {
                _settings.Use24HourClock = use24Hour;
                changed = true;
            }

            switch (verb)
            {
                case WidgetVerbs.AddClock:
                    changed |= AddClockLocked(data);
                    break;

                case WidgetVerbs.RemoveClock when data.TimeZoneId is not null:
                    changed |= _settings.RemoveClock(data.TimeZoneId);
                    break;

                case WidgetVerbs.MoveClockUp when data.TimeZoneId is not null:
                    changed |= _settings.MoveClockUp(data.TimeZoneId);
                    break;

                default:
                    break;
            }
        }

        return changed;
    }

    private bool AddClockLocked(ActionData data)
    {
        TimeZoneInfo? zone = TimeZoneCatalog.TryFind(data.TimeZoneId);
        if (zone is null)
        {
            DiagnosticLog.Info($"Ignoring add for unknown time zone '{data.TimeZoneId}'.");
            return false;
        }

        string label = string.IsNullOrWhiteSpace(data.Label)
            ? TimeZoneCatalog.GetFriendlyLabel(zone)
            : data.Label.Trim();

        return _settings.TryAddClock(new ClockEntry(zone.Id, label));
    }

    private void SaveSettings()
    {
        try
        {
            ClockSettings snapshot;
            lock (_gate)
            {
                snapshot = _settings;
            }

            _store.Save(snapshot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            DiagnosticLog.Error("Could not save settings.", ex);
        }
    }

    private void OnMinuteTick()
    {
        PushAll(includeTemplate: false, except: null, activeOnly: true);
    }

    private void PushAll(bool includeTemplate, WidgetInstance? except, bool activeOnly = false)
    {
        List<WidgetInstance> targets;
        lock (_gate)
        {
            targets = _widgets.Values
                .Where(w => !ReferenceEquals(w, except))
                .Where(w => !activeOnly || w.IsActive)
                .ToList();
        }

        foreach (WidgetInstance instance in targets)
        {
            Push(instance, includeTemplate);
        }
    }

    private void Push(WidgetInstance instance, bool includeTemplate)
    {
        try
        {
            ClockSettings settings;
            bool inCustomization;
            CoreWidgetSize size;
            lock (_gate)
            {
                settings = _settings;
                inCustomization = instance.InCustomization;
                size = instance.Size;
            }

            var calculator = new WorldClockCalculator(TimeZoneInfo.Local, CultureInfo.CurrentCulture);
            var builder = new WidgetPayloadBuilder(calculator);
            DateTimeOffset now = DateTimeOffset.UtcNow;

            var options = new WidgetUpdateRequestOptions(instance.Id)
            {
                Data = inCustomization
                    ? builder.BuildCustomizationData(settings, now)
                    : builder.BuildClockData(settings, size, now),
            };

            // The customization card always needs its template because the host is switching
            // between two different layouts; the clock view only needs it on first show.
            if (includeTemplate || inCustomization)
            {
                options.Template = inCustomization ? WidgetTemplates.Customization : WidgetTemplates.Clock;
            }

            WidgetManager.GetDefault().UpdateWidget(options);
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error($"Failed to update widget {instance.Id}.", ex);
        }
    }

    private WidgetInstance Register(string widgetId, CoreWidgetSize size)
    {
        lock (_gate)
        {
            if (!_widgets.TryGetValue(widgetId, out WidgetInstance? instance))
            {
                instance = new WidgetInstance(widgetId, size);
                _widgets[widgetId] = instance;
            }
            else
            {
                instance.Size = size;
            }

            _emptyIdleTimer.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
            UpdateRefreshTimerLocked();
            return instance;
        }
    }

    private void UpdateRefreshTimerLocked()
    {
        bool anyActive = _widgets.Values.Any(w => w.IsActive && !w.InCustomization);
        if (anyActive)
        {
            _refreshTimer.Start();
        }
        else
        {
            _refreshTimer.Stop();
        }
    }

    private void ArmEmptyIdleTimerIfNeeded()
    {
        lock (_gate)
        {
            if (_widgets.Count == 0)
            {
                _emptyIdleTimer.Change(s_emptyIdleTimeout, Timeout.InfiniteTimeSpan);
            }
        }
    }

    private void OnEmptyIdleTimeout()
    {
        lock (_gate)
        {
            if (_widgets.Count > 0)
            {
                return;
            }
        }

        RequestShutdown("no widgets were pinned");
    }

    private void RequestShutdown(string reason)
    {
        DiagnosticLog.Info($"Shutting down: {reason}.");
        _refreshTimer.Stop();
        _shutdownRequested.Set();
    }

    private static void OpenClockApp()
    {
        try
        {
            // ms-clock: is the Windows Clock app's protocol; the call returns immediately.
            _ = Launcher.LaunchUriAsync(new Uri("ms-clock:"));
        }
        catch (Exception ex)
        {
            DiagnosticLog.Error("Could not launch the Clock app.", ex);
        }
    }

    private static CoreWidgetSize ToCoreSize(Microsoft.Windows.Widgets.WidgetSize size) => size switch
    {
        Microsoft.Windows.Widgets.WidgetSize.Small => CoreWidgetSize.Small,
        Microsoft.Windows.Widgets.WidgetSize.Large => CoreWidgetSize.Large,
        _ => CoreWidgetSize.Medium,
    };

    private static string ResolveDataFolder()
    {
        try
        {
            return ApplicationData.Current.LocalFolder.Path;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
            // No package identity (e.g. running the exe directly): fall back to a plain per-user folder.
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(local, "WorldClockWidget");
        }
    }

    /// <summary>
    /// The inputs and action data the host sends with <c>Action.Execute</c>, as one JSON object.
    /// </summary>
    private readonly record struct ActionData(string? TimeZoneId, string? Label, bool? Use24Hour)
    {
        public static ActionData Parse(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return default;
            }

            try
            {
                using JsonDocument document = JsonDocument.Parse(json);
                JsonElement root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                return new ActionData(
                    ReadString(root, WidgetInputs.TimeZoneId),
                    ReadString(root, WidgetInputs.Label),
                    ReadBool(root, WidgetInputs.Use24Hour));
            }
            catch (JsonException ex)
            {
                DiagnosticLog.Error("Action data was not valid JSON.", ex);
                return default;
            }
        }

        private static string? ReadString(JsonElement root, string name) =>
            root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static bool? ReadBool(JsonElement root, string name)
        {
            if (!root.TryGetProperty(name, out JsonElement value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => bool.TryParse(value.GetString(), out bool parsed) ? parsed : null,
                _ => null,
            };
        }
    }
}
