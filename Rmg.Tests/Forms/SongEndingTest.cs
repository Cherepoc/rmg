using System.Collections.Immutable;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Forms;

public sealed class SongEndingTest
{
    // the tracks of the songs this builds itself
    private const int MelodyTrack = 5;
    private const int BassTrack = 6;

    private static readonly IReadOnlyList<CorpusSong> Songs = TestCorpus.Range(32).ToArray();

    private static EndingSpan Ending(CorpusSong song) => song.Map.Ending;

    // the song's tonic as it ends, which a key change moves
    private static int Tonic(CorpusSong song) =>
        song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(StateKinds.KeyOffset).GetEffectiveValueAt(song.Map.Ending.Start).Mod(12);

    [Test]
    public async Task ClosedEndings_LandOnTheTonic()
    {
        var closed = Songs.Where(x => FormLayers.HasFinalChord(Ending(x).Kind)).ToArray();
        var bassOnTonic = closed.Count(x => x.Notes(x.Track(TrackRole.Bass))[^1].Value.Offset.Mod(12) == Tonic(x));
        var melodyOnTonic = closed.Count(x => x.Notes(x.Track(TrackRole.Melody))[^1].Value.Offset.Mod(12) == Tonic(x));

        await Assert.That(closed.Length).IsGreaterThan(20);
        await Assert.That(bassOnTonic).IsEqualTo(closed.Length);
        await Assert.That(melodyOnTonic).IsEqualTo(closed.Length);
    }

    [Test]
    public async Task AFadingSong_PlaysItsLastSectionOnceMore_AndFadesOutOverIt()
    {
        var fading = TestCorpus.Range(128).Where(x => Ending(x).Kind == EndingKind.Fade).ToArray();

        await Assert.That(fading.Length).IsGreaterThan(5);
        foreach (var song in fading)
        {
            var sections = song.Map.Sections;
            var fade = song.Rendered.FadeTimeline;
            await Assert.That(sections[^1].SectionId).IsEqualTo(sections[^2].SectionId);
            await Assert.That(Ending(song).Duration).IsEqualTo(0);
            await Assert.That(song.Map.Duration).IsEqualTo(sections[^1].End).Within(1e-9);
            // its first step down a step into the last section, or eight bars from the end where the section is shorter, as a
            // step of 1, as loud as the band plays, is none
            var fadeStart = Math.Min(sections[^1].Start, sections[^1].End - 2 * sections[^1].Meter.PatternDuration);
            await Assert.That(fade[0].Position).IsEqualTo(fadeStart + FormLayers.FadeStep).Within(1e-9);
            await Assert.That(fade[0].Value).IsLessThan(1);
            await Assert.That(fade.Zip(fade.Skip(1)).All(x => x.Second.Value < x.First.Value)).IsTrue();
            // a step above silence at the song's end, the steps as many as the fade's span holds
            await Assert.That(fade[^1].Value).IsEqualTo(FormLayers.FadeStep / (sections[^1].End - fadeStart)).Within(1e-9);
        }

        await Assert.That(Songs.Where(x => Ending(x).Kind != EndingKind.Fade).All(x => x.Rendered.FadeTimeline.Count == 0)).IsTrue();
    }

    [Test]
    public async Task ALinesLastNote_AsksToLandOnTheRoot_GoingOnFromTheNoteBefore()
    {
        var note = StateMap.FromStates([CompositionStateKinds.LinePhraseStart.CreateState((int)PhraseStart.Afresh), StateKinds.Velocity.CreateState(0.5)]).ToTimelineItem(0);

        var landed = SongFormGenerator.LandOnRoot(note).Value;
        await Assert.That(landed.GetStateValue(CompositionStateKinds.LineLanding)).IsEqualTo((int)ChordArrival.Root);
        await Assert.That(landed.GetStateValue(CompositionStateKinds.LinePhraseStart)).IsEqualTo((int)PhraseStart.None);
        await Assert.That(landed.GetStateValue(StateKinds.Velocity)).IsEqualTo(0.5);
    }

    [Test]
    public async Task TheFinalChord_PlaysOnTheLine_AndIsHeld()
    {
        foreach (var song in Songs.Where(x => FormLayers.HasFinalChord(Ending(x).Kind)))
        {
            var bass = song.Notes(song.Track(TrackRole.Bass))[^1];

            await Assert.That(bass.Position).IsEqualTo(Ending(song).Start);
            await Assert.That(bass.Value.Duration).IsEqualTo(Ending(song).Held);
            await Assert.That(song.Song.Duration).IsEqualTo(Ending(song).Start + Ending(song).Duration);
        }
    }

    [Test]
    public async Task Stops_SilenceTheBand_BeforeTheFinalChord()
    {
        var stops = Songs.Where(x => Ending(x).Kind == EndingKind.Stop).ToArray();
        foreach (var song in stops)
        {
            var from = Ending(song).Start - Ending(song).Stop;
            var sounding = song.Rendered.Tracks
                .SelectMany(x => x.NoteTimeline)
                // struck in the silence, its first beat too, or a long note ringing into it
                .Where(x => x.Position >= from - 1e-9 && x.Position < Ending(song).Start - 1e-9
                            || x.Position < from - 1e-9 && x.Value.Duration > 0.25 && x.Position + x.Value.Duration > from + 1e-9)
                .ToArray();

            await Assert.That(sounding).IsEmpty();
        }

        await Assert.That(stops.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Ritardandos_SlowTheBarBeforeTheEnding()
    {
        var slowing = Songs.Where(x => Ending(x).SlowsDown).ToArray();
        foreach (var song in slowing)
        {
            var tempo = song.Rendered.TempoTimeline;
            // the tempo before the slowing bar, the last section's, whatever its meter
            var before = tempo.GetEffectiveValueAt(Ending(song).Start - song.Map.Sections[^1].Meter.BarDuration - 1e-6);

            await Assert.That(tempo.GetEffectiveValueAt(Ending(song).Start - 1)).IsLessThan(before);
            await Assert.That(tempo.GetEffectiveValueAt(Ending(song).Start)).IsEqualTo(before * FormLayers.Ritardando[^1]).Within(1e-9);
        }

        await Assert.That(slowing.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task WildSongs_EndOpenOrStoppedMoreOften()
    {
        double Share(double unconventionality)
        {
            var weights = FormLayers.WeighEndings(unconventionality, Genre.None);
            return weights.Where(x => FormLayers.AdventurousEndings.Contains(x.Value)).Sum(x => x.Weight) / weights.Sum(x => x.Weight);
        }

        await Assert.That(Share(0)).IsEqualTo(0);
        await Assert.That(Share(0.25)).IsLessThan(Share(0.5));
        await Assert.That(Share(0.75)).IsGreaterThan(Share(0.5));
        // the wildest's share tuned, 0.4 as it is, bounded widely
        Console.WriteLine($"adventurous endings: {Share(0.5):P0} of a middling song's, {Share(1):P0} of the wildest's");
        await Assert.That(Share(1)).IsBetween(0.2, 0.7);
    }

    [Test]
    public async Task EndingBar_HoldsEachPitchedTracksFirstNote_OnTheRoot()
    {
        var velocity = StateMap.FromStates([StateKinds.Velocity.CreateState(0.5)]);
        EventStateTimelineMap<StateMap> Track(params double[] positions) =>
            EventTimeline.Create(32, positions.Select(x => StateMap.Default.ToTimelineItem(x))).ToEventStateTimelineMap(velocity);
        var drum = DrumGroups.GetTrackNumber(DrumDefinitions.Kick);
        var section = new GeneratedSection(
            TrackEventStateTimelineMap.Create(
                32,
                [
                    new KeyValuePair<int, EventStateTimelineMap<StateMap>>(MelodyTrack, Track(1.5, 2, 5)),
                    new KeyValuePair<int, EventStateTimelineMap<StateMap>>(BassTrack, Track(6, 7)),
                    new KeyValuePair<int, EventStateTimelineMap<StateMap>>(drum, Track(0, 1, 2))
                ],
                StateMap.FromStates([StateKinds.ChordArrival.CreateState((int)ChordArrival.Third)]).ToStateTimelineMap(32)
            ),
            new RhythmicUnconventionality(0.5),
            Unconventionality.Generate(0.5, _ => new GenerationContext(0)),
            FillGrooves.FromSource(ResolvedRhythm.DefaultState),
            0,
            false,
            ImmutableDictionary<int, DrumRole>.Empty,
            [],
            2,
            ImmutableDictionary<int, int>.Empty,
            ImmutableHashSet<TrackRole>.Empty,
            ImmutableHashSet<TrackRole>.Empty,
            SectionRole.Free,
            0,
            new Dictionary<int, TrackRole> { [MelodyTrack] = TrackRole.Melody, [BassTrack] = TrackRole.Bass, [drum] = TrackRole.Drum }.ToImmutableDictionary(),
            Meter.FourFour,
            false,
            false,
            false,
            false,
            false,
            0,
            DrumJob.Groove,
            Genre.None
        );

        var ending = SongFormGenerator.CreateEnding(
            section,
            [],
            8,
            8,
            new Dictionary<int, TrackRole> { [MelodyTrack] = TrackRole.Melody, [BassTrack] = TrackRole.Bass, [drum] = TrackRole.Drum }
        );
        var melody = ending.TrackTimelineMap[MelodyTrack].EventTimeline.Single();

        await Assert.That(ending.Duration).IsEqualTo(8);
        await Assert.That(melody.Position).IsEqualTo(0);
        await Assert.That(melody.Value.GetStateValue(StateKinds.HeldDuration)).IsEqualTo(8);
        await Assert.That(ending.TrackTimelineMap[drum].EventTimeline).IsEmpty();
        // the bass rests in the home bar, and plays its first note of the section
        var bass = ending.TrackTimelineMap[BassTrack].EventTimeline.Single();
        await Assert.That(bass.Position).IsEqualTo(0);
        // every line lands on the root, whatever the bar's own arrival
        await Assert.That(bass.Value.GetStateValue(CompositionStateKinds.LineLanding)).IsEqualTo((int)ChordArrival.Root);
        await Assert.That(melody.Value.GetStateValue(CompositionStateKinds.LineLanding)).IsEqualTo((int)ChordArrival.Root);
    }

    /// <summary>The final chord in the sound the last section left each part in: its reverb, its tone and its tape, not reset at the ending.</summary>
    [Test]
    public async Task TheFinalChord_KeepsTheLastSectionsSound()
    {
        var checkedNotes = 0;
        foreach (var song in TestCorpus.Range(64).Where(x => x.Map.Ending.Duration > 0))
        foreach (var (track, notes) in song.Song.Notes!.Where(x => song.Song.TrackDefinitions[x.Key] is PitchInstrumentTrack))
        {
            var last = notes.LastOrDefault(x => x.IsWithin(song.Map.Sections[^1].Start, song.Map.Ending.Start));
            if (last.Value is null)
                continue;
            foreach (var note in notes.Where(x => x.Position >= song.Map.Ending.Start - 1e-9))
            foreach (var kind in new[] { CompositionStateKinds.Reverb, CompositionStateKinds.Tone, CompositionStateKinds.Wow })
            {
                checkedNotes++;
                await Assert.That(note.Value.State.GetStateValue(kind)).IsEqualTo(last.Value.State.GetStateValue(kind)).Because($"seed {song.Seed}, track {track}, {kind.Name}");
            }
        }

        await Assert.That(checkedNotes).IsGreaterThan(100);
    }
}
