using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Text;

namespace HttpResponsePlotter;

internal sealed class MainForm : Form
{
    private readonly string _settingsPath;
    private AppSettings _settings;
    private readonly SeriesData[] _series = [new SeriesData(), new SeriesData()];
    private readonly ResponseTimeChart _chart = new() { Dock = DockStyle.Fill };

    private readonly ToolStripButton _startButton = new("Start") { ToolTipText = "Start polling (F5)" };
    private readonly ToolStripButton _stopButton = new("Stop") { ToolTipText = "Stop polling (Shift+F5)" };
    private readonly ToolStripStatusLabel _stateLabel = new() { AutoSize = true };
    private readonly ToolStripStatusLabel[] _statLabels =
        [new() { AutoSize = true, Margin = new Padding(12, 3, 0, 2) }, new() { AutoSize = true, Margin = new Padding(12, 3, 0, 2) }];

    private HttpClient? _client;
    private CancellationTokenSource? _cts;
    private int _generation;

    public MainForm(string settingsPath)
    {
        _settingsPath = settingsPath;
        _settings = AppSettings.Load(settingsPath);

        Text = "HTTP Response Time Plotter";
        Size = new Size(1100, 650);
        MinimumSize = new Size(500, 350);
        StartPosition = FormStartPosition.CenterScreen;
        KeyPreview = true;

        var toolStrip = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden };
        var clearButton = new ToolStripButton("Clear") { ToolTipText = "Discard all collected samples" };
        var settingsButton = new ToolStripButton("Settings...") { ToolTipText = "Change URLs, interval, chart and logging options" };
        var exportButton = new ToolStripButton("Export CSV...") { ToolTipText = "Save collected samples to a CSV file" };
        toolStrip.Items.AddRange([_startButton, _stopButton, new ToolStripSeparator(), clearButton,
            new ToolStripSeparator(), settingsButton, exportButton]);

        var statusStrip = new StatusStrip();
        statusStrip.Items.Add(_stateLabel);
        statusStrip.Items.AddRange(_statLabels);

        // Fill control first so the docked strips claim their edges before it.
        Controls.Add(_chart);
        Controls.Add(toolStrip);
        Controls.Add(statusStrip);

        _startButton.Click += (_, _) => StartPolling();
        _stopButton.Click += (_, _) => StopPolling();
        clearButton.Click += (_, _) => { foreach (var s in _series) s.Samples.Clear(); RefreshView(); };
        settingsButton.Click += (_, _) => ShowSettings();
        exportButton.Click += (_, _) => ExportCsv();
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F5 && e.Shift) StopPolling();
            else if (e.KeyCode == Keys.F5) StartPolling();
        };

        ApplySettings(_settings, isInitial: true);
        Shown += (_, _) => { if (_settings.AutoStart) StartPolling(); };
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        StopPolling();
        _client?.Dispose();
        base.OnFormClosing(e);
    }

    // ---------------- Settings ----------------

    private void ShowSettings()
    {
        using var dlg = new SettingsForm(_settings, _settingsPath);
        if (dlg.ShowDialog(this) != DialogResult.OK || dlg.Result is null) return;

        try { dlg.Result.Save(_settingsPath); }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Could not save settings:\n" + ex.Message, "Settings", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        bool wasRunning = _cts is not null;
        StopPolling();
        ApplySettings(dlg.Result, isInitial: false);
        if (wasRunning) StartPolling();
    }

    private void ApplySettings(AppSettings s, bool isInitial)
    {
        _settings = s;
        UpdateSeries(_series[0], s.Url1Name, s.Url1, s.Url1Color, s.Url1Enabled, isInitial);
        UpdateSeries(_series[1], s.Url2Name, s.Url2, s.Url2Color, s.Url2Enabled, isInitial);
        foreach (var series in _series) Trim(series);

        _client?.Dispose();
        _client = CreateClient(s);

        _chart.Settings = s;
        _chart.Series = _series;
        RefreshView();
    }

    private static void UpdateSeries(SeriesData series, string name, string url, Color color, bool enabled, bool isInitial)
    {
        // Samples belong to a specific endpoint; start fresh when the URL changes.
        if (!isInitial && !string.Equals(series.Url, url, StringComparison.OrdinalIgnoreCase))
            series.Samples.Clear();
        series.Name = string.IsNullOrWhiteSpace(name) ? url : name;
        series.Url = url;
        series.Color = color;
        series.Enabled = enabled;
    }

    private static HttpClient CreateClient(AppSettings s)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = s.FollowRedirects,
            UseProxy = s.UseSystemProxy,
            AutomaticDecompression = DecompressionMethods.All,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        };
        if (s.IgnoreCertificateErrors)
            handler.SslOptions.RemoteCertificateValidationCallback = (_, _, _, _) => true;

        return new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan }; // per-request timeout is applied via CTS
    }

    // ---------------- Polling ----------------

    private void StartPolling()
    {
        if (_cts is not null) return;
        if (!_series.Any(x => x.Enabled))
        {
            MessageBox.Show(this, "Both URLs are disabled. Enable at least one in Settings.", "Nothing to poll",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        _cts = new CancellationTokenSource();
        _ = RunLoopAsync(_settings, _client!, ++_generation, _cts.Token);
        RefreshView();
    }

    private void StopPolling()
    {
        if (_cts is null) return;
        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
        _generation++;
        RefreshView();
    }

    /// <summary>Runs on the UI thread; the HTTP work itself runs off-thread inside MeasureAsync.</summary>
    private async Task RunLoopAsync(AppSettings s, HttpClient client, int generation, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(s.IntervalSeconds));
        try
        {
            do
            {
                var polls = new List<Task>();
                for (int i = 0; i < _series.Length; i++)
                    if (_series[i].Enabled) polls.Add(PollAsync(i, _series[i].Url, s, client, generation, ct));

                // Wait for this round so slow/timing-out servers can't pile up overlapping requests.
                await Task.WhenAll(polls);
            }
            while (await timer.WaitForNextTickAsync(ct));
        }
        catch (OperationCanceledException) { }
    }

    private async Task PollAsync(int index, string url, AppSettings s, HttpClient client, int generation, CancellationToken ct)
    {
        Sample sample;
        try { sample = await MeasureAsync(url, s, client, ct); }
        catch (OperationCanceledException) { return; }

        if (generation != _generation) return; // stopped or settings changed while in flight
        AddSample(index, sample);
    }

    private static async Task<Sample> MeasureAsync(string url, AppSettings s, HttpClient client, CancellationToken ct)
    {
        var sample = new Sample { Time = DateTime.Now };
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(s.TimeoutSeconds));

        var sw = new Stopwatch();
        try
        {
            using var request = new HttpRequestMessage(s.Method == RequestMethod.HEAD ? HttpMethod.Head : HttpMethod.Get, url);
            if (!s.ReuseConnections) request.Headers.ConnectionClose = true;
            if (!string.IsNullOrWhiteSpace(s.UserAgent)) request.Headers.TryAddWithoutValidation("User-Agent", s.UserAgent);
            foreach (var (name, value) in s.ParseExtraHeaders())
                request.Headers.TryAddWithoutValidation(name, value);

            var completion = s.MeasureUntil == MeasureUntil.FullBody
                ? HttpCompletionOption.ResponseContentRead
                : HttpCompletionOption.ResponseHeadersRead;

            sw.Start();
            using var response = await client.SendAsync(request, completion, timeout.Token).ConfigureAwait(false);
            sw.Stop();

            sample.ElapsedMs = sw.Elapsed.TotalMilliseconds;
            sample.StatusCode = (int)response.StatusCode;
            if (s.TreatNon2xxAsFailure && !response.IsSuccessStatusCode)
                sample.Error = $"HTTP {(int)response.StatusCode} {response.ReasonPhrase}";
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            sample.Error = $"Timed out after {s.TimeoutSeconds:0.##} s";
        }
        catch (HttpRequestException ex)
        {
            sample.Error = ex.InnerException?.Message ?? ex.Message;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            sample.Error = ex.Message;
        }
        return sample;
    }

    // ---------------- Data ----------------

    private void AddSample(int index, Sample sample)
    {
        var series = _series[index];
        series.Samples.Add(sample);
        Trim(series);
        if (_settings.LogToCsv) AppendCsvLog(series, sample);
        RefreshView();
    }

    private void Trim(SeriesData series)
    {
        int excess = series.Samples.Count - _settings.MaxSamples;
        if (excess > 0) series.Samples.RemoveRange(0, excess);
    }

    private void RefreshView()
    {
        bool running = _cts is not null;
        _startButton.Enabled = !running;
        _stopButton.Enabled = running;
        _stateLabel.Text = running
            ? $"Polling every {_settings.IntervalSeconds:0.###} s"
            : "Stopped";
        _stateLabel.ForeColor = running ? Color.DarkGreen : SystemColors.ControlText;

        for (int i = 0; i < _series.Length; i++)
        {
            _statLabels[i].Visible = _series[i].Enabled;
            _statLabels[i].Text = FormatStats(_series[i]);
            _statLabels[i].ForeColor = _series[i].Color;
        }
        _chart.Invalidate();
    }

    private static string FormatStats(SeriesData s)
    {
        if (s.Samples.Count == 0) return $"{s.Name}: no data";

        var last = s.Samples[^1];
        string lastText = last.IsSuccess ? $"{last.ElapsedMs:0.0} ms" : "FAILED";
        int failures = 0;
        double sum = 0, min = double.MaxValue, max = 0;
        foreach (var p in s.Samples)
        {
            if (!p.IsSuccess) { failures++; continue; }
            double v = p.ElapsedMs!.Value;
            sum += v;
            if (v < min) min = v;
            if (v > max) max = v;
        }
        int ok = s.Samples.Count - failures;
        string agg = ok > 0 ? $" | avg {sum / ok:0.0} | min {min:0.0} | max {max:0.0}" : "";
        return $"{s.Name}: last {lastText}{agg} | failures {failures}/{s.Samples.Count}";
    }

    // ---------------- CSV ----------------

    private const string CsvHeader = "Timestamp,Name,Url,ResponseTimeMs,StatusCode,Success,Error";

    private static string CsvLine(SeriesData series, Sample p) => string.Join(",",
        p.Time.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture),
        Csv(series.Name),
        Csv(series.Url),
        p.ElapsedMs?.ToString("0.###", CultureInfo.InvariantCulture) ?? "",
        p.StatusCode?.ToString(CultureInfo.InvariantCulture) ?? "",
        p.IsSuccess ? "true" : "false",
        Csv(p.Error ?? ""));

    private static string Csv(string value) =>
        value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? "\"" + value.Replace("\"", "\"\"") + "\"" : value;

    private void AppendCsvLog(SeriesData series, Sample p)
    {
        try
        {
            var path = _settings.CsvLogPath;
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            bool newFile = !File.Exists(path) || new FileInfo(path).Length == 0;
            File.AppendAllText(path, (newFile ? CsvHeader + Environment.NewLine : "") + CsvLine(series, p) + Environment.NewLine);
        }
        catch (Exception ex)
        {
            _stateLabel.Text = "CSV log error: " + ex.Message;
        }
    }

    private void ExportCsv()
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"response-times-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
        };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        var sb = new StringBuilder(CsvHeader).AppendLine();
        foreach (var (series, p) in _series.SelectMany(s => s.Samples.Select(p => (s, p))).OrderBy(x => x.p.Time))
            sb.AppendLine(CsvLine(series, p));
        try { File.WriteAllText(dlg.FileName, sb.ToString()); }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
