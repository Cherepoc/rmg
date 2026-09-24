namespace Rmg.WebApi.Analytics;

/// <param name="Median">The middle of them, which is what a typical visitor saw.</param>
/// <param name="Upper">The three quarter mark, which is what a slow connection saw.</param>
/// <param name="Worst">The nineteen in twenty mark, which is as bad as it reasonably gets.</param>
/// <param name="Count">How many measurements these came from, so a small sample shows itself.</param>
public sealed record Spread(double? Median, double? Upper, double? Worst, int Count);

/// <param name="Name">What happened, and how far down the page it is.</param>
/// <param name="Visitors">How many visitors got that far.</param>
public sealed record FunnelStep(string Name, int Visitors);

/// <param name="Day">The day, as yyyy-MM-dd.</param>
public sealed record DayCount(string Day, int Visitors, int Songs, int Plays);

/// <param name="Seed">The song.</param>
/// <param name="Seconds">How long it was listened to, over every visitor who played it.</param>
public sealed record SeedListening(long Seed, double Seconds, int Plays);

/// <param name="Name">What failed.</param>
/// <param name="Detail">Why, as far as the page could say.</param>
public sealed record Failure(string Name, string Detail, int Count);

/// <param name="Days">How many days this covers.</param>
/// <param name="Visitors">Distinct visitors over the whole window, counted once per day each.</param>
/// <param name="SoundFontMs">How long the soundfont took to arrive.</param>
/// <param name="FirstGestureMs">How long the page waited to be touched before it could make a sound.</param>
/// <param name="Funnel">How far down the page visitors got.</param>
/// <param name="Daily">The same, day by day.</param>
/// <param name="Seeds">Which songs held attention.</param>
/// <param name="Failures">What went wrong, and how often.</param>
public sealed record AnalyticsSummary(
    int Days,
    int Visitors,
    int PageOpens,
    Spread SoundFontMs,
    Spread FirstGestureMs,
    Spread ListenedSeconds,
    IReadOnlyList<FunnelStep> Funnel,
    IReadOnlyList<DayCount> Daily,
    IReadOnlyList<SeedListening> Seeds,
    IReadOnlyList<Failure> Failures,
    int Events
);
