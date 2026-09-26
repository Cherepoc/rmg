using System.Collections.Immutable;

namespace Rmg.Core.Composition;

/// <summary>
///     A chord as drawn: the heights of its notes above the root in fractions of an octave, laid out by a voicing,
///     which <c>Render</c> snaps to the scale.
/// </summary>
/// <param name="IsVoicingFixed">
///     Whether the layout is what the chord is, such as a quartal stack or a cluster, so that it is moved only by whole
///     octaves and never laid out another way.
/// </param>
public sealed record Chord(ImmutableArray<double> Heights, bool IsVoicingFixed)
{
    public bool Equals(Chord? other)
    {
        return other is not null && IsVoicingFixed == other.IsVoicingFixed && Heights.SequenceEqual(other.Heights);
    }

    public override int GetHashCode()
    {
        var hashCode = new HashCode();
        hashCode.Add(IsVoicingFixed);
        foreach (var height in Heights)
            hashCode.Add(height);
        return hashCode.ToHashCode();
    }
}
