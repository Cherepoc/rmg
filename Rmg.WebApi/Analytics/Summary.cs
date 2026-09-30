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

/// <param name="Version">The songs' version, which a seed needs to name a song.</param>
/// <param name="FirstDay">The first day an event of it arrived, as yyyy-MM-dd.</param>
/// <param name="Songs">Songs generated.</param>
/// <param name="Plays">Presses of play.</param>
/// <param name="Listens">Songs listened to, a visitor's song once however often it was paused.</param>
/// <param name="ListensPast30s">Of those, the ones listened to for 30 seconds or more in all.</param>
/// <param name="MedianSeconds">How long a song was listened to in all, in the middle of them.</param>
/// <param name="Likes">Likes of its songs, as they stand now, from every rating kept.</param>
/// <param name="Dislikes">Dislikes of its songs, likewise.</param>
public sealed record VersionListening(
    string Version,
    string FirstDay,
    int Songs,
    int Plays,
    int Listens,
    int ListensPast30s,
    double? MedianSeconds,
    int Likes,
    int Dislikes
);

/// <param name="Seed">The song, in <see cref="AnalyticsSummary.RatedVersion" />.</param>
/// <param name="Given">The unconventionality it was asked for with, from 0 to 127, which names it too; none for one drawn.</param>
/// <param name="Likes">Its likes as they stand now: the changes to a like less the changes from one.</param>
/// <param name="Dislikes">Its dislikes, likewise.</param>
public sealed record RatedSeed(long Seed, int? Given, int Likes, int Dislikes);

/// <param name="IsGiven">Whether the songs were asked for at their unconventionality, or drew it.</param>
/// <param name="Fifth">Which fifth of the unconventionality, from 0 for the plainest to 4 for the wildest.</param>
/// <param name="Songs">How many songs of it were rated.</param>
/// <param name="Likes">Their likes as they stand now.</param>
/// <param name="Dislikes">Their dislikes.</param>
public sealed record RatedFifth(bool IsGiven, int Fifth, int Songs, int Likes, int Dislikes);

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
/// <param name="Versions">How each songs' version was listened to, the latest first.</param>
/// <param name="RatedVersion">The latest songs' version any song was rated in, which <paramref name="Rated" /> is of.</param>
/// <param name="Rated">The songs of it rated, the most liked first and the most disliked last.</param>
/// <param name="RatedFifths">Its songs' ratings by how plain or wild they are, given apart from drawn.</param>
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
    IReadOnlyList<VersionListening> Versions,
    string? RatedVersion,
    IReadOnlyList<RatedSeed> Rated,
    IReadOnlyList<RatedFifth> RatedFifths,
    IReadOnlyList<Failure> Failures,
    int Events
);
