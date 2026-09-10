# World Clock Widget for Windows 11

[![CI](https://github.com/Git-Grant-Rogers/windows11-widget-world-clock/actions/workflows/ci.yml/badge.svg)](https://github.com/Git-Grant-Rogers/windows11-widget-world-clock/actions/workflows/ci.yml)

A world clock for the Windows 11 **Widgets Board** (Win+W). It shows the current time in the
cities you choose, with the day and hour difference from your own time zone, and updates every
minute while the board is open.

| Size   | What you see                                                    |
| ------ | --------------------------------------------------------------- |
| Small  | Your primary city: big time, city name, "Tomorrow, +9 hrs"       |
| Medium | Up to four cities, one per row                                   |
| Large  | Up to eight cities                                               |

Choose **Customize widget** from the widget's `…` menu to add or remove cities, reorder them,
give a city a custom name, and switch between 12-hour and 24-hour time. Tapping the widget
opens the built-in Windows Clock app.

> The built-in Clock app does not expose its world-clock list or a widget of its own, so this
> project is a standalone widget provider built with the Windows App SDK.

## Project layout

```
WorldClockWidget.sln
├─ src/WorldClockWidget.Core/        Platform-neutral logic (net10.0): settings, time-zone maths,
│                                    Adaptive Card templates and data payloads. Unit-tested.
├─ src/WorldClockWidget/             The widget provider (net10.0-windows): packaged MSIX app that
│                                    the Widgets Board activates as an out-of-process COM server.
├─ tests/WorldClockWidget.Core.Tests xunit tests for the Core library.
└─ tools/generate-assets.py          Regenerates the placeholder PNG logos and picker screenshots.
```

The provider mirrors Microsoft's
[Windows App SDK widgets sample](https://github.com/microsoft/WindowsAppSDK-Samples/tree/main/Samples/Widgets)
(single-project MSIX, `IWidgetProvider` + `IWidgetProvider2`, COM class factory) and follows the
[Windows widget design guidance](https://learn.microsoft.com/windows/apps/design/widgets/)
for sizes, typography, margins and theming.

## Prerequisites

- Windows 11, version 22H2 (build 22621) or later, with **Developer Mode** turned on
  (Settings › System › For developers).
- Visual Studio 2022 17.10 or later with the **WinUI application development** workload
  (this brings in the Windows App SDK, single-project MSIX tooling and the Windows 11 SDK).
- .NET 10 SDK (installed by the workload above).

## Build, deploy and try it

1. Open `WorldClockWidget.sln` in Visual Studio.
2. Set the solution platform to **x64** (or **ARM64** on an Arm PC).
3. Right-click the **WorldClockWidget** project and choose **Deploy**. This builds the Core
   library, packages the provider as MSIX, and installs it on your machine.
4. Press **Win+W** to open the Widgets Board, click **Add widgets** (the `+` in the top right),
   and pick **World clock** under *World Clock Widget*.

The Widgets Board starts `WorldClockWidget.exe -RegisterProcessAsComServer` on demand. To debug,
use the launch profile *Widget provider (attach when the Widgets Board starts it)*, or attach to
the running `WorldClockWidget.exe` from **Debug › Attach to Process**.

From a Developer Command Prompt you can also build and produce a package without Visual Studio:

```powershell
msbuild WorldClockWidget.sln /restore /p:Configuration=Release /p:Platform=x64 /p:GenerateAppxPackageOnBuild=true
```

To run the unit tests on any OS:

```bash
dotnet test tests/WorldClockWidget.Core.Tests
```

Continuous integration (`.github/workflows/ci.yml`) runs those tests on Ubuntu and compiles the
provider on Windows for x64 and ARM64 on every pull request.

## How it works

- `Package.appxmanifest` declares a `windows.comServer` extension (so the OS can activate the
  exe) and a `com.microsoft.windows.widgets` app extension that registers the `WorldClock_Widget`
  definition with its sizes, icon and picker screenshots. The COM CLSID appears there twice and
  once more on the `WidgetProvider` class; keep all three identical.
- `WidgetProvider` is a thin COM-visible shim. `WidgetService` owns the pinned-widget registry,
  the user's settings, and a timer aligned to the minute boundary that pushes fresh data to every
  visible widget. The timer runs only between `Activate` and `Deactivate`, so nothing ticks while
  the board is closed.
- The card UI is two Adaptive Card templates embedded in the Core library. The clock template
  uses `$host.widgetSize` to switch between the small layout and the list layout; the
  customization template is shown while a widget is in *Customize widget* mode. Everything
  dynamic is bound from a JSON data payload, so minute updates send data only.
- Settings live in `settings.json` under the package's local data folder
  (`%LOCALAPPDATA%\Packages\GrantRogers.WorldClockWidget_<hash>\LocalState`). A `logs\provider.log`
  file next to it records what the provider is doing, which is the quickest way to diagnose a
  widget that shows the system error state.

## Design notes

- Typography follows the widget type ramp: city names are *Body Strong* (default, bolder),
  captions are *Caption* (small, lighter, subtle), times are *Subtitle* (large, bolder) in the list
  and *Title* (extra large, bolder) on the small widget.
- Colours are left to the host so light and dark themes are handled automatically.
- Hour differences are formatted the way the Windows Clock app does ("+9 hrs", "−5.5 hrs"),
  paired with "Today", "Tomorrow" or "Yesterday" relative to your own date.
- The Widgets Board renders Adaptive Cards, which are static between updates, so the widget shows
  hours and minutes rather than a ticking seconds display.

## Before publishing

- Replace the generated placeholder images in `src/WorldClockWidget/Assets` and
  `src/WorldClockWidget/ProviderAssets` with real artwork. The picker screenshot must be
  300 × 304 px with transparent rounded corners and should show the medium widget.
- Set the `Publisher` in `Package.appxmanifest` to match your signing certificate (Visual Studio
  does this when you choose a certificate under *Packaging*).
- Bump the package `Version` for each release; Windows only installs a higher version over an
  existing one.
