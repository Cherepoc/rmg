using Rmg.Core.Composition;
using Rmg.Core.Songs;
using Rmg.Tests.Genres;

namespace Rmg.Tests.Arrangements;

/// <summary>The rhythm part playing the riff with it in unison, as a metal band's two guitars do (<see cref="LineDoubling.RhythmChance" />).</summary>
public sealed class RhythmRiffReportTest
{
    /// <summary>
    ///     A genre's sections the rhythm part doubles the riff in, its doubled notes sounding the riff's pitch with it, in unison,
    ///     and the rhythm part's lowest note against the riff's (measured over 64 songs on 26 October 2026: Metal's 220 of 873
    ///     sections, 18095 of 21461 notes in unison, 1472 an octave or two apart, the riff on an instrument it switched to;
    ///     Djent's 155 of 805 and 16458 of 17399; Doom's 73 of 481 and 4068 of 5152).
    /// </summary>
    [Test]
    [Explicit]
    [Arguments("metal")]
    [Arguments("djent")]
    [Arguments("doom")]
    public async Task Report(string genre)
    {
        var (sections, doubling, doubled, unison, octave) = (0, 0, 0, 0, 0);
        var (rhythmLows, riffLows) = (new List<int>(), new List<int>());
        foreach (var song in TestCorpus.Streamed(Enumerable.Range(0, 64), seed => TestCorpus.Get(seed, new SongOverrides(Genre: GenrePresets.Genre(genre)))))
        {
            sections += song.Map.Sections.Length;
            doubling += song.Trace.Count(x => x.Point == TracePoints.LineDoubling && x.Value is (int, TrackRole.Riff, TrackRole.Rhythm, int));
            if (!song.Song.TrackDefinitions.Values.Any(x => x.Role == TrackRole.Rhythm) || !song.Song.TrackDefinitions.Values.Any(x => x.Role == TrackRole.Riff))
                continue;
            var (rhythm, riff) = (song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.Rhythm))?.ToArray() ?? [], song.Song.Notes!.GetValueOrDefault(song.Track(TrackRole.Riff))?.ToArray() ?? []);
            foreach (var note in rhythm.Where(x => x.Value.State.GetStateValue(CompositionStateKinds.Doubled) == (int)TrackRole.Riff + 1))
            {
                doubled++;
                unison += riff.Any(x => Math.Abs(x.Position - note.Position) < 1e-6 && x.Value.Pitches.Min() == note.Value.Pitches.Min()) ? 1 : 0;
                octave += riff.Any(x => Math.Abs(x.Position - note.Position) < 1e-6 && Math.Abs(x.Value.Pitches.Min() - note.Value.Pitches.Min()) % 12 == 0 && x.Value.Pitches.Min() != note.Value.Pitches.Min()) ? 1 : 0;
            }
            if (rhythm.Length > 0 && riff.Length > 0)
                (rhythmLows, riffLows) = ([..rhythmLows, rhythm.Min(x => x.Value.Pitches.Min())], [..riffLows, riff.Min(x => x.Value.Pitches.Min())]);
        }

        Console.WriteLine($"{genre}: {doubling} of {sections} sections doubled by the rhythm part, {unison} of {doubled} doubled notes in unison with the riff, {octave} an octave or two apart; lowest notes, the rhythm part's median {rhythmLows.Order().ElementAtOrDefault(rhythmLows.Count / 2)}, the riff's {riffLows.Order().ElementAtOrDefault(riffLows.Count / 2)}");
        await Task.CompletedTask;
    }
}
