namespace Rmg.WebApi.Analytics;

/// <summary>How a song asked of the server ended.</summary>
public enum SongOutcome
{
    Made,
    CancelledWaiting,
    CancelledMaking,
    RefusedClient,
    RefusedFull,
    Failed
}

public static class SongOutcomes
{
    /// <summary>The outcome as it is kept and shown.</summary>
    public static string Name(SongOutcome outcome) => outcome switch
    {
        SongOutcome.Made => "made",
        SongOutcome.CancelledWaiting => "cancelled_waiting",
        SongOutcome.CancelledMaking => "cancelled_making",
        SongOutcome.RefusedClient => "refused_client",
        SongOutcome.RefusedFull => "refused_full",
        SongOutcome.Failed => "failed",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
    };
}

/// <summary>What the server measured of a song asked of it, which says nothing of who asked.</summary>
/// <param name="WaitMs">How long it waited for its turn; for one refused, how long it took to be.</param>
/// <param name="MakeMs">How long generating it took, for one that was generated.</param>
/// <param name="RenderMs">How long rendering it and writing its file took, for one that was rendered.</param>
/// <param name="Running">How many songs were being made when it arrived.</param>
/// <param name="Waiting">How many were waiting their turn when it arrived.</param>
/// <param name="Version">The songs' version that made it.</param>
/// <param name="Unconventionality">How far it strays from convention, from 0 to 63, for one that was generated.</param>
/// <param name="Notes">How many notes it has, which is how much there was to make, for one that was generated.</param>
public sealed record SongMeasurement(
    DateTimeOffset At,
    SongOutcome Outcome,
    long WaitMs,
    long? MakeMs,
    long? RenderMs,
    int Running,
    int Waiting,
    string Version,
    int? Unconventionality,
    int? Notes
);
