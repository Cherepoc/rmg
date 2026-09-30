using System.Collections.Immutable;
using Rmg.Core.Probabilities;
using Rmg.Core.Songs;

namespace Rmg.Core.Composition;

/// <summary>How an appearance of a section brings its parts in over its phrases.</summary>
public enum TextureKind
{
    /// <summary>Every part that plays plays throughout, as the section starts.</summary>
    Steady,

    /// <summary>The pitched parts come in phrase by phrase, all of them playing by the last.</summary>
    Build,

    /// <summary>The pitched parts drop out phrase by phrase, one of them left in the last.</summary>
    Thin,

    /// <summary>One part plays the whole appearance, the drums or a pitched part, alone.</summary>
    Alone
}

/// <summary>
///     The texture of an appearance of a section: which of the parts that play play in each of its phrases, its 4-bar
///     patterns, so that a section can build up as an intro does, thin out, or play one instrument alone; by the form
///     facet: the plainest songs play every part through each section, their textures changing only where a section
///     does, the wildest take every texture as likely. The drums play through a phrase or rest the whole appearance, as
///     their fills and landings keep to the sections whose drums play.
/// </summary>
internal static class Texture
{
    /// <summary>How likely each texture is, at the plain end, the middle and the wild end.</summary>
    public static ImmutableArray<(TextureKind Kind, ByConvention Weight)> Kinds { get; } =
    [
        (TextureKind.Steady, new ByConvention(1, 0.82, 1)),
        (TextureKind.Build, new ByConvention(0, 0.1, 1)),
        (TextureKind.Thin, new ByConvention(0, 0.04, 1)),
        (TextureKind.Alone, new ByConvention(0, 0.04, 1))
    ];

    /// <summary>
    ///     An appearance's texture: the parts it leaves out throughout, over those it rests already, and those it leaves
    ///     out in each phrase; a texture that needs more phrases or parts than the appearance has plays steady.
    /// </summary>
    /// <param name="form">The form facet of the section's unconventionality.</param>
    /// <param name="phrases">How many 4-bar patterns the appearance plays.</param>
    /// <param name="playing">The parts the appearance plays, of those the song has.</param>
    public static (TextureKind Kind, ImmutableHashSet<TrackRole> Resting, ImmutableArray<ImmutableHashSet<TrackRole>> PhraseSilent) Draw(
        IGenerationContext context,
        double form,
        int phrases,
        IReadOnlyCollection<TrackRole> playing
    )
    {
        var kind = context.Pick(ByConvention.Weigh(Kinds, form));
        TrackRole[] pitched = [..playing.Where(x => x != TrackRole.Drum).Order()];
        // the order the pitched parts come in, drawn whatever the texture, so that the draws stay as they are
        var order = pitched.OrderBy(_ => context.GenerateDouble()).ToArray();
        var alone = playing.Order().ElementAt(context.GenerateInt(0, Math.Max(1, playing.Count)) % Math.Max(1, playing.Count));
        ImmutableArray<ImmutableHashSet<TrackRole>> none = [..Enumerable.Repeat(ImmutableHashSet<TrackRole>.Empty, phrases)];

        switch (kind)
        {
            case TextureKind.Build or TextureKind.Thin when phrases >= 2 && order.Length >= 2:
            {
                // phrase p plays the first of the order, more every phrase, from one to all
                var silent = Enumerable.Range(0, phrases)
                    .Select(phrase => order.Skip((int)Math.Ceiling((phrase + 1) * order.Length / (double)phrases)).ToImmutableHashSet())
                    .ToArray();
                if (kind == TextureKind.Thin)
                    Array.Reverse(silent);
                return (kind, ImmutableHashSet<TrackRole>.Empty, [..silent]);
            }
            case TextureKind.Alone when playing.Count >= 2:
                return (kind, [..playing.Where(x => x != alone)], none);
            default:
                return (TextureKind.Steady, ImmutableHashSet<TrackRole>.Empty, none);
        }
    }
}
