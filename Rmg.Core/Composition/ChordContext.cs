namespace Rmg.Core.Composition;

/// <summary>The chord a note is played over: the pitch of any step of the scale, counted from the chord's root, and the root's step in the scale.</summary>
internal sealed class ChordContext(Func<int, int> getPitch, int rootIndex)
{
    /// <summary>The pitch of the note the steps above the root, or below it for negative steps.</summary>
    public int GetPitch(int stepsAboveRoot)
    {
        return getPitch(stepsAboveRoot);
    }

    public int Root => GetPitch(0);

    /// <summary>The root's step in the scale, counted from its first note.</summary>
    public int RootIndex => rootIndex;

    /// <summary>The pitch of the scale's first note, the key's tonic in the section's mode, as many steps below the root as the root is above it.</summary>
    public int Tonic => GetPitch(-rootIndex);
}
