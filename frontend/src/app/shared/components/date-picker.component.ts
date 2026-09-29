import { CommonModule } from '@angular/common';
import { Component, ElementRef, EventEmitter, HostListener, Input, Output, signal } from '@angular/core';

interface DayCell {
  date: Date;
  inMonth: boolean;
  disabled: boolean;
}

@Component({
  selector: 'app-date-picker',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div class="dpick">
      <button type="button" class="dpick-trigger" (click)="toggle()">
        <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="4" width="18" height="18" rx="2"></rect><path d="M16 2v4M8 2v4M3 10h18"></path></svg>
        <span [class.ph]="!value">{{ displayLabel }}</span>
      </button>

      @if (open()) {
        <div class="dpick-pop" (click)="$event.stopPropagation()">
          <div class="dpick-head">
            <button type="button" class="dpick-nav" (click)="prevMonth()" [disabled]="!canGoPrev" aria-label="Previous month">&lsaquo;</button>
            <b>{{ monthLabel }}</b>
            <button type="button" class="dpick-nav" (click)="nextMonth()" aria-label="Next month">&rsaquo;</button>
          </div>
          <div class="dpick-week">
            @for (w of weekdays; track $index) { <span>{{ w }}</span> }
          </div>
          <div class="dpick-grid">
            @for (c of cells(); track c.date.getTime()) {
              <button
                type="button"
                class="dpick-cell"
                [class.out]="!c.inMonth"
                [class.today]="isToday(c.date)"
                [class.sel]="isSelected(c.date)"
                [disabled]="c.disabled"
                (click)="pick(c)"
              >
                {{ c.date.getDate() }}
              </button>
            }
          </div>
          @if (value) {
            <button type="button" class="dpick-clear" (click)="clear()">Clear date</button>
          }
        </div>
      }
    </div>
  `,
})
export class DatePickerComponent {
  @Input() value = '';
  @Input() placeholder = 'Select date';
  @Input() min: string | null = null;
  @Output() valueChange = new EventEmitter<string>();

  readonly open = signal(false);
  readonly viewMonth = signal(this.startOfMonth(new Date()));

  readonly weekdays = ['S', 'M', 'T', 'W', 'T', 'F', 'S'];

  constructor(private readonly host: ElementRef<HTMLElement>) {}

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
    }
  }

  toggle(): void {
    const next = !this.open();
    this.open.set(next);
    if (next) {
      this.viewMonth.set(this.startOfMonth(this.value ? this.parseIso(this.value) : new Date()));
    }
  }

  get displayLabel(): string {
    if (!this.value) return this.placeholder;
    // "Thu, 15 Oct 2026" — built by hand: en-IN adds a comma before the year and writes "Sept".
    const d = this.parseIso(this.value);
    const weekday = d.toLocaleDateString('en-IN', { weekday: 'short' });
    const dayMonth = d.toLocaleDateString('en-IN', { day: 'numeric', month: 'short' }).replace('Sept', 'Sep');
    return `${weekday}, ${dayMonth} ${d.getFullYear()}`;
  }

  get monthLabel(): string {
    return this.viewMonth().toLocaleDateString('en-IN', { month: 'long', year: 'numeric' });
  }

  cells(): DayCell[] {
    const first = this.viewMonth();
    const gridStart = new Date(first.getFullYear(), first.getMonth(), 1 - first.getDay());
    const minDate = this.min ? this.parseIso(this.min) : null;
    const cells = Array.from({ length: 42 }, (_, i) => {
      const date = new Date(gridStart.getFullYear(), gridStart.getMonth(), gridStart.getDate() + i);
      return {
        date,
        inMonth: date.getMonth() === first.getMonth(),
        disabled: !!minDate && date < minDate,
      };
    });
    // Drop whole weeks that are entirely before `min` (e.g. late in the month only the last week or
    // two are offered) so the calendar isn't mostly greyed-out rows.
    while (cells.length > 7 && cells.slice(0, 7).every((c) => c.disabled)) {
      cells.splice(0, 7);
    }
    return cells;
  }

  isSelected(date: Date): boolean {
    return !!this.value && this.parseIso(this.value).toDateString() === date.toDateString();
  }

  isToday(date: Date): boolean {
    return new Date().toDateString() === date.toDateString();
  }

  /** No stepping back into months that are entirely before `min`. */
  get canGoPrev(): boolean {
    if (!this.min) return true;
    return this.viewMonth() > this.startOfMonth(this.parseIso(this.min));
  }

  prevMonth(): void {
    if (!this.canGoPrev) return;
    const d = this.viewMonth();
    this.viewMonth.set(new Date(d.getFullYear(), d.getMonth() - 1, 1));
  }

  nextMonth(): void {
    const d = this.viewMonth();
    this.viewMonth.set(new Date(d.getFullYear(), d.getMonth() + 1, 1));
  }

  pick(cell: DayCell): void {
    if (cell.disabled) return;
    this.value = this.toIso(cell.date);
    this.valueChange.emit(this.value);
    this.open.set(false);
  }

  clear(): void {
    this.value = '';
    this.valueChange.emit('');
    this.open.set(false);
  }

  private startOfMonth(d: Date): Date {
    return new Date(d.getFullYear(), d.getMonth(), 1);
  }

  private parseIso(iso: string): Date {
    return new Date(`${iso}T00:00:00`);
  }

  private toIso(d: Date): string {
    const y = d.getFullYear();
    const m = String(d.getMonth() + 1).padStart(2, '0');
    const day = String(d.getDate()).padStart(2, '0');
    return `${y}-${m}-${day}`;
  }
}
