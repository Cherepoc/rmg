using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>Where a song's whole mix is filtered: whether it opens up, the sections it dips before, by their starts, and whether it closes.</summary>
public sealed record MixFilterPlan(bool Opening, ImmutableArray<double> Dips, bool Closing, ImmutableArray<double> Drops);

/// <summary>
///     The whole mix filtered, every part's brightness and the drums' closed together, a form device: the song opening up
///     from muffled over its intro, or its first phrase where it has none; a section's last bar or two closing, the
///     likelier the louder the next, and opening at once as it starts; and the last section's last phrase closing as the
///     song ends. Each by the form facet and a genre's chances, the songs' seldom and no oftener in a wild song than a
///     middling one; its depth drawn. A stage of the song after it is put together (<see cref="SongStages" />), as a
///     timeline of how closed the mix is, stepped as a fade is (<see cref="CompositionStateKinds.MixClosed" />).
/// </summary>
internal static class MixFilter
{
    public static ByConvention OpeningChance { get; } = new(0, 0.05, 0.05);

    /// <summary>The chance a section's end dips, before the next section, at an energy as high as its own.</summary>
    public static ByConvention DipChance { get; } = new(0, 0.008, 0.008);

    /// <summary>The odds a dip's chance is leaned by, to the power of how much louder the next section is than its own.</summary>
    private const double DipOdds = 4;

    public static ByConvention ClosingChance { get; } = new(0, 0.04, 0.04);

    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var map = song.Form.Map;
        if (map.Sections.IsEmpty)
            return draft;
        var stream = song.Stream(SongStream.MixFilter);
        double Chance(ByConvention own, string option) => own.At(song.Facets[Facet.Form], song.Genre.Chance(GenreChoices.MixFilter, option));
        // how closed the mix is over a span, from a share to another, as far as its depth takes it
        var ramps = new List<(double From, double To, double Start, double End)>();

        var opens = stream.TestProbability(Chance(OpeningChance, "opening"));
        var openingDepth = 0.6 + 0.4 * stream.GenerateDouble();
        if (opens)
        {
            var first = map.Sections[0];
            // over the intro, or the first phrase where there is none or only a count-in's clicks
            var overIntro = map.Intro.Duration > 0 && map.Intro.Kind != IntroKind.CountIn;
            var end = overIntro ? map.Origin : first.Start + Math.Min(first.Duration, first.Meter.PhraseDuration);
            ramps.Add((overIntro ? 0 : first.Start, end, openingDepth, 0));
        }

        // a section's end, but where the band falls silent for a drop, and the last's, each from a sequence of its own by the
        // section ending there and its appearance
        var dips = ImmutableArray.CreateBuilder<double>();
        for (var i = 0; i + 1 < map.Sections.Length; i++)
        {
            var (span, next) = (map.Sections[i], map.Sections[i + 1]);
            var dipStream = song.Stream(SongStream.MixFilter, span.SectionId, map.Sections.Take(i).Count(x => x.SectionId == span.SectionId));
            var dipsHere = dipStream.TestProbability(Tilt.Of(DipOdds, song.Energies[i + 1] - song.Energies[i]).Chance(Chance(DipChance, "dips"), 1));
            var bars = 1 + dipStream.GenerateInt(0, 2);
            var depth = 0.5 + 0.5 * dipStream.GenerateDouble();
            if (!dipsHere || song.Form.Drops.Contains(next.Start))
                continue;
            dips.Add(next.Start);
            ramps.Add((next.Start - Math.Min(span.Duration, bars * span.Meter.BarDuration), next.Start, 0, depth));
        }

        // a filtered drop's bar before it closing all the way, opening on its downbeat (FormLayers.DropKinds)
        foreach (var drop in song.Form.FilteredDrops)
            ramps.Add((drop - song.Form.Map.MeterAt(drop - 1e-6).BarDuration, drop, 0, 1));

        var closes = stream.TestProbability(Chance(ClosingChance, "closing"));
        var closingDepth = 0.6 + 0.4 * stream.GenerateDouble();
        if (closes)
        {
            var last = map.Sections[^1];
            ramps.Add((last.End - Math.Min(last.Duration, last.Meter.PhraseDuration), last.End, 0, closingDepth));
            // and closed through the ending
            ramps.Add((last.End, map.Duration, closingDepth, closingDepth));
        }

        var plan = new MixFilterPlan(opens, dips.ToImmutable(), closes, song.Form.FilteredDrops);
        StateTrace.RecordSong(TracePoints.MixFilter, $"{plan}", plan);
        if (ramps.Count == 0)
            return draft;

        // stepped as a fade is, every step the sum of the ramps over it, where it changes, a ramp's first step at its start
        // and its last at its end
        var duration = draft.Timeline.Duration;
        var items = new List<TimelineItem<double>>();
        var closed = 0.0;
        for (var at = 0.0; at < duration - 1e-9; at += FormLayers.FadeStep)
        {
            var value = Math.Min(1, ramps.Where(x => at >= x.From - 1e-9 && at < x.To - 1e-9).Sum(x => x.Start + (x.End - x.Start) * Math.Min(1, (at - x.From) / Math.Max(FormLayers.FadeStep, x.To - x.From - FormLayers.FadeStep))));
            value = Math.Round(value, 3);
            if (value != closed)
                items.Add(value.ToTimelineItem(at));
            closed = value;
        }

        var mix = StateTimeline.Create(duration, CompositionStateKinds.MixClosed, items);
        var common = StateTimelineMap.Merge([draft.Timeline.CommonStateTimelineMap, StateTimelineMap.Create(duration, [mix])]);
        return draft with { Timeline = TrackEventStateTimelineMap.Create(duration, draft.Timeline.TrackTimelineMap, common) };
    }
}
