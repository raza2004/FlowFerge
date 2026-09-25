import { Directive, ElementRef, Input, OnChanges, SimpleChanges, inject } from '@angular/core';

@Directive({
  selector: '[appCountUp]',
  standalone: true
})
export class CountUpDirective implements OnChanges {
  private el = inject<ElementRef<HTMLElement>>(ElementRef);
  private rafId?: number;
  private current = 0;

  @Input('appCountUp') target: number | null = null;

  ngOnChanges(changes: SimpleChanges) {
    if (!('target' in changes) || this.target == null) return;
    this.animateTo(this.target);
  }

  private animateTo(target: number) {
    if (this.rafId) cancelAnimationFrame(this.rafId);
    const start = this.current;
    const delta = target - start;
    if (delta === 0) { this.render(target); return; }

    const duration = 600;
    const startTime = performance.now();

    const tick = (now: number) => {
      const progress = Math.min((now - startTime) / duration, 1);
      const eased = 1 - Math.pow(1 - progress, 3);
      const value = Math.round(start + delta * eased);
      this.render(value);
      if (progress < 1) {
        this.rafId = requestAnimationFrame(tick);
      } else {
        this.current = target;
      }
    };
    this.rafId = requestAnimationFrame(tick);
  }

  private render(value: number) {
    this.el.nativeElement.textContent = String(value);
  }
}
