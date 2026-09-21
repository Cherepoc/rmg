using System.Collections.Immutable;

namespace Rmg.Core.Composition;

/// <summary>
///     A part of a song: a sequence of section ids ordered by a brush. Section ids are song-wide, so the same
///     section can be used by several parts.
/// </summary>
public sealed record SongPart(SectionBrush Brush, ImmutableArray<int> SectionIds);
