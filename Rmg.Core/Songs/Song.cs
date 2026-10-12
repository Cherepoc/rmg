using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Events;

namespace Rmg.Core.Songs;

public sealed class Song
{
    public Song(
        double duration,
        Meter meter,
        ImmutableSortedDictionary<int, IInstrumentTrack> trackDefinitions,
        TrackEventStateTimelineMap<StateMap> trackEventStateTimelineMap,
        SongMap? map,
        ImmutableSortedDictionary<int, EventTimeline<RealizedNote>>? notes,
        SongDraws? draws
    )
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(duration);

        Duration = duration;
        Meter = meter;
        TrackDefinitions = trackDefinitions;
        TrackEventStateTimelineMap = trackEventStateTimelineMap;
        Map = map;
        Notes = notes;
        Draws = draws;
    }

    /// <summary>What a generated song drew, or was given in place of drawing; none for a song made otherwise.</summary>
    public SongDraws? Draws { get; }

    /// <summary>
    ///     Every playing track's notes, by its number, as the song's generation decided them from its state; none for a
    ///     song made otherwise, which <c>Render</c> decides them for.
    /// </summary>
    public ImmutableSortedDictionary<int, EventTimeline<RealizedNote>>? Notes { get; }

    /// <summary>Where the song's intro, sections and ending are, for a generated song.</summary>
    public SongMap? Map { get; }

    public double Duration { get; }

    /// <summary>The meter the song's bars are in.</summary>
    public Meter Meter { get; }

    /// <summary>The meter the bar at a position is in, as the map has it; the song's where it has none.</summary>
    public Meter MeterAt(double position) => Map?.MeterAt(position) ?? Meter;

    /// <summary>Where a position is in the song's bars, as the map has it; in the song's meter from its start where it has none.</summary>
    public Place PlaceOf(double position)
    {
        if (Map is { } map)
            return map.PlaceOf(position);
        var bar = (int)Math.Floor(position / Meter.BarDuration + 1e-9);
        return new Place(bar, bar.Mod(Meter.PatternBarCount), Math.Max(0, position - bar * Meter.BarDuration), Meter);
    }

    public ImmutableSortedDictionary<int, IInstrumentTrack> TrackDefinitions { get; }

    public TrackEventStateTimelineMap<StateMap> TrackEventStateTimelineMap { get; }
}
