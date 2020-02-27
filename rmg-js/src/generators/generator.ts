export interface IGenerator<T> {
  generate(): GeneratorValue<T>
}

export type GeneratorValue<T> = IGenerator<T> | T;
