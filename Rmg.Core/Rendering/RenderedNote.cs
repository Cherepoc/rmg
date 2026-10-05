namespace Rmg.Core.Rendering;

/// <summary>
///     A note as it is written. <paramref name="Shift" /> is how far, in beats, its part plays it off its place, which
///     only the MIDI written hears: the note keeps its place on the grid for whatever reads the rendering.
/// </summary>
public readonly record struct RenderedNote(int Offset, double Velocity, double Duration, double Shift = 0);
