using System;
using System.Linq;
using RMG.Core.Music;
using RMG.Core.Utils;

namespace RMG.Core.Render
{
    public static class SongRenderer
    {
        public static RenderedSong RenderSong(Song song)
        {
            var flattenedSong = TimelineFlattener.FlattenSongTimeline(song);
            return new RenderedSong(
                song.Tempo,
                song.Duration,
                flattenedSong.TrackTimelines
                    .Select(flattenedTrack => RenderTrack(flattenedSong, flattenedTrack))
                    .ToList()
            );
        }

        private static RenderedTrack RenderTrack(
            FlattenedSongTimeline flattenedSong,
            FlattenedTrackTimeline flattenedTrack
        )
        {
            return new RenderedTrack(
                flattenedTrack.Track.Instrument.Code,
                flattenedTrack.NoteTimeline
                    .Select(note => RenderTimedNote(flattenedSong, flattenedTrack, note))
                    .ToList()
            );
        }

        private static TimedEvent<RenderedNote> RenderTimedNote(
            FlattenedSongTimeline flattenedSong,
            FlattenedTrackTimeline flattenedTrack,
            TimedEvent<Note> timedNote
        )
        {
            var note = timedNote.Event;
            var song = flattenedSong.Song;

            var offset = GetNoteOffset(flattenedSong, flattenedTrack, timedNote);

            var volume = song.Volume
                         * flattenedTrack.NoteBasePattern.VolumeTimeline.GetEffectiveEvent(timedNote.Position, 1)
                         * note.Volume;

            return new TimedEvent<RenderedNote>
            {
                Event = new RenderedNote(offset, volume, note.Duration),
                Position = timedNote.Position
            };
        }

        private static int GetNoteOffset(
            FlattenedSongTimeline flattenedSong,
            FlattenedTrackTimeline flattenedTrack,
            TimedEvent<Note> timedNote
        )
        {
            var note = timedNote.Event;
            var song = flattenedSong.Song;
            var scale = song.Scale;

            var baseScaleOffset = flattenedTrack.NoteBasePattern.ScaleOffsetTimeline.GetEffectiveEvent(
                timedNote.Position,
                new int[Scale.ScaleRankCount]);

            var scaleOffsetIndex = 0;
            for (int i = 0; i < scale.RankedOffsetIndexes.Count; i++)
            {
                var rankedScaleIndexes = scale.RankedOffsetIndexes[i];
                var rankScaleOffsetIndex = MathUtils.Mod(
                    song.ScaleNoteOffset[i] + baseScaleOffset[i] + note.ScaleOffset[i],
                    rankedScaleIndexes.Count);
                scaleOffsetIndex += rankedScaleIndexes[rankScaleOffsetIndex];
            }

            var scaleOffset = scale.NoteOffsets[scaleOffsetIndex % scale.NoteOffsets.Count];

            var baseOctave = flattenedTrack.NoteBasePattern.OctaveTimeline.GetEffectiveEvent(timedNote.Position, 0);
            var octaveOffset = (song.Octave + baseOctave + note.Octave) * 12;

            var baseKey = song.Key
                          + flattenedTrack.NoteBasePattern.KeyTimeline.GetEffectiveEvent(timedNote.Position, 0);

            return baseKey + octaveOffset + scaleOffset;
        }
    }
}
