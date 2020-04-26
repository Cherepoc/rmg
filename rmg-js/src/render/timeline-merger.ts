import { TimelineMerger } from '../core/timeline-operations';

export const additiveMerger: TimelineMerger<number> = {
  merge(t1: number, t2: number): number {
    return t1 + t2;
  },
  default(): number {
    return 0;
  },
};

export const multiplicativeMerger: TimelineMerger<number> = {
  merge(t1: number, t2: number): number {
    return t1 * t2;
  },
  default(): number {
    return 1;
  },
};
