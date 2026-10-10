using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Rendering;

/// <summary>A palm-muted note: its articulation in its state, its written length kept, General MIDI playing it short.</summary>
public sealed class PalmMuteTest
{
    [Test]
    public async Task APalmMutedNote_KeepsItsLength_AndIsPlayedShort_OnAWeakBeat_ARepeatOrOnItsPedal()
    {
        int muted = 0, shorter = 0;
        foreach (var song in TestCorpus.InParallel(Enumerable.Range(0, 16), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre("rock")))))
        foreach (var track in new[] { song.Track(TrackRole.Riff), song.Track(TrackRole.Rhythm), song.Track(TrackRole.Bass) })
        {
            if (!song.Song.Notes!.TryGetValue(track, out var notes))
                continue;
            var rendered = song.Rendered.Tracks.Where(x => !x.IsPercussionInstrument && x.Role == song.Song.TrackDefinitions[track].Role).SelectMany(x => x.NoteTimeline).ToArray();
            // the rendered notes where they play, swung
            var swing = Rmg.Core.Rendering.Render.GetSwing(song.Song);
            var items = notes.ToArray();
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].Value.State.GetStateValue(CompositionStateKinds.PalmMute) == 0)
                    continue;
                muted++;
                // on a weak beat, a repeat of the note before or a riff's note chugging on its pedal; but a hook intro's first,
                // which quotes the chorus's after the note before it
                var onPedal = items[i].Value.State.GetStateValue(CompositionStateKinds.LinePedal) > 0;
                await Assert.That(onPedal || i == 0 && song.Map.Intro.Kind == IntroKind.Hook || i > 0 && items[i].Value.Pitches.SequenceEqual(items[i - 1].Value.Pitches)).IsTrue();
                await Assert.That(items[i].Value.State.GetStateValue(CompositionStateKinds.BeatRank)).IsGreaterThan(0);
                shorter += rendered.Where(x => Math.Abs(x.Position - swing.Apply(items[i].Position)) < 0.05).Any(x => x.Value.Duration < items[i].Value.Duration - 1e-6) ? 1 : 0;
            }
        }

        await Assert.That(muted).IsGreaterThan(100);
        await Assert.That(shorter / (double)muted).IsGreaterThan(0.9);
    }
}
