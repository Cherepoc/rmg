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

            var volume = song.NoteBase.Volume
                         * flattenedTrack.Track.NoteBase.Volume
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
            var track = flattenedTrack.Track;

            var baseScaleOffset = flattenedTrack.NoteBasePattern.ScaleOffsetTimeline.GetEffectiveEvent(
                timedNote.Position,
                new int[Scale.ScaleRankCount]);

            var scaleOffsetIndex = 0;
            for (var i = 0; i < scale.RankedOffsetIndexes.Count; i++)
            {
                var rankedScaleIndexes = scale.RankedOffsetIndexes[i];
                var offset = song.NoteBase.ScaleOffset[i]
                             + track.NoteBase.ScaleOffset[i]
                             + baseScaleOffset[i]
                             + note.ScaleOffset[i];
                var rankScaleOffsetIndex = MathUtils.Mod(offset, rankedScaleIndexes.Count);
                scaleOffsetIndex += rankedScaleIndexes[rankScaleOffsetIndex];
            }

            var scaleOffset = scale.NoteOffsets[scaleOffsetIndex % scale.NoteOffsets.Count];

            var baseOctave = flattenedTrack.NoteBasePattern.OctaveTimeline.GetEffectiveEvent(timedNote.Position, 0);
            var octaveOffset = (song.NoteBase.Octave + track.NoteBase.Octave + baseOctave + note.Octave) * 12;

            var baseKey = song.NoteBase.Key
                          + track.NoteBase.Key
                          + flattenedTrack.NoteBasePattern.KeyTimeline.GetEffectiveEvent(timedNote.Position, 0);

            var noteOffset = baseKey + octaveOffset + scaleOffset;
            return FixOctaveNoteOffset(noteOffset, track.MinOctave, track.MaxOctave);
        }

        private static int FixOctaveNoteOffset(int noteOffset, int minOctave, int maxOctave)
        {
            var minNoteOffset = minOctave * 12;
            var maxNoteOffset = (maxOctave + 1) * 12;
            var noteLength = maxNoteOffset - minNoteOffset;
            var fixedNoteOffset = noteOffset;
            while (true)
            {
                if (fixedNoteOffset < minNoteOffset)
                {
                    fixedNoteOffset += noteLength;
                }
                else if (fixedNoteOffset >= maxNoteOffset)
                {
                    fixedNoteOffset -= noteLength;
                }
                else
                {
                    break;
                }
            }

            return fixedNoteOffset;
        }
    }
}
