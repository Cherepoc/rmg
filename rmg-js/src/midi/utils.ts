export function intToBytes (value: number, length = 0, bitsInByte = 8): number[] {
  const result: number[] = [];
  let shiftedValue = Math.floor(value);
  const mask = 0xffffffff >>> (32 - bitsInByte);
  while ((length >= 1 && result.length < length) || (length < 1 && (shiftedValue > 0 || result.length === 0))) {
    const byte = shiftedValue & mask;
    result.unshift(byte);
    shiftedValue = shiftedValue >>> bitsInByte;
  }
  return result;
}

// export function* intToBytesGenerator (value: number, length = 0, bitsInByte = 8): Generator<number, void> {
//   const valueLength = Math.ceil(Math.log2(value) / bitsInByte);
//   const minLength = Math.min(valueLength, length >= 1 ? length : valueLength, 1);
//   const mask = 0xffffffff >>> (32 - bitsInByte);
//   for (let i = minLength - 1; i >= 0; i--) {
//     yield (value >>> (bitsInByte * i)) & mask;
//   }
// }
