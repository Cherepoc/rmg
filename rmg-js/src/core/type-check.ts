export type TypeGuard<T> = (obj: any) => obj is T;

export function createTypeGuard<T> (...fields: Array<keyof T>): TypeGuard<T> {
  return <TypeGuard<T>>((obj: any) => fields.every(field => obj[field] !== undefined));
}
