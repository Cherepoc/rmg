using System.Collections.Immutable;
using Rmg.Core.Composition;

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

    /// <summary>The section a position is in; none before the first or after the last.</summary>
    public SectionSpan? SectionAt(double position)
    {
        // the sections one after another: the last starting at or before it, by halving, as every note asks
        var (low, high) = (0, Sections.Length - 1);
        while (low <= high)
        {
            var middle = (low + high) / 2;
            if (Sections[middle].Start <= position)
                low = middle + 1;
            else
                high = middle - 1;
        }

        return high >= 0 && position < Sections[high].End ? Sections[high] : null;
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

    /// <summary>The bar of a section's pattern a position is in, counted from its section's start, before the first too, a short last bar the pattern's own.</summary>
    public int PatternBarAt(double position)
    {
        var meter = BarsAt(position)?.Meter ?? Meter;
        return ((int)Math.Floor((position - (BarsAt(position)?.Start ?? Origin)) / meter.BarDuration + 1e-9)).Mod(meter.PatternBarCount);
    }

    /// <summary>How far into its bar a position is, in beats, the bars counted from its section's start.</summary>
    public double BeatInBar(double position)
    {
        var meter = BarsAt(position)?.Meter ?? Meter;
        var fromStart = position - (BarsAt(position)?.Start ?? Origin);
        // a hair before a bar line counted at it, against a sum's rounding
        return Math.Max(0, fromStart - Math.Floor(fromStart / meter.BarDuration + 1e-9) * meter.BarDuration);
    }
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

    /// <summary>How many bars it plays, a short last one among them.</summary>
    public int BarCount => (int)Math.Ceiling(Duration / Meter.BarDuration - 1e-9);

    /// <summary>The bar a position is in, counted from its start, a hair early counting as the bar it is at.</summary>
    public int BarOf(double position) => (int)Math.Floor((position - Start) / Meter.BarDuration + 1e-9);

    /// <summary>Where a bar of it starts and ends, a short last one ending at the section's end.</summary>
    public (double Start, double End) Bar(int index) => (Start + index * Meter.BarDuration, Math.Min(Start + (index + 1) * Meter.BarDuration, End));
}

/// <summary>How a song ends: the ending's kind, where it starts, how long it is, and its final chord's length.</summary>
/// <param name="Start">Where the last section ends.</param>
/// <param name="Duration">How long the ending plays after the last section, 0 for an open ending.</param>
/// <param name="Held">How long the final chord is held, in beats; 0 for an open ending.</param>
/// <param name="Stop">How long the band is silent before the final chord, in beats; 0 but for a stop.</param>
/// <param name="SlowsDown">Whether the bar before the ending slows down.</param>
public sealed record EndingSpan(EndingKind Kind, double Start, double Duration, double Held, double Stop = 0, bool SlowsDown = false);
