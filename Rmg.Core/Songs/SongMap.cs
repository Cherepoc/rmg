using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;

namespace Rmg.Core.Songs;

/// <summary>
///     Where the parts of a song are, in beats from its start: its intro, its sections one after another, each in its
///     meter, and its ending, and the meter the song starts in.
/// </summary>
/// <param name="Meter">The meter the song starts in, its first section's, which its intro plays in.</param>
public sealed record SongMap(Meter Meter, IntroSpan Intro, ImmutableArray<SectionSpan> Sections, EndingSpan Ending)
{
    /// <summary>Where the first section starts, after the intro's bars.</summary>
    public double Origin => Sections.IsEmpty ? Intro.Duration : Sections[0].Start;

    public double Duration => Ending.Start + Ending.Duration;

    /// <summary>The section a position is in, a hair early counting as at its start (<see cref="Span.Within" />); none before the first or after the last.</summary>
    public SectionSpan? SectionAt(double position)
    {
        var index = IndexAt(position + 1e-9);
        return index >= 0 && Sections[index].IsWithin(position) ? Sections[index] : null;
    }

    /// <summary>The last section starting at or before a position, by its index; -1 before the first.</summary>
    private int IndexAt(double position)
    {
        // the sections one after another, by halving, as every note asks
        var (low, high) = (0, Sections.Length - 1);
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (Sections[middle].Start <= position)
                low = middle + 1;
            else
                high = middle - 1;
        }

        return high;
    }

    /// <summary>
    ///     A place's key, by the section it is in or after, the intro's for none, and how far into it, in places a beat:
    ///     a stream keyed by it stays as it was where something before the section moves, such as an intro's length or a
    ///     short bar.
    /// </summary>
    public long PlaceKey(double position, int placesPerBeat)
    {
        var index = IndexAt(position + 1e-9);
        return Seeds.Key(index, (long)Math.Round((position - (index >= 0 ? Sections[index].Start : 0)) * placesPerBeat));
    }

    /// <summary>
    ///     The section whose bars a position is counted in: the one it is in, the first before it and the last after
    ///     it; none for a song of no sections.
    /// </summary>
    private SectionSpan? BarsAt(double position)
    {
        if (Sections.IsEmpty)
            return null;

        return SectionAt(position) ?? (position < Origin ? Sections[0] : Sections[^1]);
    }

    /// <summary>
    ///     The meter a position's bar is in: its section's, the first's before it and the last's after it, and a short last
    ///     bar's own (<see cref="ShortBars" />), so that the bar before a section's start is the bar there is.
    /// </summary>
    public Meter MeterAt(double position)
    {
        return BarsAt(position) is not { } span ? Meter
            : span.Cut > 0 && position >= span.LastBar - 1e-9 && position < span.End - 1e-9 ? ShortBars.Of(span.Meter)
            : span.Meter;
    }

    /// <summary>How long the bar ending at a position is: a section's short last bar before the next section's start, its meter's bar otherwise.</summary>
    public double BarBefore(double position) => MeterAt(position - 1e-6).BarDuration;

    /// <summary>
    ///     Where a position is in the song's bars: its bar counted from its section's start, before the first and after the
    ///     last too, in the section's meter, and how far into the bar it is, a hair before a bar line counted at it
    ///     against a sum's rounding; the bar's own meter a short last bar's (<see cref="MeterAt" />).
    /// </summary>
    public Place PlaceOf(double position)
    {
        var span = BarsAt(position);
        var meter = span?.Meter ?? Meter;
        var fromStart = position - (span?.Start ?? Origin);
        var bar = (int)Math.Floor(fromStart / meter.BarDuration + 1e-9);
        return new Place(bar, bar.Mod(meter.PatternBarCount), Math.Max(0, fromStart - bar * meter.BarDuration), MeterAt(position));
    }

    /// <summary>The bar of a section's pattern a position is in, counted from its section's start, before the first too, a short last bar the pattern's own.</summary>
    public int PatternBarAt(double position) => PlaceOf(position).PatternBar;

    /// <summary>How far into its bar a position is, in beats, the bars counted from its section's start.</summary>
    public double BeatInBar(double position) => PlaceOf(position).Beat;
}

/// <summary>Where a position is in the song's bars (<see cref="SongMap.PlaceOf" />).</summary>
/// <param name="Bar">Its bar, counted from its section's start, negative before the first section.</param>
/// <param name="PatternBar">Its bar of the section's pattern.</param>
/// <param name="Beat">How far into its bar it is, in beats.</param>
/// <param name="Meter">Its bar's own meter, a short last bar's.</param>
public readonly record struct Place(int Bar, int PatternBar, double Beat, Meter Meter);

/// <summary>A stretch of a section, a bar, a phrase or a play of its pattern, by its index from the section's start, the last cut at the section's end.</summary>
public readonly record struct Span(int Index, double Start, double End)
{
    /// <summary>Whether a position is in the stretch, a hair early counting as at its start, as every stage asks.</summary>
    public bool Contains(double position) => Within(position, Start, End);

    /// <summary>Whether a position is within [start, end), a hair early counting as at a bound, the one tolerance the stages share.</summary>
    public static bool Within(double position, double start, double end) => position >= start - 1e-9 && position < end - 1e-9;
}

/// <summary>How a song starts: its intro's kind, and how long it plays before the first section, 0 for none.</summary>
/// <param name="Window">Where the band comes in part by part, for an intro of entries.</param>
/// <param name="Entries">When every part comes in, for an intro of entries, in the order drawn; none otherwise.</param>
public sealed record IntroSpan(IntroKind Kind, double Duration, IntroWindow Window = default, ImmutableArray<IntroEntry> Entries = default);

/// <summary>A part of the band coming in, in an intro of entries.</summary>
/// <param name="Tracks">The part's tracks.</param>
/// <param name="Entry">When it comes in, in beats from the start of the intro's window.</param>
public sealed record IntroEntry(IntroPart Part, ImmutableArray<int> Tracks, double Entry);

/// <summary>A section where the song plays it, the meter its bars are in, and how much its last bar is cut short, in beats (<see cref="ShortBars" />).</summary>
public sealed record SectionSpan(int SectionId, double Start, double Duration, Meter Meter, double Cut)
{
    public double End => Start + Duration;

    /// <summary>Where its last bar starts, a short one's.</summary>
    public double LastBar => End - Meter.BarDuration + Cut;

    /// <summary>Whether a position is in it, a hair early counting as at its start.</summary>
    public bool IsWithin(double position) => Span.Within(position, Start, End);

    /// <summary>How many bars it plays, a short last one among them.</summary>
    public int BarCount => (int)Math.Ceiling(Duration / Meter.BarDuration - 1e-9);

    /// <summary>The bar a position is in, counted from its start, a hair early counting as the bar it is at.</summary>
    public int BarOf(double position) => Count(position, Meter.BarDuration);

    /// <summary>The phrase a position is in, counted from its start, a hair early counting as the phrase it is at.</summary>
    public int PhraseOf(double position) => Count(position, Meter.PhraseDuration);

    /// <summary>The play of its pattern a position is in, counted from its start, a hair early counting as the play it is at.</summary>
    public int PatternOf(double position) => Count(position, Meter.PatternDuration);

    private int Count(double position, double length) => (int)Math.Floor((position - Start) / length + 1e-9);

    /// <summary>Where a bar of it starts and ends, a short last one ending at the section's end.</summary>
    public (double Start, double End) Bar(int index) => (Start + index * Meter.BarDuration, Math.Min(Start + (index + 1) * Meter.BarDuration, End));

    /// <summary>Its bars, a short last one among them.</summary>
    public IEnumerable<Span> Bars => Walk(Meter.BarDuration);

    /// <summary>Its phrases, the last cut at its end.</summary>
    public IEnumerable<Span> Phrases => Walk(Meter.PhraseDuration);

    /// <summary>The plays of its pattern, the last cut at its end.</summary>
    public IEnumerable<Span> Patterns => Walk(Meter.PatternDuration);

    private IEnumerable<Span> Walk(double length)
    {
        for (var index = 0; Start + index * length < End - 1e-9; index++)
            yield return new Span(index, Start + index * length, Math.Min(Start + (index + 1) * length, End));
    }
}

/// <summary>How a song ends: the ending's kind, where it starts, how long it is, and its final chord's length.</summary>
/// <param name="Start">Where the last section ends.</param>
/// <param name="Duration">How long the ending plays after the last section, 0 for an open ending.</param>
/// <param name="Held">How long the final chord is held, in beats; 0 for an open ending.</param>
/// <param name="Stop">How long the band is silent before the final chord, in beats; 0 but for a stop.</param>
/// <param name="SlowsDown">Whether the bar before the ending slows down.</param>
public sealed record EndingSpan(EndingKind Kind, double Start, double Duration, double Held, double Stop = 0, bool SlowsDown = false);
