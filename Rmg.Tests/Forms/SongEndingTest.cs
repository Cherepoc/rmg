using System.Globalization;
using System.Text.RegularExpressions;
using Rmg.Core;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Rendering;
using Rmg.Core.Songs;

namespace Rmg.Tests.Forms;

public sealed class SongEndingTest
{
    private const int MelodyTrack = 5;
    private const int BassTrack = 6;

    private sealed record Ending(EndingKind Kind, double Line, double Held, double Stop, bool SlowsDown);

    private sealed record EndedSong(Song Song, RenderedSong Rendered, Ending Ending);

    private static double Number(string description, string pattern) =>
        Regex.Match(description, pattern) is { Success: true } match ? double.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;

    private static EndedSong Generate(int seed)
    {
        using var trace = StateTrace.Start();
        var song = SongGenerator.GenerateSong(seed);
        var description = trace.Entries.Single(x => x.Point == "Song form").Phrase!;
        var ending = new Ending(
            Enum.Parse<EndingKind>(description.Split(' ')[0]),
            Number(description, @"at beat ([\d.]+)"),
            Number(description, @"held ([\d.]+)"),
            Number(description, @"stopping ([\d.]+)"),
            description.Contains("slowing down")
        );
        return new EndedSong(song, Render.RenderSong(song), ending);
    }

    private static readonly IReadOnlyList<EndedSong> Songs = Enumerable.Range(0, 40).Select(Generate).ToArray();

    private static TimelineItem<RenderedNote>[] Notes(EndedSong song, int track)
    {
        var program = ((PitchInstrumentTrack)song.Song.TrackDefinitions[track]).InstrumentCode;
        return song.Rendered.Tracks
            .Where(x => !x.IsPercussionInstrument && x.PitchInstrumentCode == program)
            .SelectMany(x => x.NoteTimeline)
            .OrderBy(x => x.Position)
            .ToArray();
    }

    private static int Tonic(EndedSong song) =>
        song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(StateKinds.KeyOffset).Mod(12);

    [Test]
    public async Task ClosedEndings_LandOnTheTonic()
    {
        var closed = Songs.Where(x => x.Ending.Kind != EndingKind.Open).ToArray();
        var bassOnTonic = closed.Count(x => Notes(x, BassTrack)[^1].Value.Offset.Mod(12) == Tonic(x));
        var melodyOnTonic = closed.Count(x => Notes(x, MelodyTrack)[^1].Value.Offset.Mod(12) == Tonic(x));

        await Assert.That(closed.Length).IsGreaterThan(20);
        await Assert.That(bassOnTonic).IsEqualTo(closed.Length);
        await Assert.That(melodyOnTonic).IsEqualTo(closed.Length);
    }

    [Test]
    public async Task TheFinalChord_PlaysOnTheLine_AndIsHeld()
    {
        foreach (var song in Songs.Where(x => x.Ending.Kind != EndingKind.Open))
        {
            var bass = Notes(song, BassTrack)[^1];

            await Assert.That(bass.Position).IsEqualTo(song.Ending.Line);
            await Assert.That(bass.Value.Duration).IsEqualTo(song.Ending.Held);
            await Assert.That(song.Song.Duration).IsEqualTo(song.Ending.Line + Math.Max(song.Ending.Held, 4));
        }
    }

    [Test]
    public async Task Stops_SilenceTheBand_BeforeTheFinalChord()
    {
        var stops = Songs.Where(x => x.Ending.Kind == EndingKind.Stop).ToArray();
        foreach (var song in stops)
        {
            var from = song.Ending.Line - song.Ending.Stop;
            var sounding = song.Rendered.Tracks
                .SelectMany(x => x.NoteTimeline)
                .Where(x => x.Position < song.Ending.Line && x.Position + (x.Value.Duration > 0.25 ? x.Value.Duration : 0) > from + 1e-9)
                .ToArray();

            await Assert.That(sounding).IsEmpty();
        }

        await Assert.That(stops.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task Ritardandos_SlowTheBarBeforeTheEnding()
    {
        var slowing = Songs.Where(x => x.Ending.SlowsDown).ToArray();
        foreach (var song in slowing)
        {
            var tempo = song.Rendered.TempoTimeline;
            var before = tempo.GetEffectiveValueAt(song.Ending.Line - 5);

            await Assert.That(tempo.GetEffectiveValueAt(song.Ending.Line - 1)).IsLessThan(before);
            await Assert.That(tempo.GetEffectiveValueAt(song.Ending.Line)).IsEqualTo(before * FormLayers.Ritardando[^1]).Within(1e-9);
        }

        await Assert.That(slowing.Length).IsGreaterThan(0);
    }

    [Test]
    public async Task WildSongs_EndOpenOrStoppedMoreOften()
    {
        double Share(double chanceScale)
        {
            var weights = FormLayers.WeighEndings(chanceScale);
            return weights.Where(x => FormLayers.AdventurousEndings.Contains(x.Value)).Sum(x => x.Weight) / weights.Sum(x => x.Weight);
        }

        await Assert.That(Share(0.25)).IsLessThan(Share(1));
        await Assert.That(Share(4)).IsGreaterThan(Share(1));
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
            1
        );

        var ending = SongFormGenerator.CreateEnding(section, [], 8, 8);
        var melody = ending.TrackTimelineMap[MelodyTrack].EventTimeline.Single();

        await Assert.That(ending.Duration).IsEqualTo(8);
        await Assert.That(melody.Position).IsEqualTo(0);
        await Assert.That(melody.Value.GetStateValue(StateKinds.HeldDuration)).IsEqualTo(8);
        await Assert.That(melody.Value.GetStateValue(StateKinds.MelodyFinal)).IsEqualTo(1);
        await Assert.That(ending.TrackTimelineMap[drum].EventTimeline).IsEmpty();
        // the bass rests in the home bar, and plays its first note of the section
        await Assert.That(ending.TrackTimelineMap[BassTrack].EventTimeline.Single().Position).IsEqualTo(0);
        await Assert.That(ending.CommonStateTimelineMap.GetEffectiveStateMapAt(0).GetStateValue(StateKinds.ChordArrival))
            .IsEqualTo((int)ChordArrival.Root);
    }
}
