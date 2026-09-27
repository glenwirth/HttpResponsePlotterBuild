using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing.Design;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HttpResponsePlotter;

public enum RequestMethod { GET, HEAD }

public enum MeasureUntil
{
    /// <summary>Stop the clock when response headers arrive.</summary>
    ResponseHeaders,
    /// <summary>Stop the clock after the whole body has been downloaded.</summary>
    FullBody,
}

/// <summary>
/// All user-configurable settings. Edited through a PropertyGrid and persisted as JSON.
/// Category names are numbered so the PropertyGrid shows them in a sensible order.
/// </summary>
public sealed class AppSettings
{
    private const string CatUrl1 = "1. URL 1";
    private const string CatUrl2 = "2. URL 2";
    private const string CatPolling = "3. Polling";
    private const string CatRequest = "4. Request";
    private const string CatChart = "5. Chart";
    private const string CatLogging = "6. Logging";

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "HttpResponsePlotter", "settings.json");

    // ---------- URL 1 ----------
    [Category(CatUrl1), DisplayName("URL"), Description("Address to measure.")]
    public string Url1 { get; set; } = "http://localhost:45245/v1/info";

    [Category(CatUrl1), DisplayName("Name"), Description("Label shown in the legend and status bar.")]
    public string Url1Name { get; set; } = "Server 1 (45245)";

    [Category(CatUrl1), DisplayName("Enabled"), Description("Poll and plot this URL.")]
    public bool Url1Enabled { get; set; } = true;

    [Category(CatUrl1), DisplayName("Line color"), JsonIgnore]
    public Color Url1Color { get => ParseColor(Url1ColorHex); set => Url1ColorHex = ColorTranslator.ToHtml(value); }

    [Browsable(false)]
    public string Url1ColorHex { get; set; } = "#1F77B4";

    // ---------- URL 2 ----------
    [Category(CatUrl2), DisplayName("URL"), Description("Address to measure.")]
    public string Url2 { get; set; } = "http://localhost:45246/v1/info";

    [Category(CatUrl2), DisplayName("Name"), Description("Label shown in the legend and status bar.")]
    public string Url2Name { get; set; } = "Server 2 (45246)";

    [Category(CatUrl2), DisplayName("Enabled"), Description("Poll and plot this URL.")]
    public bool Url2Enabled { get; set; } = true;

    [Category(CatUrl2), DisplayName("Line color"), JsonIgnore]
    public Color Url2Color { get => ParseColor(Url2ColorHex); set => Url2ColorHex = ColorTranslator.ToHtml(value); }

    [Browsable(false)]
    public string Url2ColorHex { get; set; } = "#FF7F0E";

    // ---------- Polling ----------
    [Category(CatPolling), DisplayName("Interval (seconds)"), Description("Time between polls. Fractions are allowed, e.g. 0.5.")]
    public double IntervalSeconds { get; set; } = 5;

    [Category(CatPolling), DisplayName("Timeout (seconds)"), Description("A request that takes longer than this is recorded as a failure.")]
    public double TimeoutSeconds { get; set; } = 10;

    [Category(CatPolling), DisplayName("Start polling on launch"), Description("Begin polling automatically when the program starts.")]
    public bool AutoStart { get; set; } = true;

    [Category(CatPolling), DisplayName("Treat non-2xx as failure"), Description("If true, responses such as 404 or 500 are plotted as failures.")]
    public bool TreatNon2xxAsFailure { get; set; } = true;

    // ---------- Request ----------
    [Category(CatRequest), DisplayName("HTTP method")]
    public RequestMethod Method { get; set; } = RequestMethod.GET;

    [Category(CatRequest), DisplayName("Measure until"), Description("ResponseHeaders = time to first response headers. FullBody = time until the whole body is downloaded.")]
    public MeasureUntil MeasureUntil { get; set; } = MeasureUntil.FullBody;

    [Category(CatRequest), DisplayName("Reuse connections (keep-alive)"), Description("If false, every request opens a new TCP connection (includes connect time in the measurement).")]
    public bool ReuseConnections { get; set; } = true;

    [Category(CatRequest), DisplayName("Follow redirects")]
    public bool FollowRedirects { get; set; } = true;

    [Category(CatRequest), DisplayName("Ignore certificate errors"), Description("Accept invalid/self-signed HTTPS certificates.")]
    public bool IgnoreCertificateErrors { get; set; } = false;

    [Category(CatRequest), DisplayName("Use system proxy")]
    public bool UseSystemProxy { get; set; } = true;

    [Category(CatRequest), DisplayName("User-Agent")]
    public string UserAgent { get; set; } = "HttpResponsePlotter/1.0";

    [Category(CatRequest), DisplayName("Extra headers"), Description("One header per line, in the form  Name: Value")]
    [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
    public string ExtraHeaders { get; set; } = "";

    // ---------- Chart ----------
    [Category(CatChart), DisplayName("Time window (minutes)"), Description("How much history is visible. 0 = show everything collected.")]
    public double TimeWindowMinutes { get; set; } = 10;

    [Category(CatChart), DisplayName("Max samples per URL"), Description("Older samples are discarded beyond this count.")]
    public int MaxSamples { get; set; } = 20000;

    [Category(CatChart), DisplayName("Y axis max (ms)"), Description("Fixed top of the Y axis. 0 = automatic.")]
    public double YAxisMaxMs { get; set; } = 0;

    [Category(CatChart), DisplayName("Warning threshold (ms)"), Description("Draws a dashed horizontal line at this value. 0 = off.")]
    public double WarningThresholdMs { get; set; } = 0;

    [Category(CatChart), DisplayName("Line width")]
    public float LineWidth { get; set; } = 2f;

    [Category(CatChart), DisplayName("Show point markers")]
    public bool ShowMarkers { get; set; } = true;

    [Category(CatChart), DisplayName("Background color"), JsonIgnore]
    public Color ChartBackColor { get => ParseColor(ChartBackColorHex); set => ChartBackColorHex = ColorTranslator.ToHtml(value); }

    [Browsable(false)]
    public string ChartBackColorHex { get; set; } = "#FFFFFF";

    [Category(CatChart), DisplayName("Text/axis color"), JsonIgnore]
    public Color ChartForeColor { get => ParseColor(ChartForeColorHex); set => ChartForeColorHex = ColorTranslator.ToHtml(value); }

    [Browsable(false)]
    public string ChartForeColorHex { get; set; } = "#333333";

    [Category(CatChart), DisplayName("Grid color"), JsonIgnore]
    public Color GridColor { get => ParseColor(GridColorHex); set => GridColorHex = ColorTranslator.ToHtml(value); }

    [Browsable(false)]
    public string GridColorHex { get; set; } = "#E6E6E6";

    [Category(CatChart), DisplayName("Threshold line color"), JsonIgnore]
    public Color ThresholdColor { get => ParseColor(ThresholdColorHex); set => ThresholdColorHex = ColorTranslator.ToHtml(value); }

    [Browsable(false)]
    public string ThresholdColorHex { get; set; } = "#D62728";

    // ---------- Logging ----------
    [Category(CatLogging), DisplayName("Log samples to CSV"), Description("Append every sample to a CSV file as it is measured.")]
    public bool LogToCsv { get; set; } = false;

    [Category(CatLogging), DisplayName("CSV log path")]
    public string CsvLogPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "HttpResponsePlotter.csv");

    // ---------- Helpers ----------
    private static Color ParseColor(string hex)
    {
        try { return ColorTranslator.FromHtml(hex); }
        catch { return Color.Gray; }
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public AppSettings Clone() =>
        JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(this, JsonOptions), JsonOptions)!;

    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), JsonOptions) ?? new AppSettings();
        }
        catch
        {
            // Corrupt file: keep a copy for the user and fall back to defaults.
            try { File.Copy(path, path + ".bad", overwrite: true); } catch { }
        }
        return new AppSettings();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    /// <summary>Returns a description of the first invalid setting, or null if everything is valid.</summary>
    public string? Validate()
    {
        if (Url1Enabled && !IsHttpUrl(Url1)) return "URL 1 must be an absolute http:// or https:// address.";
        if (Url2Enabled && !IsHttpUrl(Url2)) return "URL 2 must be an absolute http:// or https:// address.";
        if (IntervalSeconds < 0.1 || IntervalSeconds > 86400) return "Interval must be between 0.1 and 86400 seconds.";
        if (TimeoutSeconds < 0.1 || TimeoutSeconds > 3600) return "Timeout must be between 0.1 and 3600 seconds.";
        if (TimeWindowMinutes < 0) return "Time window cannot be negative.";
        if (MaxSamples < 10) return "Max samples must be at least 10.";
        if (YAxisMaxMs < 0) return "Y axis max cannot be negative.";
        if (WarningThresholdMs < 0) return "Warning threshold cannot be negative.";
        if (LineWidth < 0.5f || LineWidth > 10f) return "Line width must be between 0.5 and 10.";
        if (LogToCsv && string.IsNullOrWhiteSpace(CsvLogPath)) return "CSV log path is required when CSV logging is on.";
        return null;
    }

    private static bool IsHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

    public IEnumerable<(string Name, string Value)> ParseExtraHeaders()
    {
        foreach (var line in (ExtraHeaders ?? "").Split('\n'))
        {
            int colon = line.IndexOf(':');
            if (colon <= 0) continue;
            yield return (line[..colon].Trim(), line[(colon + 1)..].Trim());
        }
    }
}
