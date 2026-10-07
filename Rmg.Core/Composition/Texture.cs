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
///     does, the wildest take every texture as likely; those of fewer parts leaned to the quieter sections, as a band
///     thins out in a verse rather than in a chorus. The drums play through a phrase or rest the whole appearance, but
///     where a section builds, which brings them in at a phrase now and then with a fill, as a band builds; and they play
///     alone, or first in a build, only where they keep time, never where they only mark the form.
/// </summary>
internal static class Texture
{
    /// <summary>
    ///     How likely each texture is, at the plain end, the middle and the wild end, and how it leans by the section's
    ///     energy: those of fewer parts against it.
    /// </summary>
    public static ImmutableArray<(TextureKind Kind, ByConvention Weight, double EnergyLean)> Kinds { get; } =
    [
        (TextureKind.Steady, new ByConvention(1, 0.82, 1), 0),
        (TextureKind.Build, new ByConvention(0, 0.1, 1), 0),
        (TextureKind.Thin, new ByConvention(0, 0.04, 1), -1),
        (TextureKind.Alone, new ByConvention(0, 0.04, 1), -1)
    ];

    /// <summary>A texture as a genre's page shows it.</summary>
    public static string LabelOf(TextureKind kind) => kind switch
    {
        TextureKind.Steady => "Every part through",
        TextureKind.Build => "Parts coming in",
        TextureKind.Thin => "Parts dropping out",
        _ => "One part alone"
    };

    /// <summary>
    ///     An appearance's texture: the parts it leaves out throughout, over those it rests already, and those it leaves
    ///     out in each phrase; a texture that needs more phrases or parts than the appearance has plays steady.
    /// </summary>
    /// <param name="form">The form facet of the section's unconventionality.</param>
    /// <param name="phrases">How many 4-bar patterns the appearance plays.</param>
    /// <param name="playing">The parts the appearance plays, of those the song has.</param>
    /// <param name="genre">The genre whose pool of textures it is drawn among, where it names one.</param>
    /// <param name="energy">The appearance's energy, which the textures of fewer parts lean against.</param>
    /// <param name="drumsMark">Whether the song's drums only mark its form, which leaves them no part to play alone.</param>
    public static (TextureKind Kind, ImmutableHashSet<TrackRole> Resting, ImmutableArray<ImmutableHashSet<TrackRole>> PhraseSilent) Draw(
        IGenerationContext context,
        double form,
        int phrases,
        IReadOnlyCollection<TrackRole> playing,
        Genre genre,
        Tilt energy,
        bool drumsMark
    )
    {
        var leans = Kinds.ToDictionary(x => x.Kind, x => x.EnergyLean);
        var kind = context.Pick(energy.Weigh(
            ByConvention.Weigh(Kinds.Select(x => (x.Kind, x.Weight)), form, genre.Pool<TextureKind>(GenreChoices.Texture, x => GenreChoices.Name(x.ToString()))),
            x => leans[x]));
        TrackRole[] pitched = [..playing.Where(x => x != TrackRole.Drum).Order()];
        // the order the pitched parts come in, drawn whatever the texture, so that the draws stay as they are
        var order = pitched.OrderBy(_ => context.GenerateDouble()).ToArray();
        // drawn whether any part plays or not, so that the draws stay as they are; among the parts that carry a section
        // alone, the drums only where they keep time
        // alone, the drums only where they keep time, as likely each, or as a genre weighs them (GenreChoices.Alone), a
        // metal's clean arpeggio, an ambient piece's pad
        TrackRole[] carrying = [..playing.Where(x => x != TrackRole.Drum || !drumsMark).Order()];
        var weighed = genre.Pool<TrackRole>(GenreChoices.Alone, x => GenreChoices.Name(x.ToString())) is { } pool
            ? carrying.Select(x => new Weighted<TrackRole>(pool(x)?.At(form) ?? 0, x)).Where(x => x.Weight > 0).ToImmutableArray()
            : [];
        TrackRole? alone = weighed.IsEmpty ? null : context.Pick(weighed);
        if (weighed.IsEmpty)
        {
            var aloneIndex = context.GenerateInt(0, Math.Max(1, carrying.Length));
            alone = carrying.Length == 0 ? null : carrying[aloneIndex % carrying.Length];
        }

        // and where the drums come in a build, among the pitched parts, drawn after the rest so that they stay as they were;
        // never first where they only mark the form, which would leave the first phrase silent but for its marks
        var drumsAt = context.GenerateInt(0, order.Length + 1);
        if (drumsMark)
            drumsAt = Math.Max(1, drumsAt);
        ImmutableArray<ImmutableHashSet<TrackRole>> none = [..Enumerable.Repeat(ImmutableHashSet<TrackRole>.Empty, phrases)];

        switch (kind)
        {
            case TextureKind.Build or TextureKind.Thin when phrases >= 2 && order.Length >= 2:
            {
                // phrase p plays the first of the order, more every phrase, from one to all; a build's drums among them,
                // a thinning's playing through, as the drums never stop within a section
                TrackRole[] parts = kind == TextureKind.Build && playing.Contains(TrackRole.Drum) ? [..order.Take(drumsAt), TrackRole.Drum, ..order.Skip(drumsAt)] : order;
                var silent = Enumerable.Range(0, phrases)
                    .Select(phrase => parts.Skip((int)Math.Ceiling((phrase + 1) * parts.Length / (double)phrases)).ToImmutableHashSet())
                    .ToArray();
                if (kind == TextureKind.Thin)
                    Array.Reverse(silent);
                return (kind, ImmutableHashSet<TrackRole>.Empty, [..silent]);
            }
            case TextureKind.Alone when playing.Count >= 2 && alone is not null:
                return (kind, [..playing.Where(x => x != alone)], none);
            default:
                return (TextureKind.Steady, ImmutableHashSet<TrackRole>.Empty, none);
        }
    }
}
