using Rmg.Core.Composition;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Listening;

/// <summary>
///     How much two renderings of a song differ as heard over a stretch of seconds, a listening question's song with its
///     device and without: the share of its notes' loudness that differs, notes added, taken away or played at another
///     pitch, loudness or instrument, weighted by their loudness so that a change in a quiet part counts little; and the share of the
///     moments its sounding tracks' bend, expression or controllers differ, which a device that plays no notes of its own
///     changes, a tape's wow or a filter's sweep.
/// </summary>
internal sealed class HeardDifference
{
    /// <summary>How often the effects are compared, in seconds.</summary>
    private const double Sample = 0.25;

    /// <summary>The least differences that count: a bend's in semitones, an expression's and a controller's.</summary>
    private const double BendDifference = 0.05, ExpressionDifference = 0.1;

    private const int ControllerDifference = 8;

    /// <summary>
    ///     How far a note may move and be heard as the same note, in seconds: a swing or a pocket nudging it less is felt at
    ///     most, not heard, and a question whose songs differ only so is no question to ask.
    /// </summary>
    public const double TimingDifference = 0.03;

    /// <summary>How much louder or quieter a note may play and still be the same note, a few MIDI steps, in the velocity's share.</summary>
    public const double LoudnessDifference = 3 / 127.0;

    private readonly Rendering _with;
    private readonly Rendering _without;

    public HeardDifference(RenderedSong with, RenderedSong without)
    {
        (_with, _without) = (new Rendering(with), new Rendering(without));
    }

    /// <summary>How much the two differ from one second to another: the notes' share and the effects', each from 0 to 1, added.</summary>
    public double Between(double from, double to) => Parts(from, to).Values.Sum();

    /// <summary>
    ///     How much each part makes of the difference from one second to another, by its name as a listener knows it, a
    ///     pitched part's role and instrument, a drum's own: its notes' share of all the notes' loudness that differs and
    ///     its share of the moments the effects differ, added, so that the parts' shares add up to <see cref="Between" />.
    /// </summary>
    public Dictionary<string, double> Parts(double from, double to)
    {
        var parts = new Dictionary<string, double>();
        var (with, without) = (_with.NotesBetween(from, to), _without.NotesBetween(from, to));
        var all = with.Sum(x => x.Loudness) + without.Sum(x => x.Loudness);
        foreach (var (_, part, loudness, _) in Unmatched(with, without))
            parts[part] = parts.GetValueOrDefault(part) + loudness / all;

        var moments = 0;
        var differ = new Dictionary<string, int>();
        foreach (var track in _with.Tracks.Keys.Intersect(_without.Tracks.Keys))
        {
            var (a, b) = (_with.Tracks[track], _without.Tracks[track]);
            for (var k = (int)Math.Ceiling(from / Sample); k * Sample < to; k++)
            {
                var at = k * Sample;
                if (!a.Sounds(k) && !b.Sounds(k))
                    continue;
                moments++;
                if (Math.Abs(a.BendAt(at) - b.BendAt(at)) > BendDifference || Math.Abs(a.ExpressionAt(at) - b.ExpressionAt(at)) > ExpressionDifference
                                                                         || a.Controllers.Keys.Union(b.Controllers.Keys).Any(c => Math.Abs(a.ControllerAt(c, at) - b.ControllerAt(c, at)) > ControllerDifference))
                    differ[a.Name] = differ.GetValueOrDefault(a.Name) + 1;
            }
        }

        foreach (var (part, count) in differ)
            parts[part] = parts.GetValueOrDefault(part) + count / (double)moments;
        return parts;
    }

    /// <summary>The first second from one to another where the two differ, a note or an effect; none where they do not.</summary>
    public double? FirstDifference(double from, double to)
    {
        var notes = Unmatched(_with.NotesBetween(from, to), _without.NotesBetween(from, to)).Select(x => (double?)x.Seconds).Min();
        double? effects = null;
        foreach (var track in _with.Tracks.Keys.Intersect(_without.Tracks.Keys))
        {
            var (a, b) = (_with.Tracks[track], _without.Tracks[track]);
            for (var k = (int)Math.Ceiling(from / Sample); k * Sample < to && (effects is null || k * Sample < effects); k++)
            {
                var at = k * Sample;
                if ((a.Sounds(k) || b.Sounds(k)) && (Math.Abs(a.BendAt(at) - b.BendAt(at)) > BendDifference || Math.Abs(a.ExpressionAt(at) - b.ExpressionAt(at)) > ExpressionDifference
                                                     || a.Controllers.Keys.Union(b.Controllers.Keys).Any(c => Math.Abs(a.ControllerAt(c, at) - b.ControllerAt(c, at)) > ControllerDifference)))
                {
                    effects = at;
                    break;
                }
            }
        }

        return notes is { } n && effects is { } e ? Math.Min(n, e) : notes ?? effects;
    }

    /// <summary>
    ///     The notes of either that the other has none of alike, its track, pitch and instrument, within
    ///     <see cref="TimingDifference" /> of it and <see cref="LoudnessDifference" /> as loud; each note matched once, in order.
    /// </summary>
    private static IEnumerable<(string Key, string Part, double Loudness, double Seconds)> Unmatched(
        IReadOnlyList<(string Key, string Part, double Loudness, double Seconds)> with, IReadOnlyList<(string Key, string Part, double Loudness, double Seconds)> without)
    {
        var free = without.Select((x, i) => (x, i)).GroupBy(x => x.x.Key).ToDictionary(x => x.Key, x => x.Select(y => y.i).ToList());
        var matched = new bool[without.Count];
        foreach (var note in with)
        {
            var match = free.TryGetValue(note.Key, out var candidates)
                ? candidates.Where(i => Math.Abs(without[i].Seconds - note.Seconds) <= TimingDifference && Math.Abs(without[i].Loudness - note.Loudness) <= LoudnessDifference)
                    .DefaultIfEmpty(-1).MinBy(i => i < 0 ? double.MaxValue : Math.Abs(without[i].Seconds - note.Seconds))
                : -1;
            if (match < 0)
                yield return note;
            else
            {
                matched[match] = true;
                candidates!.Remove(match);
            }
        }

        foreach (var (note, i) in without.Select((x, i) => (x, i)))
            if (!matched[i])
                yield return note;
    }

    /// <summary>A part as a listener knows it: a pitched part's role and the instrument it plays there, a drum by its name.</summary>
    private static string NameOf(TrackRole role, bool isPercussion, int code) => isPercussion
        ? DrumGroups.AllDrums.FirstOrDefault(x => x.ArticulationCodes.Contains(code))?.Name ?? "Drums"
        : $"{role switch
        {
            TrackRole.CounterMelody => "Counter-melody",
            TrackRole.RiffTwin => "Riff's twin",
            TrackRole.Rhythm => "Rhythm part",
            _ => role.ToString()
        }} ({GeneralMidi.Programs[code]})";

    /// <summary>A controller as no MIDI one is, which the insertion effect a part plays through is told by.</summary>
    private const int InsertionController = 1000;

    /// <summary>
    ///     A track's controllers in time, and the insertion effect it plays through as one of them, its kind's number over
    ///     the whole song, as a wah or a phaser is heard on every note of the part (Midi assigns its part's channels).
    /// </summary>
    private static Dictionary<int, (double Seconds, double Value)[]> Controllers(RenderedSong song, RenderedTrack track)
    {
        var controllers = track.Controllers.GroupBy(x => x.Controller).ToDictionary(x => x.Key, x => x.Select(c => (song.SecondsAt(c.Position), (double)c.Value)).OrderBy(c => c.Item1).ToArray());
        if (song.Insertion is { } insertion && !track.IsPercussionInstrument && !track.IsRiser && track.Role.Source() == insertion.Part)
            controllers[InsertionController] = [(0.0, 16 * (1 + (int)insertion.Kind))];
        return controllers;
    }

    /// <summary>A rendering in seconds: its notes as heard, and every track's effects over time.</summary>
    private sealed class Rendering
    {
        // every note by its track, its pitch and its instrument, with its part's name, its second and its loudness
        private readonly (string Key, string Part, double Seconds, double Loudness)[] _notes;

        public Dictionary<string, Track> Tracks { get; }

        public Rendering(RenderedSong song)
        {
            var notes = new List<(string, string, double, double)>();
            Tracks = [];
            foreach (var (track, i) in song.Tracks.Select((x, i) => (x, i)))
            {
                // a track known by its part, whether it is its part's second take, and its place among the part's tracks
                var name = $"{track.Role} {track.IsDouble} {song.Tracks.Take(i).Count(x => x.Role == track.Role && x.IsDouble == track.IsDouble)}";
                var sounding = new List<(double From, double To)>();
                foreach (var note in track.NoteTimeline)
                {
                    // where it is heard, its part's pocket moving it off its place, and on which instrument
                    var seconds = song.SecondsAt(note.Position + note.Value.Shift);
                    var program = track.ProgramChanges.LastOrDefault(x => x.Position <= note.Position + 1e-9, (0, track.PitchInstrumentCode)).Program;
                    notes.Add(($"{name} {note.Value.Offset} {program}",
                        NameOf(track.Role, track.IsPercussionInstrument, track.IsPercussionInstrument ? note.Value.Offset : program), seconds, note.Value.Velocity * track.Volume));
                    sounding.Add((seconds, song.SecondsAt(note.Position + note.Value.Shift + note.Value.Duration)));
                }

                // where it sounds, a moment every sample
                var grid = new bool[(int)Math.Ceiling(song.Seconds / Sample) + 1];
                foreach (var (start, end) in sounding)
                    for (var k = (int)Math.Ceiling(start / Sample); k < grid.Length && k * Sample < end; k++)
                        grid[k] = true;
                Tracks[name] = new Track(
                    NameOf(track.Role, track.IsPercussionInstrument, track.PitchInstrumentCode),
                    grid,
                    // in time, as MIDI plays them, each note having written its own to its end: the bends and controllers in
                    // the order of their times, the expression a position's latest written at or before it, as Midi merges it
                    [..track.PitchBends.Select(x => (song.SecondsAt(x.Position), (x.Value - 8192) / 8191.0 * ExpressionRender.BendRange)).OrderBy(x => x.Item1)],
                    Track.AsWritten([..track.Expression.Select(x => (song.SecondsAt(x.Position), x.Value))]),
                    Controllers(song, track)
                );
            }

            _notes = [..notes.OrderBy(x => x.Item3)];
        }

        /// <summary>The notes starting from one second to another, in order, each its key, its part's name, its loudness and its second.</summary>
        public List<(string Key, string Part, double Loudness, double Seconds)> NotesBetween(double from, double to) =>
            [.._notes.Where(x => x.Seconds >= from && x.Seconds < to).Select(x => (x.Key, x.Part, x.Loudness, x.Seconds))];
    }

    /// <summary>A track's part's name, whether it sounds at every sample, and its bend in semitones, its expression and its controllers over time, in seconds.</summary>
    private sealed record Track(
        string Name,
        bool[] Sounding,
        (double Seconds, double Value)[] Bends,
        (double Seconds, double Value)[] Expressions,
        Dictionary<int, (double Seconds, double Value)[]> Controllers
    )
    {
        public bool Sounds(int sample) => sample < Sounding.Length && Sounding[sample];

        public double BendAt(double at) => ValueAt(Bends, at, 0);

        public double ExpressionAt(double at) => ValueAt(Expressions, at, 1);

        public double ControllerAt(int controller, double at) => Controllers.TryGetValue(controller, out var values) ? ValueAt(values, at, -1) : -1;

        /// <summary>Values in the order they were written as a timeline: at each second, the latest written of those at or before it.</summary>
        public static (double Seconds, double Value)[] AsWritten((double Seconds, double Value)[] written)
        {
            var (latest, next) = (-1, 0);
            var byTime = Enumerable.Range(0, written.Length).OrderBy(i => written[i].Seconds).ToArray();
            var result = new List<(double Seconds, double Value)>();
            foreach (var at in byTime.Select(i => written[i].Seconds).Distinct())
            {
                for (; next < byTime.Length && written[byTime[next]].Seconds <= at; next++)
                    latest = Math.Max(latest, byTime[next]);
                result.Add((at, written[latest].Value));
            }

            return [..result];
        }

        /// <summary>The last value set at or before a second, the one given before any is.</summary>
        private static double ValueAt((double Seconds, double Value)[] values, double at, double before)
        {
            var (low, high) = (0, values.Length);
            while (low < high)
            {
                var middle = (low + high) / 2;
                if (values[middle].Seconds <= at)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low == 0 ? before : values[low - 1].Value;
        }
    }
}
