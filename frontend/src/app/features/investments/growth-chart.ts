import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, computed, input, linkedSignal, signal } from '@angular/core';
import { GrowthPoint } from '../../core/models';
import { niceTicks } from './growth-scale';

/** A point with its position in the plot, in percentages from the top left corner. */
interface PlottedPoint extends GrowthPoint {
  x: number;
  y: number;
}

/** Balance over time. The line has steps because gains are paid once a month, not every day. */
@Component({
  selector: 'app-growth-chart',
  imports: [CurrencyPipe, DatePipe],
  templateUrl: './growth-chart.html',
})
export class GrowthChart {
  readonly points = input.required<GrowthPoint[]>();

  protected readonly showTable = signal(false);

  protected readonly ticks = computed(() => {
    const balances = this.points().map((point) => point.balance);
    return niceTicks(Math.min(...balances), Math.max(...balances));
  });

  protected readonly plotted = computed<PlottedPoint[]>(() => {
    const points = this.points();
    const start = Date.parse(points[0].date);
    const duration = Date.parse(points.at(-1)!.date) - start;
    const bottom = this.ticks()[0];
    const range = this.ticks().at(-1)! - bottom;

    return points.map((point) => ({
      ...point,
      x: ((Date.parse(point.date) - start) / duration) * 100,
      y: 100 - ((point.balance - bottom) / range) * 100,
    }));
  });

  protected readonly paid = computed(() => this.plotted().filter((point) => !point.projected));
  protected readonly hasProjection = computed(() => this.paid().length < this.points().length);
  protected readonly paidPath = computed(() => stepPath(this.paid()));
  protected readonly projectedPath = computed(() =>
    stepPath([this.paid().at(-1)!, ...this.plotted().filter((point) => point.projected)]),
  );
  protected readonly middleDate = computed(() => {
    const points = this.points();
    return (Date.parse(points[0].date) + Date.parse(points.at(-1)!.date)) / 2;
  });

  private readonly activeIndex = linkedSignal<GrowthPoint[], number | null>({
    source: this.points,
    computation: () => null,
  });
  protected readonly active = computed(() => {
    const index = this.activeIndex();
    return index === null ? null : this.plotted()[index];
  });

  protected tickLabel(tick: number): string {
    return new Intl.NumberFormat('en-US', {
      style: 'currency',
      currency: 'BRL',
      notation: this.ticks().at(-1)! >= 10_000 ? 'compact' : 'standard',
      minimumFractionDigits: 0,
      maximumFractionDigits: 3,
    }).format(tick);
  }

  protected pointAt(event: PointerEvent): void {
    const area = (event.currentTarget as HTMLElement).getBoundingClientRect();
    const x = ((event.clientX - area.left) / area.width) * 100;
    const distances = this.plotted().map((point) => Math.abs(point.x - x));
    this.activeIndex.set(distances.indexOf(Math.min(...distances)));
  }

  protected clear(): void {
    this.activeIndex.set(null);
  }

  protected move(event: KeyboardEvent): void {
    const last = this.points().length - 1;
    const current = this.activeIndex();
    const targets: Record<string, number | null> = {
      ArrowLeft: Math.max((current ?? last + 1) - 1, 0),
      ArrowRight: Math.min((current ?? -1) + 1, last),
      Home: 0,
      End: last,
      Escape: null,
    };

    if (event.key in targets) {
      event.preventDefault();
      this.activeIndex.set(targets[event.key]);
    }
  }
}

function stepPath(points: PlottedPoint[]): string {
  return points.map(({ x, y }, i) => (i === 0 ? `M${x} ${y}` : `H${x}V${y}`)).join('');
}
