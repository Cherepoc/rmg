using System.Collections.Immutable;
using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>What a drum solo's feet keep time with, where they keep it (<see cref="DrumSolos.Feet" />).</summary>
public enum SoloFeet
{
    /// <summary>The section's kick, as its groove played it.</summary>
    Groove,

    /// <summary>The hi-hat's pedal on 2 and 4, a jazz drummer's.</summary>
    HatFoot,

    /// <summary>A double kick in 16ths, a metal drummer's.</summary>
    DoubleKick,

    /// <summary>The bossa's kick, a dotted quarter and an 8th, twice a bar.</summary>
    Bossa
}

/// <summary>A sticking a drum solo's run or lick plays, its limbs in turn (<see cref="DrumSolos.Stickings" />).</summary>
public enum Sticking
{
    Singles,
    Doubles,
    Paradiddle,
    SixStroke,

    /// <summary>Two hands and the kick, a triplet round the kit.</summary>
    HandFoot,

    /// <summary>Both hands down the toms and the kick, a triplet from the highest tom, a metal drummer's cascade.</summary>
    Cascade
}

/// <summary>
///     A drum solo played as a pitched solo is (<see cref="SoloLines" />), gesture by gesture by the drummer's solo
///     vocabulary (<see cref="Soloist" />), the kit its scale: the section's snare, its toms from the highest to the
///     lowest, and its kick, in that order, so that a run's cells go round the kit as a pitched run's go up the scale, a
///     sequence a drum on at a time, an arpeggio skipping one, a turn between neighbours and a pedal on one drum between
///     the moving others, a gesture of the climax starting on a crash. In most solos the feet keep time, the section's
///     kick under the line, softer, and the kick is then none of the hands' drums. A held note is a crash with the kick,
///     ringing, as is the release every phrase ends on; a pause is the hands silent; a quote is the section's own groove.
///     It leaves its last bar to the fill into the section after, so that the band comes back in on its landing. Every
///     percussion drum is a player of its own, its sounds its scale: the soloists are drawn among the kit's drummer and
///     the players as the pitched solos' are, trading by phrase or together, and the others keep their groove under
///     them, softer, or rest, by the groove facet.
/// </summary>
internal static class DrumSolos
{
    /// <summary>How loud a solo's hits are, the drums' run's loudness at its end, moved by the intensity, and a cell's first the louder.</summary>
    public const double Loudness = FillLayers.RunEndVelocity - 0.1;

    public const double IntensityVelocity = 0.3;

    public const double CellAccent = 0.15;

    /// <summary>How intense a run or a lick is before it starts on a crash, the solo's climax.</summary>
    public const double ClimaxIntensity = 0.8;

    /// <summary>The chance a drum solo stacks the kick under its runs' and licks' accents, as a drummer weighs a grouping.</summary>
    public const double KickUnderAccents = 0.5;

    /// <summary>
    ///     The chance the feet keep time under the kit's solo, the section's kick under the hands' line, which then leave
    ///     the kick out of their kit and stack none under their accents.
    /// </summary>
    public const double FeetKeepTime = 0.7;

    /// <summary>
    ///     The chance a player that does not solo keeps its groove under the solo rather than resting, by the groove
    ///     facet: always at the plain end, never at the wild.
    /// </summary>
    public static ByConvention KeepTime { get; } = new(1, 0.6, 0);

    /// <summary>What the feet keep time with, and how likely: the groove's kick the songs', the others a genre's.</summary>
    public static ImmutableArray<(SoloFeet Feet, ByConvention Weight)> Feet { get; } =
    [
        (SoloFeet.Groove, new ByConvention(1, 1, 1)),
        (SoloFeet.HatFoot, new ByConvention(0, 0, 0)),
        (SoloFeet.DoubleKick, new ByConvention(0, 0, 0)),
        (SoloFeet.Bossa, new ByConvention(0, 0, 0))
    ];

    /// <summary>A feet's pattern, its 16ths in a bar of four beats; the groove's its own.</summary>
    private static int[] OnsetsOf(SoloFeet feet) => feet switch
    {
        SoloFeet.HatFoot => [4, 12],
        SoloFeet.DoubleKick => [..Enumerable.Range(0, 16)],
        SoloFeet.Bossa => [0, 6, 8, 14],
        _ => []
    };

    /// <summary>
    ///     The stickings the kit's runs and licks play, as hands play them rather than a scale: the right hand round the
    ///     toms, the left on the snare, the foot on the kick, each sticking's first stroke the accent; and how likely each
    ///     is, the plainest the singles and doubles, the wilder the six-stroke roll and the hands with the foot.
    /// </summary>
    public static ImmutableArray<(Sticking Sticking, ByConvention Weight)> Stickings { get; } =
    [
        (Sticking.Singles, new ByConvention(1, 1, 1)),
        (Sticking.Doubles, new ByConvention(0.4, 0.5, 0.6)),
        (Sticking.Paradiddle, new ByConvention(0.2, 0.4, 0.6)),
        (Sticking.SixStroke, new ByConvention(0, 0.2, 0.5)),
        (Sticking.HandFoot, new ByConvention(0, 0.3, 0.6)),
        (Sticking.Cascade, new ByConvention(0, 0.1, 0.3))
    ];

    /// <summary>A sticking's limbs, R and L the hands, R round the toms and L on the snare, T the left on the toms, K the foot.</summary>
    public static string LimbsOf(Sticking sticking) => sticking switch
    {
        Sticking.Cascade => "RTK",
        Sticking.Doubles => "RRLL",
        Sticking.Paradiddle => "RLRRLRLL",
        Sticking.SixStroke => "RLLRRL",
        Sticking.HandFoot => "RLK",
        _ => "RL"
    };

    /// <summary>
    ///     The chance a phrase's first run or lick of the kit's restates the phrase before's, its motif, the same sticking at
    ///     the same speed, moved a drum round the kit; and the chance it is displaced by a 16th besides.
    /// </summary>
    public const double MotifChance = SoloLines.MotifChance, DisplaceChance = 0.3;

    /// <summary>How loud the drums keeping time under a solo are, softer than its calmest.</summary>
    public const double TimeLoudness = Loudness - IntensityVelocity / 2 - 0.05;

    /// <summary>
    ///     A drum solo's player: the kit's drummer, its hands' scale the kit's drums, or a percussion drum's player, its
    ///     scale the drum's sounds from the highest; the tracks it plays.
    /// </summary>
    private sealed class Player(RunSound[] scale, IReadOnlySet<int> tracks, bool isKit)
    {
        public RunSound[] Scale { get; } = scale;

        public IReadOnlySet<int> Tracks { get; } = tracks;

        public bool IsKit { get; } = isKit;

        // where its line is on its scale, and the cells it goes on with
        public int At { get; set; }

        public SoloLines.CellCursor? Cursor { get; set; }

        // its last phrase's first run or lick, its motif: the phrase, the gesture, the speed, the sticking and the drum it started on
        public (int Phrase, SoloGesture Gesture, int Speed, Sticking Sticking, int At)? Motif { get; set; }
    }

    /// <summary>The drum solos, the drummer's own, over the groove they quote, a stage of the song after it is put together (<see cref="SongStages" />).</summary>
    internal static SongDraft Apply(SongDecisions song, SongDraft draft)
    {
        var holds = draft.Holds(song);
        return draft with
        {
            Timeline = Play(
                draft.Timeline,
                song.Tracks.SongDrums,
                song.Form.Map,
                song.Solos,
                song.Soloists.GetValueOrDefault(TrackRole.Drum),
                index => song.AppearanceStream(SongStream.DrumSolo, index),
                index => song.AppearanceStream(SongStream.DrumSoloStacks, index),
                index => song.AppearanceStream(SongStream.DrumSoloFeet, index),
                index => song.AppearanceStream(SongStream.DrumSoloPlayers, index),
                index => song.AppearanceStream(SongStream.DrumSoloOstinato, index),
                index => song.AppearanceStream(SongStream.DrumSoloStickings, index),
                index => song.AppearanceStream(SongStream.DrumSoloMotifs, index),
                index => song.SectionOf(song.Played[index]).Facets[Facet.Groove],
                song.Form.Drops,
                // where the form next stops the drums, which a drum solo keeps out of as every drum stage does
                position => song.Tracks.Definitions.Where(x => x.Value is PercussionInstrumentTrack).Select(x => holds.NextAfter(Hold.Cut, position, x.Key)).DefaultIfEmpty(double.PositiveInfinity).Min(),
                song.Genre,
                song.Meter,
                DrumSounds.OddsOf(song.Genre, GenreChoices.DrumSounds)
            )
        };
    }

    /// <summary>
    ///     The song with every drum solo played, each from a sequence of its own by its appearance's place; the drums that
    ///     play in the section are its kit.
    /// </summary>
    /// <param name="drummer">The drummer's solo vocabulary; none where the song has no drum solo.</param>
    /// <param name="stacks">A solo's draw of whether it stacks the kick under its accents, by its appearance's place.</param>
    /// <param name="feet">A solo's draw of whether the feet keep time under it, by its appearance's place.</param>
    /// <param name="players">A solo's draw of its soloists among the players and of whether the others keep time, by its appearance's place.</param>
    /// <param name="ostinatos">A solo's draw of what its feet keep time with, by its appearance's place.</param>
    /// <param name="stickings">A solo's draws of its runs' and licks' stickings, by its appearance's place.</param>
    /// <param name="motifs">A solo's draws of which phrases restate the phrase before's motif, by its appearance's place.</param>
    /// <param name="genre">The genre, whose pools of the feet's patterns and the stickings are the solo's where it names them.</param>
    /// <param name="groove">An appearance's groove facet, by its place, which leans how many solo and whether the others keep time.</param>
    /// <param name="drops">Where the song drops, whose build a solo before it leaves to roll.</param>
    /// <param name="stopsAfter">Where the form next stops the drums after a position, a stop ending's silence, which a solo keeps out of.</param>
    private static TrackEventStateTimelineMap<StateMap> Play(
        TrackEventStateTimelineMap<StateMap> song,
        IEnumerable<PercussionInstrumentDefinition> songDrums,
        SongMap map,
        IReadOnlyDictionary<int, SoloPlan> solos,
        Soloist? drummer,
        Func<int, IGenerationContext> streams,
        Func<int, IGenerationContext> stacks,
        Func<int, IGenerationContext> feet,
        Func<int, IGenerationContext> players,
        Func<int, IGenerationContext> ostinatos,
        Func<int, IGenerationContext> stickings,
        Func<int, IGenerationContext> motifs,
        Func<int, double> groove,
        IReadOnlyCollection<double> drops,
        Func<double, double> stopsAfter,
        Genre genre,
        Meter meter,
        Func<int, double> odds
    )
    {
        if (drummer is null)
            return song;

        var sounds = new FillSounds(songDrums, odds).Sounds;
        foreach (var (index, solo) in solos.Where(x => x.Value.IsDrumSolo || x.Value.TradesWithDrums).OrderBy(x => x.Key))
        {
            var whole = map.Sections[index];
            // a solo traded with the drums their turns alone, the band laying out for them
            var drumsTurn = solo.Soloists.IndexOf(TrackRole.Drum);
            bool IsDrumsTurn(double position) => !solo.TradesWithDrums || solo.TurnAt(whole, position) == drumsTurn;
            // all but its last bar, a short one's, the fill's into the section after, and the bars a build rolls over into a
            // drop after it
            // nor where the form stops the drums, a stop ending's silence
            var until = Math.Min(drops.Any(x => Math.Abs(x - whole.End) < 1e-9) ? whole.LastBar - FormLayers.BuildRollBars * whole.Meter.BarDuration : whole.LastBar,
                stopsAfter(whole.Start));
            var span = whole with { Duration = Math.Max(whole.Meter.BarDuration, until - whole.Start), Cut = 0 };
            bool Plays(RunSound sound) => song.TrackTimelineMap.TryGetValue(sound.Track, out var track) &&
                                          track.EventTimeline.Any(x => x.IsWithin(span.Start, span.End));
            RunSound[] Of(FillDrumRole role) => sounds.TryGetValue(role, out var all) ? [..all.Where(Plays).DistinctBy(x => x.Track)] : [];
            // the song's toms where the section's kit plays, its snare or its kick, every one of them by its note, as the toms are
            // one drum of several sounds, whether the groove there struck them or not; none in a section of percussion alone
            var kitPlays = Of(FillDrumRole.Snare).Length > 0 || Of(FillDrumRole.Kick).Length > 0;
            RunSound[] kitToms = kitPlays && sounds.TryGetValue(FillDrumRole.Toms, out var tomSounds)
                ? [..tomSounds.Where(x => song.TrackTimelineMap.ContainsKey(x.Track)).DistinctBy(x => x.Code)]
                : [];
            // the hands' kit from its highest to its lowest: the snare, the toms by their notes, and the kick unless the
            // feet keep time on it
            RunSound[] hands = [..Of(FillDrumRole.Snare).Take(1), ..kitToms.OrderByDescending(x => x.Code)];
            var kicks = Of(FillDrumRole.Kick);
            var kick = kicks.FirstOrDefault();
            var feetKeepTime = feet(index).TestProbability(FeetKeepTime) && kick is not null && hands.Length > 0;
            // and what they keep it with, a genre's pattern in a bar of four, the hi-hat's pedal where the song has the hi-hat
            var hat = DrumGroups.GetTrackNumber(DrumDefinitions.HiHat);
            var feetPattern = ostinatos(index).Pick(ByConvention.Weigh(Feet, groove(index), genre.Pool<SoloFeet>(GenreChoices.DrumSoloFeet)));
            if (whole.Meter.Sixteenths != 16 || feetPattern == SoloFeet.HatFoot && !song.TrackTimelineMap.ContainsKey(hat))
                feetPattern = SoloFeet.Groove;
            var tracks = song.TrackTimelineMap.Keys.Where(x => x >= DrumGroups.FirstTrackNumber).ToArray();
            // every percussion drum a player of its own, its sounds from the highest, and the kit's drummer playing the rest
            var percussion = sounds.TryGetValue(FillDrumRole.Percussion, out var all) ? all.Where(Plays).GroupBy(x => x.Track)
                // a hand drum's sounds from its high tone to its low, which its number runs up, as a pair's high drum is the lower
                .Select(x => new Player([..x.DistinctBy(y => y.Articulation).OrderBy(y => y.Code)], new HashSet<int> { x.Key }, false)).ToArray() : [];
            var kit = new Player(feetKeepTime ? hands : [..hands, ..kicks.Take(1)], tracks.Except(percussion.SelectMany(x => x.Tracks)).ToHashSet(), true);
            Player[] band = [kit, ..percussion];

            // the soloists, as many as the pitched solos have by the groove facet, among the players with a scale of two
            // sounds or more, or the kit's alone; the others keeping time or resting
            var draws = players(index);
            var facet = groove(index);
            var count = draws.Pick(ByConvention.Weigh(Solos.Counts, facet));
            var mode = draws.Pick(ByConvention.Weigh(Solos.Modes, facet));
            var candidates = band.Where(x => x.Scale.Length >= 2).ToList();
            if (candidates.Count == 0)
                candidates = kit.Scale.Length > 0 ? [kit] : [];
            var soloists = new List<Player>();
            while (soloists.Count < count && candidates.Count > 0)
            {
                var soloist = candidates[draws.GenerateInt(0, candidates.Count)];
                soloists.Add(soloist);
                candidates.Remove(soloist);
            }

            var keepers = band.Except(soloists).Where(_ => draws.TestProbability(KeepTime.At(facet))).ToHashSet();
            if (soloists.Count == 0)
                continue;

            // the song's crash, whether its groove there played it or not
            var crash = sounds.TryGetValue(FillDrumRole.Cymbal, out var cymbals) ? cymbals.FirstOrDefault() : null;
            var context = streams(index);
            var kitSolos = soloists.Contains(kit);
            var stacksKick = stacks(index).TestProbability(KickUnderAccents) && kick is not null && !feetKeepTime;
            // the feet keep time while the kit solos, its turns and the others'
            var feetTracks = !kitSolos || !feetKeepTime ? []
                : feetPattern == SoloFeet.HatFoot ? new HashSet<int> { hat }
                : kicks.Select(x => x.Track).ToHashSet();
            var sticks = stickings(index);
            var motifDraws = motifs(index);
            var answered = new List<int>();
            var stickingWeights = ByConvention.Weigh(Stickings, groove(index), genre.Pool<Sticking>(GenreChoices.Stickings));
            var common = song.CommonStateTimelineMap;
            var plan = SoloLines.Plan(
                context,
                span,
                drummer,
                // the groove is there to quote throughout
                (_, _) => 1,
                position => Meter.BeatsPerSecond(common.GetStateTimeline(StateKinds.Tempo).GetEffectiveValueAt(position)),
                solo.TradesWithDrums ? SoloPlan.TurnOf(whole) : span.Duration,
                span.Start
            );
            var turn = SoloPlan.TurnOf(whole);
            // its segments in the drums' turns, a segment across a turn's end cut there
            var played = solo.TradesWithDrums
                ? plan.SelectMany(x => Enumerable.Range(0, (int)Math.Ceiling((x.End - whole.Start) / turn))
                        .Select(k => x with { Start = Math.Max(x.Start, whole.Start + k * turn), End = Math.Min(x.End, whole.Start + (k + 1) * turn) })
                        .Where(y => y.End > y.Start + 1e-9 && IsDrumsTurn(y.Start)))
                    .ToArray()
                : plan;
            // a trade whose drums' turns fall in the bar the fill plays alone leaves them none
            if (played.Length == 0)
                continue;
            StateTrace.RecordSection(TracePoints.DrumSolo, FillGenerator.DrumsTrace, whole.SectionId,
                $"{played.Length} segments, {soloists.Count} soloists {mode}{(feetTracks.Count > 0 ? ", the feet keeping time" : "")}",
                new DrumSolo(index, played, feetTracks.Count > 0, [..soloists.Select(x => Array.IndexOf(band, x))], mode, [..keepers.Select(x => Array.IndexOf(band, x))], [..band.Select(x => x.Tracks.Order().ToArray())], answered));

            var edits = new TimelineEdits(context, meter, map);
            if (solo.TradesWithDrums)
            {
                for (var at = whole.Start; at < whole.End - 1e-9; at += turn)
                    if (IsDrumsTurn(at))
                        foreach (var track in song.TrackTimelineMap.Keys.Where(x => x < DrumGroups.FirstTrackNumber))
                            edits.Clear(track, at, Math.Min(whole.End, at + turn));
            }

            foreach (var (start, end, level, gesture, speed, shape, sizes, goesOn, phrase, _) in played)
            {
                // the groove quoted as it plays, and anything else in place of it
                if (gesture == SoloGesture.Quote)
                    continue;
                bool IsIn(TimelineItem<StateMap> note) => note.IsWithin(start, end);
                // every drum cleared but the feet's where they replay the groove's kick
                foreach (var track in tracks.Where(x => !feetTracks.Contains(x) || feetPattern != SoloFeet.Groove))
                    edits.Clear(track, start, end);
                // the feet and the players keeping time, softer, their accents kept under it as the section's dynamics scale
                // them, the strongest at the time's loudness, the feet by their pattern where it is not the groove's
                foreach (var track in (feetPattern == SoloFeet.Groove ? feetTracks : []).Concat(keepers.SelectMany(x => x.Tracks)))
                foreach (var note in song.TrackTimelineMap[track].EventTimeline.Where(IsIn))
                {
                    var dynamics = song.CommonStateTimelineMap.GetEffectiveStateMapAt(note.Position)
                        .MergeWith(song.TrackTimelineMap[track].StateTimelineMap.GetEffectiveStateMapAt(note.Position)).GetStateValue(CompositionStateKinds.NoteDynamics);
                    edits.Replay(track, note, note.Value.GetStateValue(StateKinds.Velocity) - VelocityLayers.Note * dynamics * BeatAccent.StrongestAccent + TimeLoudness);
                }
                if (feetPattern != SoloFeet.Groove && feetTracks.Count > 0)
                {
                    var (footTrack, footSound) = feetPattern == SoloFeet.HatFoot
                        ? (hat, DrumDefinitions.HiHat.GetArticulationIndex(DrumSounds.PedalHiHat))
                        : (kick!.Track, kick.Articulation);
                    var bar = whole.Meter.BarDuration;
                    for (var at = whole.Start + Math.Floor((start - whole.Start) / bar + 1e-9) * bar; at < end - 1e-9; at += bar)
                    foreach (var onset in OnsetsOf(feetPattern).Select(x => at + x * 0.25).Where(x => x >= start - 1e-9 && x < end - 1e-9))
                        edits.Hit(footTrack, onset, TimeLoudness, footSound, whole.SectionId, HitKind.DrumSolo);
                }

                var loudness = Loudness + IntensityVelocity * (level - 0.5);
                // the soloists in turn by phrase, or together
                foreach (var player in mode == SoloMode.Trading ? [soloists[phrase % soloists.Count]] : soloists)
                {
                    var scale = player.Scale;
                    switch (gesture)
                    {
                        case SoloGesture.Held:
                            if (player.IsKit && crash is not null)
                                edits.Hit(crash.Track, start, loudness + CellAccent, crash.Articulation, whole.SectionId, HitKind.DrumSolo);
                            var low = player.IsKit ? kick ?? scale[^1] : scale[^1];
                            edits.Hit(low.Track, start, loudness + CellAccent, low.Articulation, whole.SectionId, HitKind.DrumSolo);
                            break;
                        case SoloGesture.Run or SoloGesture.Lick when player.IsKit:
                        {
                            // a sticking, the right hand round the toms, the left on the snare, the foot on the kick, but where the
                            // feet keep time on it; a section without a kick plays the foot's strokes as rests
                            var kickFree = kick is null || !feetTracks.Contains(kick.Track);
                            // the hands' own where the feet keep time and a genre's pool names those with the foot alone
                            ImmutableArray<Weighted<Sticking>> handsOnly = [..stickingWeights.Where(x => !LimbsOf(x.Value).Contains('K'))];
                            var sticking = sticks.Pick(kickFree ? stickingWeights : handsOnly.IsEmpty ? [new Weighted<Sticking>(1, Sticking.Singles)] : handsOnly);
                            var snare = scale[0];
                            RunSound[] toms = [..scale.Skip(1).Where(x => kick is null || x.Track != kick.Track)];
                            var at = Math.Clamp(player.At, 0, Math.Max(0, toms.Length - 1));
                            var (rhythm, speedOf) = (gesture, speed);
                            var displaced = false;
                            // a phrase's first restating the phrase before's motif now and then, a drum round the kit, displaced
                            // by a 16th now and then, its draws made whatever it plays
                            if (player.Motif?.Phrase != phrase)
                            {
                                var (answers, displaces) = (motifDraws.TestProbability(MotifChance), motifDraws.TestProbability(DisplaceChance));
                                if (answers && player.Motif is { } motif && (!LimbsOf(motif.Sticking).Contains('K') || kickFree))
                                {
                                    (rhythm, speedOf, sticking, displaced) = (motif.Gesture, motif.Speed, motif.Sticking, displaces);
                                    at = toms.Length > 0 ? (motif.At + 1) % toms.Length : 0;
                                    answered.Add(phrase);
                                }

                                player.Motif = (phrase, rhythm, speedOf, sticking, at);
                            }

                            // a cascade from the highest tom down
                            if (sticking == Sticking.Cascade)
                                at = 0;

                            var limbs = LimbsOf(sticking);
                            var positions = SoloLines.Positions(start, end, rhythm, speedOf).Select(x => x + (displaced ? 0.25 : 0)).Where(x => x < end - 1e-9).ToArray();
                            // a run walks the toms to their other end, a lick rocks between two of them
                            var step = at < toms.Length / 2 ? 1 : -1;
                            var rights = 0;
                            for (var i = 0; i < positions.Length; i++)
                            {
                                var limb = limbs[i % limbs.Length];
                                RunSound sound;
                                if (limb == 'K' && kick is null)
                                    continue;
                                if (limb == 'K')
                                    sound = kick!;
                                else if (limb is 'R' or 'T' && toms.Length > 0)
                                {
                                    sound = toms[gesture == SoloGesture.Lick ? Math.Clamp(at + rights % 2, 0, toms.Length - 1) : at];
                                    if (gesture == SoloGesture.Run)
                                    {
                                        if (at + step < 0 || at + step >= toms.Length)
                                            step = -step;
                                        at = Math.Clamp(at + step, 0, toms.Length - 1);
                                    }

                                    rights++;
                                }
                                else
                                    sound = snare;
                                var accent = i % limbs.Length == 0;
                                edits.Hit(sound.Track, positions[i], loudness + (accent ? CellAccent : 0), sound.Articulation, whole.SectionId, HitKind.DrumSolo);
                                // a climax's gesture starting on a crash
                                if (i == 0 && level >= ClimaxIntensity && crash is not null)
                                    edits.Hit(crash.Track, positions[i], loudness + CellAccent, crash.Articulation, whole.SectionId, HitKind.DrumSolo);
                                // and the kick under an accent, but where the accent is the kick's own
                                if (stacksKick && accent && sound.Track != kick!.Track)
                                    edits.Hit(kick.Track, positions[i], loudness + CellAccent, kick.Articulation, whole.SectionId, HitKind.DrumSolo);
                            }

                            player.At = at;
                            break;
                        }
                        case SoloGesture.Run:
                        case SoloGesture.Lick:
                        {
                            // round the scale to its other end, the way it heads the more intense the lower
                            player.Cursor = goesOn && player.Cursor is not null ? player.Cursor : new SoloLines.CellCursor(player.At);
                            var heading = player.At < scale.Length / 2 ? scale.Length - 1 : 0;
                            var positions = SoloLines.Positions(start, end, gesture, speed);
                            var (hits, accents, _) = SoloLines.Cells([..Enumerable.Range(0, scale.Length)], player.Cursor, heading, positions.Length, shape, sizes, gesture == SoloGesture.Run);
                            for (var i = 0; i < hits.Length; i++)
                                edits.Hit(scale[hits[i]].Track, positions[i], loudness + (accents[i] ? CellAccent : 0), scale[hits[i]].Articulation, whole.SectionId, HitKind.DrumSolo);
                            if (hits.Length > 0)
                                player.At = hits[^1];
                            break;
                        }
                    }
                }
            }

            song = edits.ApplyTo(song);
        }

        return song;
    }
}

/// <summary>A drum solo as the trace records it: its appearance's place, its gestures, whether the feet keep time, its
/// soloists and how they share it, the players keeping time, by their places among the players, and every player's
/// tracks, the kit's drummer first.</summary>
/// <param name="Answered">The phrases whose first run or lick restates the phrase before's motif.</param>
internal sealed record DrumSolo(int Index, SoloLines.Segment[] Plan, bool Feet, int[] Soloists, SoloMode Mode, int[] Keepers, int[][] Tracks, IReadOnlyList<int> Answered);
