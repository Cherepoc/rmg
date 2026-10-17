using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>
///     How every preset's parts sound together, beside the songs with no genre, which a rule of one note against its own
///     chord never sees: a part's note held into its own next on an instrument that does not ring on, a part a semitone or
///     a major seventh from another sounding with it on a strong beat, but where both are its chord's, its own notes and those a doubling copied apart, and
///     a strong note off its chord, a chord's missing third among them, so that the review reads what clashes as a number.
/// </summary>
public sealed class SoundingReportTest
{
    private const int Count = 32;

    /// <summary>A song's share of each way of sounding, by its name.</summary>
    internal static Dictionary<string, double> Measure(CorpusSong song)
    {
        var parts = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack && x.Value.Count > 0)
            .Select(x => (Definition: (PitchInstrumentTrack)song.Song.TrackDefinitions[x.Key], Notes: x.Value.ToArray()))
            .Select(x => (x.Definition, x.Notes, Starts: x.Notes.Select(n => n.Position).ToArray()))
            .ToArray();
        static int Class(int pitch) => (pitch % 12 + 12) % 12;
        double Share(int count, int of) => of > 0 ? count / (double)of : double.NaN;

        // a note held past its part's next, on an instrument that stops its note at the next, not one ringing on, a pitch of
        // it the next does not strike again, as a restruck key stops the held one in the file (Midi.TrimOverlaps)
        var (stopping, overlapping) = (0, 0);
        foreach (var (definition, notes, _) in parts)
            for (var i = 0; i + 1 < notes.Length; i++)
            {
                if (InstrumentTraits.Rings(InstrumentsOverTime.ProgramOf(notes[i].Value.State, definition.InstrumentCode)) || notes[i + 1].Position - notes[i].Position < 1e-9)
                    continue;
                stopping++;
                overlapping += notes[i].Position + notes[i].Value.Duration > notes[i + 1].Position + 1e-6 && notes[i].Value.Pitches.Except(notes[i + 1].Value.Pitches).Any() ? 1 : 0;
            }

        // every strong note against the other parts' notes sounding at its start, and against its own chord
        var (own, ownClashing, doubled, doubledClashing, strong, offChord, overThirdless, onMissingThird) = (0, 0, 0, 0, 0, 0, 0, 0);
        foreach (var (definition, notes, _) in parts)
        foreach (var note in notes.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.BeatRank) == 0))
        {
            var sounding = parts.Where(x => x.Definition != definition).SelectMany(x =>
            {
                // the last note starting at or before it, the starts in order
                var found = Array.BinarySearch(x.Starts, note.Position + 1e-9);
                var at = (found >= 0 ? found : ~found) - 1;
                while (at + 1 < x.Starts.Length && x.Starts[at + 1] <= note.Position + 1e-9)
                    at++;
                return at >= 0 && x.Notes[at].Position + x.Notes[at].Value.Duration > note.Position + 1e-6 ? x.Notes[at].Value.Pitches : [];
            }).ToArray();
            // a semitone apart, but a chord's own notes, a major seventh's from its root
            var (chord, _, classes) = Realizer.GetChordNotes(note.Value.State);
            var clashes = note.Value.Pitches.Any(p => sounding.Any(q => Class(p - q) is 1 or 11 && !(classes.Contains(Class(p)) && classes.Contains(Class(q)))));
            if (note.Value.State.GetStateValue(CompositionStateKinds.Doubled) > 0)
                (doubled, doubledClashing) = (doubled + 1, doubledClashing + (clashes ? 1 : 0));
            else
                (own, ownClashing) = (own + 1, ownClashing + (clashes ? 1 : 0));

            var root = Class(chord.Root);
            strong++;
            offChord += note.Value.Pitches.Any(p => !classes.Contains(Class(p))) ? 1 : 0;
            if (classes.Any(x => Class(x - root) is 3 or 4))
                continue;
            overThirdless++;
            onMissingThird += note.Value.Pitches.Any(p => Class(p - root) is 3 or 4) ? 1 : 0;
        }

        return new Dictionary<string, double>
        {
            ["notes held into their part's next"] = Share(overlapping, stopping),
            ["strong notes a semitone from another part"] = Share(ownClashing, own),
            ["doubled strong notes a semitone from another"] = Share(doubledClashing, doubled),
            ["strong notes off their chord"] = Share(offChord, strong),
            ["strong notes on a chord's missing third"] = Share(onMissingThird, overThirdless)
        };
    }

    /// <summary>The report measures a song of every preset and of none, each with every measure.</summary>
    [Test]
    public async Task TheReport_MeasuresASongOfEveryPreset()
    {
        Genre?[] genres = [null, ..GenrePresets.All.Select(GenrePresets.Genre)];
        var measures = TestCorpus.InParallel(Enumerable.Range(0, genres.Length), i => Measure(TestCorpus.Get(0, new SongOverrides(Genre: genres[i])))).ToArray();
        await Assert.That(measures.All(x => x.Keys.ToHashSet().SetEquals(measures[0].Keys))).IsTrue();
        await Assert.That(measures.Any(x => x.Values.Any(double.IsFinite))).IsTrue();
        // each a share, where it measures anything
        await Assert.That(measures.SelectMany(x => x.Values).Where(double.IsFinite).All(x => x is >= 0 and <= 1)).IsTrue();
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var columns = new List<(string Name, Dictionary<string, double>[] Songs)>
        {
            ("none", TestCorpus.InParallel(Enumerable.Range(0, Count), seed => Measure(TestCorpus.Get(seed))))
        };
        columns.AddRange(GenrePresets.All.Select(preset =>
            (preset.Name, TestCorpus.InParallel(Enumerable.Range(0, Count), seed => Measure(TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(preset))))))));

        // a preset a row, as the presets outnumber the measures
        var measures = columns[0].Songs[0].Keys.ToArray();
        Console.WriteLine($"{"",-20}" + string.Join("", measures.Select((_, i) => $"{"m" + (i + 1),8}")));
        foreach (var (name, songs) in columns)
            Console.WriteLine($"{name,-20}" + string.Join("", measures.Select(m =>
                $"{songs.Select(x => x[m]).Where(double.IsFinite).DefaultIfEmpty(double.NaN).Average(),8:P1}")));
        foreach (var (measure, i) in measures.Select((x, i) => (x, i)))
            Console.WriteLine($"m{i + 1}: {measure}");

        await Task.CompletedTask;
    }
}
