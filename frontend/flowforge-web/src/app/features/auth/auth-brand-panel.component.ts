import { Component, Input } from '@angular/core';
import { RouterLink } from '@angular/router';

/** Shared left panel for the sign in and register screens: ink surface, serif statement, drifting embers. */
@Component({
  selector: 'app-auth-brand-panel',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="relative h-full bg-ink text-canvas p-12 flex flex-col justify-between overflow-hidden">
      <div class="absolute inset-0 pointer-events-none" aria-hidden="true">
        @for (e of embers; track $index) {
          <i class="ember" [style.left.%]="e.x" [style.width.px]="e.s" [style.height.px]="e.s"
             [style.animationDuration.s]="e.d" [style.animationDelay.s]="e.delay"></i>
        }
      </div>

      <a routerLink="/" class="relative flex items-baseline gap-1 w-fit">
        <span class="font-display text-[30px] leading-none">FlowForge</span>
        <span class="w-1.5 h-1.5 rounded-full bg-ember-500"></span>
      </a>

      <div class="relative">
        <div class="eyebrow !text-canvas/50 mb-6">{{ eyebrow }}</div>
        <h2 class="font-display text-[clamp(3rem,5.4vw,5.5rem)] leading-[0.93] tracking-tight">
          {{ headline }} <em class="text-ember-400">{{ emphasis }}</em>
        </h2>
        <p class="mt-8 max-w-sm text-canvas/65 text-lg leading-relaxed">{{ sub }}</p>
      </div>

      <ul class="relative border-t border-canvas/15 font-mono text-[11px] uppercase tracking-[0.16em] text-canvas/55">
        @for (item of points; track item) {
          <li class="py-3 border-b border-canvas/15 flex items-center justify-between">
            <span>{{ item }}</span><i class="w-1.5 h-1.5 rounded-full bg-ember-500"></i>
          </li>
        }
      </ul>
    </div>
  `,
  styles: [`
    :host { display: block; height: 100%; }
    .ember {
      position: absolute; bottom: -12px; border-radius: 50%;
      background: #F2501C; opacity: 0;
      animation-name: rise; animation-iteration-count: infinite; animation-timing-function: linear;
    }
    @keyframes rise {
      0% { transform: translate(0, 0); opacity: 0; }
      15% { opacity: 0.8; }
      100% { transform: translate(28px, -110vh); opacity: 0; }
    }
    @media (prefers-reduced-motion: reduce) { .ember { animation: none; opacity: 0.25; bottom: 30%; } }
  `]
})
export class AuthBrandPanelComponent {
  @Input() eyebrow = 'Welcome';
  @Input() headline = '';
  @Input() emphasis = '';
  @Input() sub = '';
  @Input() points: string[] = ['Live boards', 'Sprints', 'Assistant'];

  // Fixed values (not random) so the layout is stable between renders.
  embers = [6, 14, 22, 31, 40, 48, 57, 66, 74, 83, 91, 36, 62, 12].map((x, i) => ({
    x, s: 2 + (i % 4), d: 9 + (i * 1.7) % 8, delay: -((i * 2.3) % 11)
  }));
}
