using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A drum solo played as a pitched solo is (<see cref="SoloLines" />), gesture by gesture by the drummer's solo
///     vocabulary (<see cref="Soloist" />), the kit its scale: the section's snare, its toms from the highest to the
///     lowest, and its kick, in that order, so that a run's cells go round the kit as a pitched run's go up the scale, a
///     sequence a drum on at a time, an arpeggio skipping one, a turn between neighbours and a pedal on one drum between
///     the moving others. A held note is a crash with the kick, ringing, as is the release every phrase ends on; a pause is
///     the drums silent; a quote is the section's own groove. It leaves its last bar to the fill into the section after, so that the band comes back in on
///     its landing.
/// </summary>
internal static class DrumSolos
{
    /// <summary>How loud a solo's hits are, the drums' run's loudness at its end, moved by the intensity, and a cell's first the louder.</summary>
    public const double Loudness = FillLayers.RunEndVelocity - 0.1;

    public const double IntensityVelocity = 0.3;

    public const double CellAccent = 0.15;

    /// <summary>
    ///     The song with every drum solo played, each from a sequence of its own by its appearance's place; the drums that
    ///     play in the section are its kit.
    /// </summary>
    /// <param name="drummer">The drummer's solo vocabulary; none where the song has no drum solo.</param>
    public static TrackEventStateTimelineMap<StateMap> Apply(
        TrackEventStateTimelineMap<StateMap> song,
        IEnumerable<PercussionInstrumentDefinition> songDrums,
        SongMap map,
        IReadOnlyDictionary<int, SoloPlan> solos,
        Soloist? drummer,
        Func<int, IGenerationContext> streams,
        Meter meter
    )
    {
        if (drummer is null)
            return song;

        var sounds = new FillSounds(songDrums).Sounds;
        foreach (var (index, _) in solos.Where(x => x.Value.IsDrumSolo).OrderBy(x => x.Key))
        {
            var whole = map.Sections[index];
            // all but its last bar, the fill's into the section after
            var span = whole with { Duration = Math.Max(whole.Meter.BarDuration, whole.Duration - whole.Meter.BarDuration) };
            bool Plays(RunSound sound) => song.TrackTimelineMap.TryGetValue(sound.Track, out var track) &&
                                          track.EventTimeline.Any(x => x.Position >= span.Start - 1e-9 && x.Position < span.End - 1e-9);
            RunSound[] Of(FillDrumRole role) => sounds.TryGetValue(role, out var all) ? [..all.Where(Plays).DistinctBy(x => x.Track)] : [];
            // the kit from its highest to its lowest: the snare, the toms by their notes, the kick
            RunSound[] kit = [..Of(FillDrumRole.Snare).Take(1), ..Of(FillDrumRole.Toms).OrderByDescending(x => x.Code), ..Of(FillDrumRole.Kick).Take(1)];
            if (kit.Length == 0)
                continue;

            var crash = Of(FillDrumRole.Cymbal).FirstOrDefault();
            var tracks = song.TrackTimelineMap.Keys.Where(x => x >= DrumGroups.FirstTrackNumber).ToArray();
            var context = streams(index);
            var common = song.CommonStateTimelineMap;
            var plan = SoloLines.Plan(
                context,
                span,
                drummer,
                // the groove is there to quote throughout
                (_, _) => 1,
                position => common.GetEffectiveStateMapAt(position).GetStateValue(StateKinds.Tempo) * Meter.BaseTempo / 60
            );
            StateTrace.Record(TracePoints.DrumSolo, FillGenerator.DrumsTrace, whole.SectionId, 0, StateMap.Default, 0, $"{plan.Length} segments", (index, plan));

            var edits = new TimelineEdits(context, meter, map);
            var indices = Enumerable.Range(0, kit.Length).ToArray();
            var at = 0;
            SoloLines.CellCursor? cursor = null;
            foreach (var (start, end, level, gesture, speed, shape, sizes, goesOn, _, _) in plan)
            {
                // the groove quoted as it plays, and anything else in place of it
                if (gesture == SoloGesture.Quote)
                    continue;
                foreach (var track in tracks)
                    edits.Clear(track, start, end);

                var loudness = Loudness + IntensityVelocity * (level - 0.5);
                switch (gesture)
                {
                    case SoloGesture.Held:
                        if (crash is not null)
                            edits.Hit(crash.Track, start, loudness + CellAccent, crash.Articulation, whole.SectionId, "Drum solo");
                        edits.Hit(kit[^1].Track, start, loudness + CellAccent, kit[^1].Articulation, whole.SectionId, "Drum solo");
                        break;
                    case SoloGesture.Run:
                    case SoloGesture.Lick:
                    {
                        // round the kit to its other end, the way it heads the more intense the lower
                        cursor = goesOn && cursor is not null ? cursor : new SoloLines.CellCursor(at);
                        var heading = at < kit.Length / 2 ? kit.Length - 1 : 0;
                        var positions = SoloLines.Positions(start, end, gesture, speed);
                        var (hits, accents, _) = SoloLines.Cells(indices, cursor, heading, positions.Length, shape, sizes, gesture == SoloGesture.Run);
                        for (var i = 0; i < hits.Length; i++)
                            edits.Hit(kit[hits[i]].Track, positions[i], loudness + (accents[i] ? CellAccent : 0), kit[hits[i]].Articulation, whole.SectionId, "Drum solo");
                        if (hits.Length > 0)
                            at = hits[^1];
                        break;
                    }
                }
            }

            song = edits.ApplyTo(song);
        }

        return song;
    }
}
