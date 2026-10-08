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

    private readonly Rendering _with;
    private readonly Rendering _without;

    public HeardDifference(RenderedSong with, RenderedSong without)
    {
        (_with, _without) = (new Rendering(with), new Rendering(without));
    }

    /// <summary>How much the two differ from one second to another: the notes' share and the effects', each from 0 to 1, added.</summary>
    public double Between(double from, double to)
    {
        var (with, without) = (_with.NotesBetween(from, to), _without.NotesBetween(from, to));
        var all = with.Sum(x => x.Value) + without.Sum(x => x.Value);
        var differing = with.Where(x => !without.ContainsKey(x.Key)).Sum(x => x.Value) + without.Where(x => !with.ContainsKey(x.Key)).Sum(x => x.Value);
        var notes = all > 0 ? differing / all : 0;

        var (moments, differ) = (0, 0);
        foreach (var track in _with.Tracks.Keys.Intersect(_without.Tracks.Keys))
        {
            var (a, b) = (_with.Tracks[track], _without.Tracks[track]);
            for (var k = (int)Math.Ceiling(from / Sample); k * Sample < to; k++)
            {
                var at = k * Sample;
                if (!a.Sounds(k) && !b.Sounds(k))
                    continue;
                moments++;
                differ += Math.Abs(a.BendAt(at) - b.BendAt(at)) > BendDifference || Math.Abs(a.ExpressionAt(at) - b.ExpressionAt(at)) > ExpressionDifference
                                                                                  || a.Controllers.Keys.Union(b.Controllers.Keys).Any(c => Math.Abs(a.ControllerAt(c, at) - b.ControllerAt(c, at)) > ControllerDifference)
                    ? 1
                    : 0;
            }
        }

        return notes + (moments > 0 ? differ / (double)moments : 0);
    }

    /// <summary>A rendering in seconds: its notes as heard, and every track's effects over time.</summary>
    private sealed class Rendering
    {
        // every note by its track, its second, its pitch and its loudness, with its loudness
        private readonly (string Key, double Seconds, double Loudness)[] _notes;

        public Dictionary<string, Track> Tracks { get; }

        public Rendering(RenderedSong song)
        {
            var notes = new List<(string, double, double)>();
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
                    notes.Add(($"{name} {Math.Round(seconds, 2)} {note.Value.Offset} {Math.Round(note.Value.Velocity, 2)} {program}", seconds, note.Value.Velocity * track.Volume));
                    sounding.Add((seconds, song.SecondsAt(note.Position + note.Value.Shift + note.Value.Duration)));
                }

                // where it sounds, a moment every sample
                var grid = new bool[(int)Math.Ceiling(song.Seconds / Sample) + 1];
                foreach (var (start, end) in sounding)
                    for (var k = (int)Math.Ceiling(start / Sample); k < grid.Length && k * Sample < end; k++)
                        grid[k] = true;
                Tracks[name] = new Track(
                    grid,
                    [..track.PitchBends.Select(x => (song.SecondsAt(x.Position), (x.Value - 8192) / 8191.0 * ExpressionRender.BendRange))],
                    [..track.Expression.Select(x => (song.SecondsAt(x.Position), x.Value))],
                    track.Controllers.GroupBy(x => x.Controller).ToDictionary(x => x.Key, x => x.Select(c => (song.SecondsAt(c.Position), (double)c.Value)).ToArray())
                );
            }

            _notes = [..notes.OrderBy(x => x.Item2)];
        }

        /// <summary>The notes starting from one second to another, by their keys, each its loudness.</summary>
        public Dictionary<string, double> NotesBetween(double from, double to)
        {
            var result = new Dictionary<string, double>();
            foreach (var (key, seconds, loudness) in _notes)
                if (seconds >= from && seconds < to)
                    result[key] = result.GetValueOrDefault(key) + loudness;
            return result;
        }
    }

    /// <summary>Whether a track sounds at every sample, and its bend in semitones, its expression and its controllers over time, in seconds.</summary>
    private sealed record Track(
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
