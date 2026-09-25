import { Directive, ElementRef, Input, OnChanges, OnDestroy, inject } from '@angular/core';

/**
 * Animates a number from its previous value to the new one ("₹0 → ₹4,800"), formatted en-IN.
 * Jumps straight to the value when the visitor prefers reduced motion.
 */
@Directive({ selector: '[appCountUp]', standalone: true })
export class CountUpDirective implements OnChanges, OnDestroy {
  @Input({ required: true }) appCountUp = 0;
  @Input() countPrefix = '';
  @Input() countDuration = 1100;

  private readonly el = inject(ElementRef<HTMLElement>);
  private current = 0;
  private frame = 0;

  ngOnChanges(): void {
    cancelAnimationFrame(this.frame);
    const from = this.current;
    const to = Number(this.appCountUp) || 0;
    const reduce = typeof window !== 'undefined' && window.matchMedia?.('(prefers-reduced-motion: reduce)').matches;
    if (reduce || from === to) {
      this.render(to);
      return;
    }

    const start = performance.now();
    const step = (now: number) => {
      const t = Math.min(1, (now - start) / this.countDuration);
      const eased = 1 - Math.pow(1 - t, 3); // ease-out cubic
      this.render(from + (to - from) * eased);
      if (t < 1) this.frame = requestAnimationFrame(step);
    };
    this.frame = requestAnimationFrame(step);
  }

  ngOnDestroy(): void {
    cancelAnimationFrame(this.frame);
  }

  private render(value: number): void {
    this.current = value;
    const rounded = Math.round(Math.abs(value));
    const sign = value < -0.5 ? '−' : '';
    this.el.nativeElement.textContent = `${sign}${this.countPrefix}${rounded.toLocaleString('en-IN')}`;
  }
}
