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

/// <param name="Seed">The song, in letters and digits.</param>
/// <param name="Seconds">How long it was listened to, over every visitor who played it.</param>
public sealed record SeedListening(string Seed, string? Version, string? Genre, double Seconds, int Plays);

/// <param name="Version">The songs' version, which a seed needs to name a song.</param>
/// <param name="FirstDay">The first day an event of it arrived within the days summed, as yyyy-MM-dd.</param>
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
/// <param name="Identity">What of its settings names it with its seed (<see cref="Songs.SongSettings.Identity" />); empty for none.</param>
/// <param name="Genre">The genre it was made in, which names it too: a preset's id or a genre's key; empty for none.</param>
/// <param name="Likes">Its likes as they stand now: the changes to a like less the changes from one.</param>
/// <param name="Dislikes">Its dislikes, likewise.</param>
public sealed record RatedSeed(string Seed, string Identity, string Genre, int Likes, int Dislikes);

/// <param name="IsGiven">Whether the songs were asked for at their unconventionality, or drew it.</param>
/// <param name="Fifth">Which fifth of the unconventionality, from 0 for the plainest to 4 for the wildest.</param>
/// <param name="Songs">How many songs of it were rated.</param>
/// <param name="Likes">Their likes as they stand now.</param>
/// <param name="Dislikes">Their dislikes.</param>
public sealed record RatedFifth(bool IsGiven, int Fifth, int Songs, int Likes, int Dislikes);

/// <param name="Genre">A preset's id, such as "rock"; "own" for every genre made on the page; empty for songs in none.</param>
/// <param name="Songs">How many songs in it were rated.</param>
/// <param name="Likes">Their likes as they stand now.</param>
/// <param name="Dislikes">Their dislikes.</param>
public sealed record RatedGenre(string Genre, int Songs, int Likes, int Dislikes);

/// <param name="Question">The listening mode's question (<c>listening.json</c>).</param>
/// <param name="With">How often the song with the device was the better.</param>
/// <param name="Without">How often the song without it was.</param>
/// <param name="Same">How often the two were the same.</param>
/// <param name="Unsure">How often the listener could not tell.</param>
public sealed record QuestionVerdicts(string Question, int With, int Without, int Same, int Unsure);

/// <param name="Version">The latest songs' version a verdict was given in, which the verdicts are of; none for none.</param>
/// <param name="Verdicts">The verdicts, by question.</param>
public sealed record ListeningVerdicts(string? Version, IReadOnlyList<QuestionVerdicts> Verdicts);

/// <param name="Name">What failed.</param>
/// <param name="Detail">Why, as far as the page could say.</param>
public sealed record Failure(string Name, string Detail, int Count);

/// <param name="Outcome">How songs asked of the server ended (<see cref="SongOutcomes.Name" />).</param>
public sealed record OutcomeCount(string Outcome, int Count);

/// <param name="Songs">How many songs of it were made.</param>
/// <param name="MedianMs">How long one took to generate, in the middle of them.</param>
/// <param name="WorstMs">The nineteen in twenty mark of it.</param>
public sealed record VersionSpeed(string Version, int Songs, double? MedianMs, double? WorstMs);

/// <param name="Fifth">Which fifth of the unconventionality, from 0 for the plainest to 4 for the wildest.</param>
/// <param name="Songs">How many songs of it were made.</param>
/// <param name="MedianMs">How long one took to generate, in the middle of them.</param>
/// <param name="WorstMs">The nineteen in twenty mark of it.</param>
public sealed record FifthSpeed(int Fifth, int Songs, double? MedianMs, double? WorstMs);

/// <param name="Day">The day, as yyyy-MM-dd.</param>
/// <param name="Made">Songs made.</param>
/// <param name="Cancelled">Songs given up by who asked, while waiting or being made.</param>
/// <param name="Refused">Songs turned away, the client having as many waiting as it may or the queue being full.</param>
/// <param name="Failed">Songs that could not be made.</param>
/// <param name="MostWaiting">The most songs waiting their turns that one arrived to.</param>
public sealed record ServerDay(string Day, int Made, int Cancelled, int Refused, int Failed, int MostWaiting);

/// <summary>What the server measured of the songs asked of it, which says nothing of who asked.</summary>
/// <param name="MakeMs">How long a song made took to generate.</param>
/// <param name="RenderMs">How long a song made took to render and write.</param>
/// <param name="WaitMs">How long a song made waited for its turn.</param>
/// <param name="Outcomes">How the songs asked for ended, the commonest first.</param>
/// <param name="Versions">How long each songs' version took to generate a song, the latest first.</param>
/// <param name="FifthsVersion">The latest songs' version any song was made in, which <paramref name="Fifths" /> is of.</param>
/// <param name="Fifths">How long its songs took to generate, by how plain or wild they are.</param>
/// <param name="Daily">Day by day.</param>
public sealed record ServerSummary(
    Spread MakeMs,
    Spread RenderMs,
    Spread WaitMs,
    IReadOnlyList<OutcomeCount> Outcomes,
    IReadOnlyList<VersionSpeed> Versions,
    string? FifthsVersion,
    IReadOnlyList<FifthSpeed> Fifths,
    IReadOnlyList<ServerDay> Daily
);

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
/// <param name="RatedGenres">Its songs' ratings by their genre.</param>
/// <param name="VerdictVersion">The latest songs' version a verdict was given in, which <paramref name="Verdicts" /> are of.</param>
/// <param name="Verdicts">The listening mode's verdicts, by question.</param>
/// <param name="Failures">What went wrong, and how often.</param>
/// <param name="Server">What the server measured of the songs asked of it.</param>
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
    IReadOnlyList<RatedGenre> RatedGenres,
    string? VerdictVersion,
    IReadOnlyList<QuestionVerdicts> Verdicts,
    IReadOnlyList<Failure> Failures,
    int Events,
    ServerSummary Server
);
