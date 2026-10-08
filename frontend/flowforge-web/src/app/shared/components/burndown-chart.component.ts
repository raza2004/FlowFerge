import { AfterViewInit, Component, ElementRef, Input, OnChanges, OnDestroy, ViewChild } from '@angular/core';
import {
  CategoryScale, Chart, Filler, Legend, LineController, LineElement, LinearScale, PointElement, Tooltip
} from 'chart.js';
import { BurndownDto } from '../models/project.models';

// Register only what a line chart needs, instead of every chart.js feature.
Chart.register(LineController, LineElement, PointElement, LinearScale, CategoryScale, Tooltip, Legend, Filler);

@Component({
  selector: 'app-burndown-chart',
  standalone: true,
  template: `
    <div class="relative h-56">
      <canvas #canvas role="img"
              [attr.aria-label]="'Burndown chart: ' + data.total + ' ' + data.unit + ' of scope'"></canvas>
    </div>
  `
})
export class BurndownChartComponent implements AfterViewInit, OnChanges, OnDestroy {
  @Input({ required: true }) data!: BurndownDto;
  @ViewChild('canvas', { static: true }) canvas!: ElementRef<HTMLCanvasElement>;

  private chart?: Chart;

  ngAfterViewInit() {
    this.chart = new Chart(this.canvas.nativeElement, {
      type: 'line',
      data: this.chartData(),
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
          legend: { position: 'bottom', labels: { boxWidth: 18, boxHeight: 3, usePointStyle: false, padding: 16 } },
          tooltip: { callbacks: { label: ctx => `${ctx.dataset.label}: ${ctx.parsed.y} ${this.data.unit}` } }
        },
        scales: {
          y: { beginAtZero: true, suggestedMax: this.data.total || 1, ticks: { precision: 0 } },
          x: { grid: { display: false } }
        }
      }
    });
  }

  ngOnChanges() {
    if (!this.chart) return;
    this.chart.data = this.chartData();
    this.chart.update();
  }

  ngOnDestroy() {
    this.chart?.destroy();
  }

  private chartData() {
    const labels = this.data.points.map(p =>
      new Date(p.date).toLocaleDateString(undefined, { month: 'short', day: 'numeric', timeZone: 'UTC' }));

    return {
      labels,
      datasets: [
        {
          label: 'Ideal',
          data: this.data.points.map(p => p.ideal),
          borderColor: '#A1A1AA',
          borderDash: [6, 4],
          pointRadius: 0,
          borderWidth: 2,
          tension: 0
        },
        {
          label: 'Remaining',
          data: this.data.points.map(p => p.remaining),
          borderColor: '#6449E0',
          backgroundColor: 'rgba(100, 73, 224, 0.12)',
          fill: true,
          pointRadius: 3,
          borderWidth: 2,
          tension: 0.2,
          spanGaps: false
        }
      ]
    };
  }
}
