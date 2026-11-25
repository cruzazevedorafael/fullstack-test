import { niceTicks } from './growth-scale';

describe('niceTicks', () => {
  it.each([
    [1000, 1120.87, [1000, 1050, 1100, 1150]],
    [0.01, 0.02, [0.01, 0.02]],
    [850.5, 1080, [800, 900, 1000, 1100]],
  ])('covers %d to %d with round numbers', (min, max, expected) => {
    expect(niceTicks(min, max)).toEqual(expected);
  });

  it('still has a range when the balance never changes', () => {
    const ticks = niceTicks(0.01, 0.01);

    expect(ticks.length).toBeGreaterThan(1);
    expect(ticks[0]).toBeLessThanOrEqual(0.01);
    expect(ticks.at(-1)).toBeGreaterThan(0.01);
  });
});
