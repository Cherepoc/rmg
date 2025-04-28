namespace Rmg.Core.Probabilities;

public readonly record struct Weighted<T>(double Weight, T Value);