using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Tests.Genres;

/// <summary>Djent's riff on an open string's drone, and Black metal's driven guitar acoustic or clean in its calm sections between the storms.</summary>
public sealed class DroneAndQuieterFormTest
{
    private static Genre With(string preset, GenreChoice choice, string option)
    {
        var genre = GenrePresets.Genre(preset);
        var own = genre.Choices.GetValueOrDefault(choice.Name, ImmutableDictionary<string, ByConvention>.Empty);
        return genre with { Choices = genre.Choices.SetItem(choice.Name, own.SetItem(option, new ByConvention(1, 1, 1))) };
    }

    /// <summary>
    ///     A riff's weak notes, those off its beats, on its open string, the lowest of the key's tonic it plays, and its strong
    ///     ones' leaps over it, in semitones, in the sections given.
    /// </summary>
    private static (int OnPedal, int Weak, int Above, int Strong) Pedal(CorpusSong song, IReadOnlySet<int> sections)
    {
        var (onPedal, weak, above, strong) = (0, 0, 0, 0);
        foreach (var (track, notes) in song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key].Role == TrackRole.Riff))
        foreach (var span in song.Map.Sections.Where(x => sections.Contains(x.SectionId) && !song.IsSolo(x)))
        {
            var inSpan = notes.Where(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9 && x.Value.Pitches.Length == 1).ToArray();
            foreach (var note in inSpan)
            {
                var tonic = Rmg.Core.Composition.Realizer.GetChord(note.Value.State).Chord.Tonic;
                var open = inSpan.Select(x => x.Value.Pitches[0]).Where(x => ((x - tonic) % 12 + 12) % 12 == 0).DefaultIfEmpty(int.MinValue).Min();
                if (note.Value.State.GetStateValue(CompositionStateKinds.BeatRank) > 0)
                    (weak, onPedal) = (weak + 1, onPedal + (note.Value.Pitches[0] == open ? 1 : 0));
                // a strong note's leap where the riff plays its string
                else if (open > int.MinValue)
                    (strong, above) = (strong + 1, above + note.Value.Pitches[0] - open);
            }
        }

        return (onPedal, weak, above, strong);
    }

    /// <summary>A droning section's riff plays its weak notes on its open string and its melody over it, leaping about an octave between them.</summary>
    [Test]
    public async Task ADroningRiff_PlaysItsWeakNotesOnItsOpenString_ItsMelodyLeapingOverIt()
    {
        var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: With("djent", GenreChoices.Riff, "drone"))));
        var drones = songs.Select(x => Pedal(x, x.Trace.Where(t => t.Point == TracePoints.Drone && (bool)t.Value!).Select(t => t.Section).ToHashSet())).ToArray();
        var (onPedal, weak, above, strong) = (drones.Sum(x => x.OnPedal), drones.Sum(x => x.Weak), drones.Sum(x => x.Above), drones.Sum(x => x.Strong));
        // the melody an octave or more over the string as far as the guitar reaches, the restated and led notes among the
        // rest, against the mechanism breaking (measured: 99% on the string, 9.5 to 10 semitones over it, at offsets 0 and a
        // million)
        Console.WriteLine($"droning: {onPedal} of {weak} weak notes on the open string, the strong ones {above / (double)strong:F1} semitones over it on the average");
        await Assert.That(weak).IsGreaterThan(100);
        await Assert.That(onPedal / (double)weak).IsGreaterThan(0.9);
        await Assert.That(above / (double)strong).IsGreaterThan(7);
    }

    /// <summary>
    ///     A calm section (<see cref="SectionForm" />): Black metal's quieter sections, against the same songs with none,
    ///     every guitar on an acoustic or a clean one, a figure of 8ths or slower, the other guitars resting, the drums in
    ///     half time and sparser.
    /// </summary>
    [Test]
    public async Task ACalmSection_PlaysItsGuitarsAcousticOrClean_ItsChordsBrokenSlowly_TheDrumsSparser()
    {
        var calm = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: With("blackMetal", GenreChoices.LouderForms, "quieter"))));
        var storm = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Without("blackMetal", GenreChoices.LouderForms.Name))));
        var (sections, guitarNotes, drumsCalm, drumsOwn, gapsSlow, gapsAll) = (0, 0, 0.0, 0.0, 0, 0);
        foreach (var (song, own) in calm.Zip(storm))
        {
            var calmIds = song.Trace.Where(t => t.Point == TracePoints.SectionForm && (SectionFormKind)t.Value! == SectionFormKind.Calm).Select(t => t.Section).ToHashSet();
            // but the first section an intro's entries build up, which bring the guitars in whatever the section rests
            var entering = song.Map.Intro.Kind == IntroKind.Entries ? song.Map.Sections[0] : null;
            foreach (var span in song.Map.Sections.Where(x => calmIds.Contains(x.SectionId) && !song.IsSolo(x) && x != entering))
            {
                sections++;
                var because = $"seed {song.Seed} at {span.Start}";
                // the guitars on one calm program, each the riff's, its twin's, the rhythm part's or the chords'
                var guitars = song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack { Role: TrackRole.Riff or TrackRole.RiffTwin or TrackRole.Rhythm or TrackRole.Chords } part
                                                          && InstrumentTraits.Has(part.InstrumentCode, InstrumentTrait.Driven))
                    .Select(x => (x.Key, Notes: x.Value.Where(n => span.IsWithin(n.Position) && n.Value.State.GetStateValue(CompositionStateKinds.Program) != Risers.Program + 1).ToArray()))
                    .Where(x => x.Notes.Length > 0).ToArray();
                await Assert.That(guitars.SelectMany(x => x.Notes).Select(x => InstrumentsOverTime.ProgramOf(x.Value.State, 0)).Distinct().All(SectionForm.CalmPrograms.Contains)).IsTrue().Because(because);
                // one playing, its notes 8ths apart or more
                await Assert.That(guitars.Length).IsLessThanOrEqualTo(1).Because(because);
                // its notes 8ths apart or more, but where a cycle is cut at a bar line or a chord struck again on its change
                foreach (var (_, notes) in guitars)
                {
                    guitarNotes += notes.Length;
                    var gaps = notes.Zip(notes.Skip(1), (x, y) => y.Position - x.Position).ToArray();
                    (gapsSlow, gapsAll) = (gapsSlow + gaps.Count(x => x >= SectionForm.CalmShortest - 1e-6), gapsAll + gaps.Length);
                }

                // the drums sparser than the same section's in the storm's song
                double Drums(CorpusSong of) => of.Song.Notes!.Where(x => of.IsDrum(x.Key)).Sum(x => x.Value.Count(n => span.IsWithin(n.Position)));
                (drumsCalm, drumsOwn) = (drumsCalm + Drums(song), drumsOwn + Drums(own));
            }
        }

        // the drums in half time and without a figure, against the mechanism breaking (measured: 197 to 221 calm sections,
        // the drums 27% to 36% of the storm's, 90% to 96% of the guitar's notes an 8th or more apart, at offsets 0, a million
        // and three million)
        Console.WriteLine($"{sections} calm sections, {guitarNotes} guitar notes, {gapsSlow / (double)Math.Max(1, gapsAll):P0} of them an 8th or more from the next; the drums {drumsCalm / Math.Max(1, drumsOwn):P0} of the storm's");
        await Assert.That(sections).IsGreaterThan(5);
        await Assert.That(gapsSlow / (double)gapsAll).IsGreaterThan(0.85);
        await Assert.That(drumsCalm).IsLessThan(drumsOwn * 0.75);
    }

    /// <summary>A heavy section turns a louder section's clean guitars driven, its chords struck, not broken.</summary>
    [Test]
    public async Task AHeavySection_DrivesItsCleanGuitars_ItsChordsStruck()
    {
        var heavy = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 32), seed => TestCorpus.Get(seed, new SongOverrides(Genre: With("rock", GenreChoices.LouderForms, "guitars")))))
        {
            var heavyIds = song.Trace.Where(t => t.Point == TracePoints.SectionForm && (SectionFormKind)t.Value! == SectionFormKind.Heavy).Select(t => t.Section).ToHashSet();
            foreach (var (track, notes) in song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack { InstrumentCode: 26 or 27 or 28, Role: TrackRole.Chords or TrackRole.Rhythm or TrackRole.Riff or TrackRole.RiffTwin }))
            foreach (var note in notes.Where(n => song.Map.SectionAt(n.Position) is { } span && heavyIds.Contains(span.SectionId)))
            {
                heavy++;
                // or a pinch's harmonic (Expression)
                await Assert.That(SectionForm.HeavyPrograms.Append(Expression.GuitarHarmonics)).Contains(InstrumentsOverTime.ProgramOf(note.Value.State, 0)).Because($"seed {song.Seed}, track {track} at {note.Position}");
                await Assert.That(note.Value.State.GetStateValue(CompositionStateKinds.Arpeggio)).IsEqualTo(0).Because($"seed {song.Seed}, track {track} at {note.Position}");
            }
        }

        await Assert.That(heavy).IsGreaterThan(100);
    }

    /// <summary>How often the presets drone and play a calm section.</summary>
    [Test]
    [Explicit]
    public async Task Report()
    {
        foreach (var name in new[] { "blackMetal", "djent", "folk" })
        {
            var songs = TestCorpus.InParallel(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(name))));
            var drones = songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.Drone)).Average(t => (bool)t.Value! ? 1.0 : 0);
            var pedal = songs.Select(x => Pedal(x, x.Trace.Where(t => t.Point == TracePoints.Drone && (bool)t.Value!).Select(t => t.Section).ToHashSet())).ToArray();
            var calm = songs.Average(x => x.Trace.Any(t => t.Point == TracePoints.SectionForm && (SectionFormKind)t.Value! == SectionFormKind.Calm) ? 1.0 : 0);
            Console.WriteLine($"{name}: sections droning {drones:P0}, their riff's weak notes on the open string {pedal.Sum(x => x.OnPedal) / (double)Math.Max(1, pedal.Sum(x => x.Weak)):P0}, its strong ones {pedal.Sum(x => x.Above) / (double)Math.Max(1, pedal.Sum(x => x.Strong)):F1} semitones over it; sections chugging {songs.SelectMany(x => x.Trace.Where(t => t.Point == TracePoints.Chugs)).Average(t => (bool)t.Value! ? 1.0 : 0):P0}; songs with a calm section {calm:P0}");
        }

        await Task.CompletedTask;
    }
}
