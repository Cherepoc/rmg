export function mapObject<T>(obj: T, func: (key: keyof T, value: any) => any): {[P in keyof T]: any} {
  const result: any = {};
  for (let objKey in obj) {
    const key = <keyof T>objKey;
    result[key] = func(key, obj[key]);
  }
  return result;
}
