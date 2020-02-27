import { ScaleOffset } from './simple-types';
import { DurationEntity } from './duration-entity';

export interface Note extends DurationEntity {
  key: number;
  scaleOffset: ScaleOffset;
  octave: number;
  volume: number;
}
