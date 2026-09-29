namespace Rmg.Core.Composition;

/// <summary>The chord a note is played over: the pitch of any step of the scale, counted from the chord's root.</summary>
internal sealed class ChordContext(Func<int, int> getPitch)
{
    /// <summary>The pitch of the note the steps above the root, or below it for negative steps.</summary>
    public int GetPitch(int stepsAboveRoot)
    {
        return getPitch(stepsAboveRoot);
    }

    public int Root => GetPitch(0);
}
