using Rmg.Core.Composition;
using Rmg.Core.Songs;

namespace Rmg.Tests.Forms;

/// <summary>A song opening on its chorus's hook (<see cref="IntroKind.Hook" />).</summary>
public sealed class HookIntroTest
{
    /// <summary>The song's first chorus where it plays, for a song that opens on its hook.</summary>
    private static SectionSpan Chorus(CorpusSong song)
    {
        var structure = (SongStructure)song.Trace.Single(x => x.Point == TracePoints.SongForm).Value!;
        return song.Map.Sections.First(x => structure.Roles[x.SectionId] == SectionRole.Chorus);
    }

    /// <summary>
    ///     A track's notes from a place for as long as given, as their pitch classes in the key there by where they fall
    ///     from it, so that a hook in the song's first key and its chorus in another after a key change are alike.
    /// </summary>
    private static (double At, int[] Classes)[] InKey(CorpusSong song, int track, double from, double length)
    {
        var keys = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(Rmg.Core.Events.StateKinds.KeyOffset);
        return song.Song.Notes!.TryGetValue(track, out var notes)
            ? [..notes.Where(x => x.Position >= from - 1e-9 && x.Position < from + length - 1e-9)
                .Select(x => (Math.Round(x.Position - from, 6), x.Value.Pitches.Select(p => ((p - keys.GetEffectiveValueAt(x.Position)) % 12 + 12) % 12).ToArray()))]
            : [];
    }

    [Test]
    public async Task AHookIntro_PlaysTheChorussFirstPhrase_NoteForNote()
    {
        // a first chorus played as a solo plays another line where the intro quotes its tune
        var hooks = TestCorpus.Range(256).Where(x => x.Map.Intro.Kind == IntroKind.Hook && !x.IsSolo(Chorus(x))).ToArray();
        await Assert.That(hooks.Length).IsGreaterThan(5);
        var compared = 0;
        foreach (var song in hooks)
        {
            var chorus = Chorus(song);
            // its length the chorus's first phrase
            await Assert.That(song.Origin).IsEqualTo(chorus.Meter.PhraseDuration).Because($"seed {song.Seed}");
            // every line's notes as the chorus plays them, but in the last bar, which leads into the first section
            var quoted = song.Origin - chorus.Meter.BarDuration;
            // every part's, the drums' too: the same notes at the same places, as every stage made the chorus's; but where the
            // chorus is in a key or at a tempo of its own, which the quote leaves to the lines
            bool Alike<T>(Rmg.Core.Events.StateKind<T> kind) where T : notnull
            {
                var timeline = song.Song.TrackEventStateTimelineMap.CommonStateTimelineMap.GetStateTimeline(kind);
                return !timeline.Any(x => x.Position > 1e-9 && x.Position < quoted - 1e-9 || x.Position > chorus.Start + 1e-9 && x.Position < chorus.Start + quoted - 1e-9)
                       && Equals(timeline.GetEffectiveValueAt(chorus.Start), timeline.GetEffectiveValueAt(0));
            }

            if (!Alike(Rmg.Core.Events.StateKinds.KeyOffset) || !Alike(Rmg.Core.Events.StateKinds.Tempo))
                continue;
            foreach (var track in song.Song.Notes!.Keys)
            {
                // a drum's notes as they are, which no key moves
                (double At, int[] Classes)[] Of(double from) => song.IsDrum(track)
                    ? [..song.Song.Notes[track].Where(x => x.IsWithin(from, from + quoted)).Select(x => (Math.Round(x.Position - from, 6), x.Value.Pitches.ToArray()))]
                    : InKey(song, track, from, quoted);
                var (intro, played) = (Of(0), Of(chorus.Start));
                await Assert.That(intro.Select(x => x.At)).IsEquivalentTo(played.Select(x => x.At)).Because($"seed {song.Seed}, track {track}");
                foreach (var ((at, pitches), (_, chorusPitches)) in intro.Zip(played))
                {
                    compared++;
                    await Assert.That(pitches.Order()).IsEquivalentTo(chorusPitches.Order()).Because($"seed {song.Seed}, track {track} at {at}");
                }
            }
        }

        await Assert.That(compared).IsGreaterThan(200);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var kinds = TestCorpus.Measure(1024, song => (Fifth: Math.Min(4, (int)(song.Unconventionality[Facet.Form] * 5)), song.Map.Intro.Kind));
        foreach (var fifth in Enumerable.Range(0, 5))
        {
            var of = kinds.Where(x => x.Fifth == fifth).ToArray();
            Console.WriteLine($"form fifth {fifth}: {of.Length} songs, " + string.Join(", ", Enum.GetValues<IntroKind>().Select(k => $"{k} {100.0 * of.Count(x => x.Kind == k) / Math.Max(1, of.Length):0}%")));
        }

        // how many of the hook's melody notes the chorus plays at the same pitch
        var same = TestCorpus.Measure(256, song =>
        {
            if (song.Map.Intro.Kind != IntroKind.Hook || song.IsSolo(Chorus(song)))
                return (0, 0);
            var chorus = Chorus(song);
            var played = InKey(song, song.Track(TrackRole.Melody), chorus.Start, song.Origin).ToDictionary(x => x.At, x => x.Classes);
            var intro = InKey(song, song.Track(TrackRole.Melody), 0, song.Origin).Where(x => played.ContainsKey(x.At)).ToArray();
            return (intro.Count(x => x.Classes.All(played[x.At].Contains)), intro.Length);
        });
        Console.WriteLine($"the hook's melody notes as the chorus plays them, in their keys, where it plays: {same.Sum(x => x.Item1)} of {same.Sum(x => x.Item2)}");
        await Task.CompletedTask;
    }
}
