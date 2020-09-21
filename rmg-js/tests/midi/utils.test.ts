import { intToBytes } from '../../src/midi/utils';

describe("Midi utils - intToBytes", () => {
  it ("does not change simple number", () => {
    const result = intToBytes(0xff);
    expect(result).toStrictEqual([0xff]);
  });

  it ("breaks complex number", () => {
    const result = intToBytes(0xf1f2);
    expect(result).toStrictEqual([0xf1, 0xf2]);
  });

  it ("breaks complex number with length", () => {
    const result = intToBytes(0xf1f2, 1);
    expect(result).toStrictEqual([0xf2]);
  });

  it ("breaks complex number with bit length", () => {
    const result = intToBytes(0xff, 0, 7);
    expect(result).toStrictEqual([0x01, 0x7f]);
  });
});
