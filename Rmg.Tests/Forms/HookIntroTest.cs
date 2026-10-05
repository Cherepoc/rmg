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
            await Assert.That(song.Origin).IsEqualTo(Meter.PhraseBarCount * chorus.Meter.BarDuration).Because($"seed {song.Seed}");
            // every line's notes as the chorus plays them, but in the last bar, which leads into the first section
            var quoted = song.Origin - chorus.Meter.BarDuration;
            // in its key, and where the chorus doubles a line an octave apart, the intro's notes among its own
            foreach (var track in new[] { SongTracks.MelodyTrack, SongTracks.CounterMelodyTrack })
            {
                var played = InKey(song, track, chorus.Start, quoted).ToDictionary(x => x.At, x => x.Classes);
                // where the chorus plays them, which may rest a part the intro plays
                foreach (var (at, pitches) in InKey(song, track, 0, quoted).Where(x => played.ContainsKey(x.At)))
                {
                    compared++;
                    await Assert.That(pitches.All(played[at].Contains)).IsTrue().Because($"seed {song.Seed}, track {track} at {at}");
                }
            }
        }

        await Assert.That(compared).IsGreaterThan(200);
    }

    [Test]
    [Explicit]
    public async Task Report()
    {
        var kinds = TestCorpus.Measure(1024, song => (Fifth: Math.Min(4, (int)(((Unconventionality)song.Trace.Single(x => x.Point == TracePoints.SongUnconventionality).Value!)[Facet.Form] * 5)), song.Map.Intro.Kind));
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
            var played = InKey(song, SongTracks.MelodyTrack, chorus.Start, song.Origin).ToDictionary(x => x.At, x => x.Classes);
            var intro = InKey(song, SongTracks.MelodyTrack, 0, song.Origin).Where(x => played.ContainsKey(x.At)).ToArray();
            return (intro.Count(x => x.Classes.All(played[x.At].Contains)), intro.Length);
        });
        Console.WriteLine($"the hook's melody notes as the chorus plays them, in their keys, where it plays: {same.Sum(x => x.Item1)} of {same.Sum(x => x.Item2)}");
        await Task.CompletedTask;
    }
}
