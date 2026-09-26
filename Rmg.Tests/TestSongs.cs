using System.Globalization;
using System.Text.RegularExpressions;
using Rmg.Core.Composition;
using Rmg.Core.Events;
using Rmg.Core.Songs;

namespace Rmg.Tests;

/// <summary>Songs as the tests look at them: where their first section starts, after any bars of an intro.</summary>
internal static class TestSongs
{
    public static (Song Song, double Origin) Generate(int seed)
    {
        using var trace = StateTrace.Start();
        var song = SongGenerator.GenerateSong(seed);
        var intro = trace.Entries.Single(x => x.Point == "Song intro").Phrase!;
        var origin = double.Parse(Regex.Match(intro, @", ([\d.]+) beats").Groups[1].Value, CultureInfo.InvariantCulture);
        return (song, origin);
    }
}
