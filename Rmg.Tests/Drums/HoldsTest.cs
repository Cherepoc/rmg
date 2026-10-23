using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Drums;

/// <summary>
///     What the song holds (<see cref="SongDraft.Holds" />), its stages' stretches, a drop's build, its stop time and where
///     the form stops a part, kept out of by the stages that name its kind: the notes a stage adds or moves never in a
///     stretch it keeps out of.
/// </summary>
public sealed class HoldsTest
{
    /// <summary>The stages that add or move notes, the kinds each keeps out of, as the stage names them, and whether it plays the drums alone.</summary>
    private static readonly (string Stage, Hold Kinds, bool Drums)[] Adders =
    [
        ("Figures", Figures.KeepsOutOf, true),
        ("Mutations", Mutations.KeepsOutOf, true),
        ("Ghosts", Ghosts.KeepsOutOf, true),
        ("Hat rolls", HatRolls.KeepsOutOf, true),
        ("Drum ornaments", DrumOrnaments.KeepsOutOf, true)
    ];

    /// <summary>A track's events after a stage, its notes once realized, its states before.</summary>
    private static double[] Positions(SongDraft draft, int track) =>
        draft.Notes is { } notes
            ? notes.TryGetValue(track, out var timeline) ? [..timeline.Select(x => Math.Round(x.Position, 6))] : []
            : draft.Timeline.TrackTimelineMap.TryGetValue(track, out var map) ? [..map.EventTimeline.Select(x => Math.Round(x.Position, 6))] : [];

    [Test]
    [Arguments("funk")]
    [Arguments("hipHop")]
    [Arguments("dnb")]
    [Arguments("reggae")]
    [Arguments("surf")]
    [Arguments("trap")]
    public async Task ANoteAStageAdds_IsNeverInAStretchItKeepsOutOf(string genre)
    {
        var added = 0;
        var overrides = new SongOverrides(Genre: GenrePresets.Genre(genre));
        foreach (var seed in Enumerable.Range(0, 16))
        {
            var song = SongDecisions.Draw(TestCorpus.SongSeed(seed), overrides, false, CancellationToken.None);
            var drafts = new Dictionary<string, SongDraft>();
            string? last = null;
            SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), overrides, ImmutableHashSet<string>.Empty, (stage, draft) =>
            {
                if (last is not null && Adders.Any(x => x.Stage == stage))
                    drafts[stage + " before"] = drafts[last];
                drafts[stage] = draft;
                last = stage;
            }, CancellationToken.None);
            foreach (var (stage, kinds, drums) in Adders)
            {
                var (before, after) = (drafts[stage + " before"], drafts[stage]);
                var holds = after.Holds(song);
                foreach (var track in song.Tracks.Definitions.Keys.Where(x => !drums || song.Tracks.Definitions[x].Role == TrackRole.Drum))
                {
                    var had = Positions(before, track).ToHashSet();
                    foreach (var position in Positions(after, track).Where(x => !had.Contains(x)))
                    {
                        added++;
                        await Assert.That(holds.Of(kinds, track).Any(x => position >= x.Start + 1e-9 && position < x.End - 1e-9))
                            .IsFalse().Because($"seed {seed}, {stage} at {position} on track {track}");
                    }
                }
            }
        }

        await Assert.That(added).IsGreaterThan(0);
    }
}
