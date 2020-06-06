export function sumArrays (...arrays: number[][]): number[] {
  const n = arrays.reduce((max, xs) => Math.max(max, xs.length), 0);
  const result = Array.from({ length: n });
  return result.map((_, i) => arrays.map(xs => xs[i] || 0).reduce((sum, x) => sum + x, 0));
}

export function arraysEqual (...arrays: any[][]): boolean {
  if (arrays.length === 0) {
    return true;
  }

  const length = arrays[0].length;
  if (arrays.some(x => x.length !== length)) {
    return false;
  }

  for (let i = 0; i < length; i++) {
    const value = arrays[0][0];
    if (arrays.some(x => x[0] !== value)) {
      return false;
    }
  }

  return true;
}
