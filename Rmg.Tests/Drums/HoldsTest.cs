using System.Collections.Immutable;
using Rmg.Core.Composition;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Drums;

/// <summary>
///     What the stages hold, written by the stage that makes each stretch (<see cref="SongDraft.Held" />), and kept out of by
///     the stages that name its kind: the notes a drum stage adds never in a stretch it keeps out of.
/// </summary>
public sealed class HoldsTest
{
    /// <summary>The drum stages that add notes, and the kinds of the stages' holds each keeps out of.</summary>
    private static readonly (string Stage, Hold Kinds)[] Adders =
    [
        ("Ghosts", Hold.Fill | Hold.DrumSolo | Hold.Polymeter),
        ("Hat rolls", Hold.Fill | Hold.DrumSolo)
    ];

    [Test]
    [Arguments("funk")]
    [Arguments("hipHop")]
    [Arguments("dnb")]
    [Arguments("reggae")]
    public async Task ANoteADrumStageAdds_IsNeverInAStretchItKeepsOutOf(string genre)
    {
        var added = 0;
        foreach (var seed in Enumerable.Range(0, 16))
        {
            var drafts = new Dictionary<string, SongDraft>();
            string? last = null;
            SongGenerator.GenerateSong(TestCorpus.SongSeed(seed), new SongOverrides(Genre: GenrePresets.Genre(genre)), ImmutableHashSet<string>.Empty, (stage, draft) =>
            {
                if (last is not null && Adders.Any(x => x.Stage == stage))
                    drafts[stage + " before"] = drafts[last];
                drafts[stage] = draft;
                last = stage;
            }, CancellationToken.None);
            foreach (var (stage, kinds) in Adders)
            {
                var (before, after) = (drafts[stage + " before"], drafts[stage]);
                var holds = after.Held.Where(x => (x.Kind & kinds) != 0).ToArray();
                foreach (var (track, notes) in after.Notes!)
                {
                    var had = before.Notes!.GetValueOrDefault(track)?.Select(x => Math.Round(x.Position, 6)).ToHashSet() ?? [];
                    foreach (var note in notes.Where(x => !had.Contains(Math.Round(x.Position, 6))))
                    {
                        added++;
                        await Assert.That(holds.Any(x => (x.Track is null || x.Track == track) && note.Position >= x.Start + 1e-9 && note.Position < x.End - 1e-9))
                            .IsFalse().Because($"seed {seed}, {stage} at {note.Position} on track {track}");
                    }
                }
            }
        }

        await Assert.That(added).IsGreaterThan(0);
    }
}
