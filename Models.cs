namespace HttpResponsePlotter;

/// <summary>One measurement of one URL.</summary>
internal sealed class Sample
{
    public DateTime Time { get; init; }
    public double? ElapsedMs { get; set; }
    public int? StatusCode { get; set; }
    public string? Error { get; set; }

    public bool IsSuccess => Error is null && ElapsedMs.HasValue;
}

/// <summary>The collected samples for one configured URL.</summary>
internal sealed class SeriesData
{
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public Color Color { get; set; }
    public bool Enabled { get; set; }
    public List<Sample> Samples { get; } = new();
}
