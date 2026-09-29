using Rmg.Core.Events;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>
///     A note of a drum that strikes playing another of its sounds in place of its stroke, as an accent, such as the
///     open hi-hat off the beat or the ride's bell on it: the note's own stroke, the lowest layer's
///     (<see cref="StateKinds.DrumStroke" />). Its chance is the sound's, leaned to the beats it favours, by how strong
///     the note's beat is, and by the section's energy, as far as the sound is louder than its drum; a note of a
///     repeated cycle plays the accent of the note it repeats, so that an accent in a figure comes back with it.
/// </summary>
internal static class DrumAccents
{
    /// <summary>The chance of an open hi-hat on a note, with no lean.</summary>
    public const double OpenHiHat = 0.025;

    /// <summary>The chance of the ride's bell on a note, with no lean.</summary>
    public const double RideBell = 0.03;

    /// <summary>The chance of a hand drum's other tone on a note, such as the conga's low one, with no lean.</summary>
    public const double HandDrum = 0.06;

    /// <summary>The chance of the conga's muted tone on a note, with no lean.</summary>
    public const double MutedConga = 0.04;

    /// <summary>The odds by which an accent leans to the beats it favours, and away from the others.</summary>
    public const double BeatOdds = 4;

    /// <summary>
    ///     A note's accent, the first of the drum's accent sounds it draws, as its stroke at a note's depth; none for a
    ///     note that plays the stroke. One draw for every accent sound.
    /// </summary>
    /// <param name="rank">How strong the note's beat is, 0 the strongest.</param>
    /// <param name="energy">The section's pull of its energy, which leans the louder accents.</param>
    public static StateMap Draw(IGenerationContext context, IReadOnlyList<DrumSound> sounds, int rank, Tilt energy)
    {
        StateMap accent = StateMap.Default;
        for (var i = 0; i < sounds.Count; i++)
        {
            var sound = sounds[i];
            if (sound.Accent <= 0)
                continue;

            // a weak beat leans to an accent that favours the weak ones, and the strongest to one that favours it
            var chance = Tilt.Of(BeatOdds, sound.AccentLean * (rank > 0 ? 1 : -1)).Chance(sound.Accent, 1);
            if (context.TestProbability(energy.Chance(chance, sound.Loudness)) && accent.IsDefault)
                accent = DrumStrokes.At(StateDepths.Note, i);
        }

        return accent;
    }
}
