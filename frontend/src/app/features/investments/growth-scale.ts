const CENT = 0.01;

/** Evenly spaced round values that cover min and max, for the vertical axis. */
export function niceTicks(min: number, max: number): number[] {
  const rough = Math.max((max - min) / 4, CENT);
  const magnitude = 10 ** Math.floor(Math.log10(rough));
  const step = [1, 2, 5, 10].map((factor) => factor * magnitude).find((candidate) => candidate >= rough - 1e-9)!;

  const first = Math.floor(min / step);
  const last = Math.max(Math.ceil(max / step), first + 1);

  return Array.from({ length: last - first + 1 }, (_, i) => Number(((first + i) * step).toFixed(2)));
}
