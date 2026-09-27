# HTTP Response Time Plotter

A small Windows desktop program that polls two HTTP URLs on a fixed interval and plots their response times on a live chart.

By default it measures:

| Series | URL |
|---|---|
| Server 1 | `http://localhost:45245/v1/info` |
| Server 2 | `http://localhost:45246/v1/info` |

The default interval is **5 seconds**. You can change every setting from inside the program.

---

## Requirements

- Windows 10 or 11 (x64)
- [.NET 9 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/9.0) to run it
- .NET 9 SDK to build it

## Building

From the project folder:

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

This builds a single file, `publish\HttpResponsePlotter.exe`.

If the target machine doesn't have .NET installed, build a self-contained exe instead. It's larger (about 150 MB) but runs without any runtime:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o publish
```

For a quick debug run:

```bash
dotnet run
```

## Running

Double-click `HttpResponsePlotter.exe`. With the default settings, polling starts as soon as the window opens.

### Command-line options

| Option | Description |
|---|---|
| `--config <path>` or `-c <path>` | Use a different settings file. This lets you keep several setups, e.g. one per environment. If the file doesn't exist, it is created the first time you save settings. |

Example:

```bash
HttpResponsePlotter.exe --config C:\Code\plotter-staging.json
```

---

## Using the program

### Toolbar

| Button | Shortcut | What it does |
|---|---|---|
| **Start** | F5 | Begin polling |
| **Stop** | Shift+F5 | Stop polling. Requests that are still running are cancelled. |
| **Clear** | | Delete all collected samples |
| **Settings...** | | Open the settings dialog |
| **Export CSV...** | | Save all collected samples from both URLs to a CSV file, sorted by time |

### The chart

- **X axis:** the time of each request (local time).
- **Y axis:** response time in milliseconds. By default it scales automatically to fit the data.
- **Lines:** each successful sample is a point, and the points are joined by a line in that URL's color.
- **Failures:** a failed request breaks the line. It is drawn as an **✕** near the top of the chart, with a faint vertical line at that moment. Each URL's ✕ marks sit in their own row, so you can tell them apart.
- **Hover:** hold the mouse over the chart to see the nearest sample for each URL: its timestamp, response time in ms, and HTTP status code, or the error message if it failed.
- **Threshold line:** if you set a warning threshold, it appears as a dashed horizontal line.
- **Legend:** at the top of the chart, showing each URL's name, address and color.

### Status bar

The status bar shows whether polling is running and at what interval. For each enabled URL it also shows:

```
Server 1 (45245): last 3.2 ms | avg 4.1 | min 1.3 | max 38.5 | failures 0/120
```

These statistics cover every sample currently in memory (see *Max samples per URL*). Min, max and average only count successful samples.

### What counts as a failure

A sample is a failure if any of the following happens:

- The request takes longer than the **Timeout**.
- The connection fails (e.g. connection refused, DNS failure, TLS error).
- The server returns a non-2xx status code, and **Treat non-2xx as failure** is on (the default).

---

## Settings

Open **Settings...** to change any option. Clicking **OK** checks the values, saves them, and applies them right away. If polling was running, it restarts with the new settings. **Reset to defaults** restores the factory settings.

Settings are saved as JSON to `settings.json` in the same folder as `HttpResponsePlotter.exe`, or to the file given with `--config`. If that file can't be read, the program starts with default settings and saves a copy of the bad file as `settings.json.bad`.

> The program needs write access to its own folder to save settings and the default CSV log. Don't put it in a protected folder such as `C:\Program Files`; if you do, use `--config` and **CSV log path** to point to a writable location.

### 1. URL 1 / 2. URL 2

| Setting | Default | Description |
|---|---|---|
| URL | `http://localhost:45245/v1/info` / `http://localhost:45246/v1/info` | The address to measure. Must be an absolute `http://` or `https://` URL. |
| Name | `Server 1 (45245)` / `Server 2 (45246)` | Label shown in the legend, status bar, tooltip and CSV |
| Enabled | `true` | Turn polling and plotting for this URL on or off |
| Line color | blue / orange | Color of this URL's line and markers |

> Changing a URL deletes the samples already collected for it, because the old data came from a different address.

### 3. Polling

| Setting | Default | Description |
|---|---|---|
| Interval (seconds) | `5` | Time between polls. Fractions such as `0.5` are allowed; the range is 0.1–86400. |
| Timeout (seconds) | `10` | A request that takes longer than this counts as a failure |
| Start polling on launch | `true` | Start polling automatically when the program opens |
| Treat non-2xx as failure | `true` | Plot responses such as 404 or 500 as failures instead of normal points |

Both URLs are requested at the same time on each tick. The next round only starts once both requests have finished or timed out, so a slow server can't pile up overlapping requests. If a round takes longer than the interval, the missed ticks are skipped.

### 4. Request

| Setting | Default | Description |
|---|---|---|
| HTTP method | `GET` | `GET` or `HEAD` |
| Measure until | `FullBody` | `FullBody`: the timer stops once the whole response body has downloaded. `ResponseHeaders`: the timer stops as soon as the response headers arrive (roughly time to first byte). |
| Reuse connections (keep-alive) | `true` | If `false`, each request opens a new TCP connection, so connection setup time is included in the measurement |
| Follow redirects | `true` | Follow 3xx redirects automatically |
| Ignore certificate errors | `false` | Accept invalid or self-signed HTTPS certificates |
| Use system proxy | `true` | Send requests through the Windows proxy settings |
| User-Agent | `HttpResponsePlotter/1.0` | Value of the `User-Agent` header. Leave empty to send none. |
| Extra headers | *(empty)* | Extra request headers, one per line, in the form `Name: Value`. Example: `Authorization: Bearer abc123` |

### 5. Chart

| Setting | Default | Description |
|---|---|---|
| Time window (minutes) | `10` | How much recent history the chart shows. `0` shows everything collected. |
| Max samples per URL | `20000` | Once a URL has more samples than this, the oldest are discarded. At a 5-second interval, 20000 samples is about 27 hours. |
| Y axis max (ms) | `0` | A fixed top for the Y axis. `0` means automatic scaling. |
| Warning threshold (ms) | `0` | Draws a dashed line at this response time. `0` turns it off. |
| Line width | `2` | Line thickness, 0.5–10 |
| Show point markers | `true` | Draw a dot at every sample |
| Background color | white | Chart background |
| Text/axis color | dark gray | Color of labels, axes and the chart border |
| Grid color | light gray | Color of the grid lines |
| Threshold line color | red | Color of the warning threshold line |

### 6. Logging

| Setting | Default | Description |
|---|---|---|
| Log samples to CSV | `false` | Append every sample to a CSV file as it is measured |
| CSV log path | `HttpResponsePlotter.csv` in the same folder as the exe | The file to append to. The folder and header row are created if needed. |

If writing to the log file fails, the error is shown in the status bar and polling carries on.

---

## CSV format

Both the continuous log and **Export CSV...** use the same columns:

```
Timestamp,Name,Url,ResponseTimeMs,StatusCode,Success,Error
2026-09-27 13:26:28.548,Server 1 (45245),http://localhost:45245/v1/info,3.762,200,true,
2026-09-27 13:26:40.112,Server 2 (45246),http://localhost:45246/v1/info,,,false,No connection could be made because the target machine actively refused it.
```

| Column | Description |
|---|---|
| Timestamp | Local time the request was sent, `yyyy-MM-dd HH:mm:ss.fff` |
| Name | The URL's name from the settings |
| Url | The URL that was requested |
| ResponseTimeMs | Elapsed time in milliseconds, using `.` as the decimal separator. Empty if no response was received. |
| StatusCode | HTTP status code. Empty if no response was received. |
| Success | `true` or `false` |
| Error | Why the request failed. Empty on success. |

Fields that contain commas, quotes or line breaks are quoted using standard CSV rules, so the file opens directly in Excel.

---

## Example settings file

```json
{
  "Url1": "http://localhost:45245/v1/info",
  "Url1Name": "Server 1 (45245)",
  "Url1Enabled": true,
  "Url1ColorHex": "#1F77B4",
  "Url2": "http://localhost:45246/v1/info",
  "Url2Name": "Server 2 (45246)",
  "Url2Enabled": true,
  "Url2ColorHex": "#FF7F0E",
  "IntervalSeconds": 5,
  "TimeoutSeconds": 10,
  "AutoStart": true,
  "TreatNon2xxAsFailure": true,
  "Method": "GET",
  "MeasureUntil": "FullBody",
  "ReuseConnections": true,
  "FollowRedirects": true,
  "IgnoreCertificateErrors": false,
  "UseSystemProxy": true,
  "UserAgent": "HttpResponsePlotter/1.0",
  "ExtraHeaders": "",
  "TimeWindowMinutes": 10,
  "MaxSamples": 20000,
  "YAxisMaxMs": 0,
  "WarningThresholdMs": 0,
  "LineWidth": 2,
  "ShowMarkers": true,
  "ChartBackColorHex": "#FFFFFF",
  "ChartForeColorHex": "#333333",
  "GridColorHex": "#E6E6E6",
  "ThresholdColorHex": "#D62728",
  "LogToCsv": false,
  "CsvLogPath": "C:\\Code\\HttpResponsePlotterBuild\\publish\\HttpResponsePlotter.csv"
}
```

You can leave settings out of the file; missing ones use their default values. Colors are written as HTML hex values (`#RRGGBB`) or named colors (`Red`).

---

## How the measurement works

- Timing uses a high-resolution `Stopwatch`. The timer starts just before the request is sent and stops when the response headers arrive or the body finishes downloading, depending on **Measure until**.
- The HTTP work runs in the background, so a slow or unresponsive server never freezes the window.
- The first request to each server is usually slower, because it includes opening the TCP connection. With keep-alive on, later requests reuse that connection. Turn **Reuse connections** off to include connection setup time in every sample.

---

## Project structure

| File | Contents |
|---|---|
| `Program.cs` | Entry point and command-line parsing |
| `MainForm.cs` | Main window, polling loop, HTTP measurement, statistics, CSV logging and export |
| `ResponseTimeChart.cs` | The chart control: axes, grid, lines, failure markers, legend and hover tooltip |
| `SettingsForm.cs` | Settings dialog built on a `PropertyGrid` |
| `AppSettings.cs` | All settings with their defaults, descriptions, validation, and JSON load/save |
| `Models.cs` | `Sample` (one measurement) and `SeriesData` (the samples for one URL) |
| `HttpResponsePlotter.csproj` | Project file (.NET 9, WinForms, no NuGet dependencies) |

### Adding a new setting

1. Add a property to `AppSettings` with `[Category]`, `[DisplayName]` and `[Description]` attributes and a default value. It then shows up in the Settings dialog and is saved to JSON automatically.
2. For `Color` settings, follow the existing pattern: a `[JsonIgnore]` `Color` property backed by a hidden `...Hex` string property.
3. Add any range checks to `AppSettings.Validate()`.
4. Read the value where it's needed, in `MainForm` or `ResponseTimeChart`.

---

## Troubleshooting

| Symptom | Likely cause and fix |
|---|---|
| Every sample fails with *...target machine actively refused it* | Nothing is listening on that port. Start the service or check the URL. |
| Every sample fails with *HTTP 401/403* | The endpoint needs authentication. Add an `Authorization: ...` line under **Extra headers**. |
| HTTPS fails with a certificate error | The certificate is self-signed or invalid. Turn on **Ignore certificate errors** (only on trusted networks). |
| Requests to `localhost` are unexpectedly slow | They may be going through a proxy. Turn off **Use system proxy**. |
| The program won't start and asks for .NET | Install the .NET 9 Desktop Runtime, or build a self-contained exe (see [Building](#building)). |
| Settings seem to have been lost | Check whether the settings file was saved as `settings.json.bad` because it couldn't be read. Fix the JSON and rename the file back. |
