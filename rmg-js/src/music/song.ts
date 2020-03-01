import { Scale } from './scale';
import { Note } from './note';
import { Track } from './track';
import { Section } from './section';
import { DurationEntity } from './duration-entity';
import { Timeline } from '../core/timeline';

export interface Song extends DurationEntity {
  tracks: Track[];
  scale: Timeline<Scale>;
  noteBase: Timeline<Note>;
  tempo: Timeline<number>;
  section: Timeline<Section>;
}
