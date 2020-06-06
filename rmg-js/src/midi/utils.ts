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
