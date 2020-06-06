import { createTypeGuard, TypeGuard } from '../core/type-check';

export interface DurationEntity {
  duration: number
}

export const isDurationEntity: TypeGuard<DurationEntity> = createTypeGuard<DurationEntity>('duration');
