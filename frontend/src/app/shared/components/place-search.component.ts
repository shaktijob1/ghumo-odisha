import { Component, ElementRef, HostListener, OnDestroy, effect, inject, input, output, signal, viewChild } from '@angular/core';
import { GoogleMapsService, PickedPlace, PlaceSuggestion } from '../../core/services/google-maps.service';

/**
 * Type-ahead place search (Google Places): the customer types, picks one of the suggestions, and the
 * component emits the point + a readable label. Optionally offers "Use my current location" and
 * "Choose on map".
 *
 * variant="form": a normal input (booking / admin pages).
 * variant="hero": the home search card's box (.hx-box) and dropdown (.hs-pop), like the Trips fields.
 */
@Component({
  selector: 'app-place-search',
  standalone: true,
  template: `
    <div class="ps" [class.ps-hero]="variant() === 'hero'">
      @if (variant() === 'hero') {
        <div class="hx-box" (click)="focusInput()">
          <input
            #inp
            class="hx-input"
            type="text"
            autocomplete="off"
            [id]="inputId()"
            [placeholder]="placeholder()"
            [disabled]="disabled()"
            [value]="text()"
            (input)="onType($any($event.target).value)"
            (focus)="onFocus()"
            (keydown)="onKey($event)"
            role="combobox"
            aria-autocomplete="list"
            [attr.aria-expanded]="open()"
            [attr.aria-controls]="inputId() + '-list'"
          />
          @if (busy() || locating()) {
            <span class="spin ps-spin"></span>
          } @else if (text()) {
            <button type="button" class="hs-clear" aria-label="Clear" (click)="clear($event)">
              <svg width="10" height="10" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round"><path d="M18 6 6 18M6 6l12 12"></path></svg>
            </button>
          }
        </div>
      } @else {
        <div class="ps-box">
          <svg class="ps-ic" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true">
            <path d="M12 21s-7-6.2-7-11.5A7 7 0 0 1 19 9.5C19 14.8 12 21 12 21z" /><circle cx="12" cy="9.5" r="2.5" />
          </svg>
          <input
            #inp
            class="inp ps-inp"
            type="text"
            autocomplete="off"
            [id]="inputId()"
            [placeholder]="placeholder()"
            [disabled]="disabled()"
            [value]="text()"
            (input)="onType($any($event.target).value)"
            (focus)="onFocus()"
            (keydown)="onKey($event)"
            role="combobox"
            aria-autocomplete="list"
            [attr.aria-expanded]="open()"
            [attr.aria-controls]="inputId() + '-list'"
          />
          @if (busy()) { <span class="spin ps-spin"></span> }
        </div>
      }

      @if (open() && (suggestions().length || heroActions())) {
        <div class="ps-list" [class.hs-pop]="variant() === 'hero'" [class.hs-places]="variant() === 'hero'" role="listbox" [id]="inputId() + '-list'">
          @if (heroActions()) {
            @if (allowCurrentLocation()) {
              <button type="button" class="ps-act" (mousedown)="$event.preventDefault()" (click)="useCurrentLocation()">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><circle cx="12" cy="12" r="3.5" /><path d="M12 2v3M12 19v3M2 12h3M19 12h3" /><circle cx="12" cy="12" r="8" /></svg>
                Use my current location
              </button>
            }
            @if (allowMap()) {
              <button type="button" class="ps-act" (mousedown)="$event.preventDefault()" (click)="askMap()">
                <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M9 4 3 6.5v13L9 17l6 2.5 6-2.5V4l-6 2.5z" /><path d="M9 4v13M15 6.5v13" /></svg>
                Choose on map
              </button>
            }
          }
          @for (s of suggestions(); track $index) {
            <button type="button" role="option" [class.on]="$index === active()" [attr.aria-selected]="$index === active()"
                (mousedown)="$event.preventDefault()" (click)="choose(s)" (mouseenter)="active.set($index)">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M12 21s-6.5-5.9-6.5-11A6.5 6.5 0 0 1 18.5 10c0 5.1-6.5 11-6.5 11z"></path><circle cx="12" cy="10" r="2.3"></circle></svg>
              <span class="ps-txt"><b>{{ s.mainText }}</b>@if (s.secondaryText) { <small>{{ s.secondaryText }}</small> }</span>
            </button>
          }
          @if (suggestions().length) { <span class="ps-by" aria-hidden="true">powered by Google</span> }
        </div>
      }

      @if (variant() === 'form' && (allowCurrentLocation() || allowMap())) {
        <div class="ps-links">
          @if (allowCurrentLocation()) {
            <button type="button" class="ps-here" [disabled]="disabled() || locating()" (click)="useCurrentLocation()">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><circle cx="12" cy="12" r="3.5" /><path d="M12 2v3M12 19v3M2 12h3M19 12h3" /><circle cx="12" cy="12" r="8" /></svg>
              {{ locating() ? 'Finding your location…' : 'Use my current location' }}
            </button>
          }
          @if (allowMap()) {
            <button type="button" class="ps-here" [disabled]="disabled()" (click)="askMap()">
              <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M9 4 3 6.5v13L9 17l6 2.5 6-2.5V4l-6 2.5z" /><path d="M9 4v13M15 6.5v13" /></svg>
              Choose on map
            </button>
          }
        </div>
      }
      @if (error()) { <div class="err-msg">{{ error() }}</div> }
    </div>
  `,
  styles: `
    .ps { position: relative; }
    .ps-box { position: relative; }
    .ps-ic { position: absolute; left: 12px; top: 50%; transform: translateY(-50%); color: var(--muted); pointer-events: none; }
    .ps-inp { padding-left: 36px; padding-right: 36px; }
    .ps-box .ps-spin { position: absolute; right: 12px; top: 50%; margin-top: -8px; width: 16px; height: 16px; }
    .ps-hero .ps-spin { width: 16px; height: 16px; flex: none; }
    /* Form variant dropdown (the hero one uses the global .hs-pop / .hs-places). */
    .ps:not(.ps-hero) .ps-list {
      position: absolute; z-index: 40; left: 0; right: 0; top: calc(100% + 6px); padding: 6px;
      background: var(--surface); border: 1px solid var(--line); border-radius: var(--radius-control);
      box-shadow: 0 14px 34px rgba(15, 20, 22, 0.14); max-height: 300px; overflow-y: auto;
    }
    .ps:not(.ps-hero) .ps-list button { display: flex; align-items: flex-start; gap: 9px; width: 100%; padding: 9px 10px; border: none; border-radius: 9px; background: none; text-align: left; cursor: pointer; font: inherit; }
    .ps:not(.ps-hero) .ps-list button svg { color: var(--muted); flex: none; margin-top: 2px; }
    .ps:not(.ps-hero) .ps-list button.on, .ps:not(.ps-hero) .ps-list button:hover { background: var(--accent-soft); }
    .ps-txt { display: flex; flex-direction: column; gap: 1px; min-width: 0; }
    .ps-txt b { font-weight: 500; font-size: 13px; color: var(--ink); }
    .ps-txt small { font-size: 11.5px; color: var(--muted); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
    .ps-hero .ps-txt b { font-size: 13.5px; }
    .ps-hero .hs-places { max-height: 330px; }
    .ps-hero .ps-act { color: var(--accent); font-weight: 600; }
    .ps-hero .ps-act svg { color: var(--accent); }
    .ps-by { display: block; text-align: right; font-size: 10.5px; color: var(--muted); padding: 4px 8px 2px; }
    .ps-links { display: flex; flex-wrap: wrap; gap: 6px 16px; margin-top: 8px; }
    .ps-here { display: inline-flex; align-items: center; gap: 6px; padding: 0; border: none; background: none; font: 600 12.5px var(--font-body); color: var(--accent); cursor: pointer; }
    .ps-here:disabled { opacity: 0.6; cursor: default; }
    .ps-here:hover:not(:disabled) { text-decoration: underline; }
  `,
})
export class PlaceSearchComponent implements OnDestroy {
  private readonly maps = inject(GoogleMapsService);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly inp = viewChild<ElementRef<HTMLInputElement>>('inp');

  readonly variant = input<'form' | 'hero'>('form');
  readonly inputId = input('place-search');
  readonly placeholder = input('Search for a place');
  readonly disabled = input(false);
  readonly allowCurrentLocation = input(false);
  readonly allowMap = input(false);
  /** The label of the place currently chosen (shown in the box). */
  readonly value = input<string | null>(null);

  readonly picked = output<PickedPlace>();
  /** Fired when the customer edits or clears the text after choosing — the old choice no longer matches. */
  readonly cleared = output<void>();
  /** "Choose on map" — the parent opens its map picker. */
  readonly mapRequested = output<void>();

  readonly text = signal('');
  readonly suggestions = signal<PlaceSuggestion[]>([]);
  readonly open = signal(false);
  readonly active = signal(0);
  readonly busy = signal(false);
  readonly locating = signal(false);
  readonly error = signal<string | null>(null);

  private timer?: ReturnType<typeof setTimeout>;
  private token?: google.maps.places.AutocompleteSessionToken;
  private query = 0;
  private hasChoice = false;

  constructor() {
    effect(() => {
      const v = this.value();
      if (v !== null) {
        this.text.set(v);
        this.hasChoice = true;
      } else if (this.hasChoice) {
        // Cleared by the parent (not by typing — typing drops hasChoice first), so empty the box.
        this.text.set('');
        this.hasChoice = false;
      }
    });
  }

  /** Hero dropdown shows "current location / choose on map" while the box is empty or just focused. */
  heroActions(): boolean {
    return this.variant() === 'hero' && (this.allowCurrentLocation() || this.allowMap()) && this.suggestions().length === 0;
  }

  focusInput(): void {
    this.inp()?.nativeElement.focus();
  }

  onFocus(): void {
    this.open.set(this.suggestions().length > 0 || this.heroActions());
  }

  onType(value: string): void {
    this.text.set(value);
    this.error.set(null);
    if (this.hasChoice) {
      this.hasChoice = false;
      this.cleared.emit();
    }
    clearTimeout(this.timer);
    const q = value.trim();
    if (q.length < 2) {
      this.suggestions.set([]);
      this.open.set(this.heroActions());
      return;
    }
    this.timer = setTimeout(() => void this.fetch(q), 250);
  }

  clear(event: Event): void {
    event.stopPropagation();
    this.onType('');
    this.focusInput();
  }

  private async fetch(q: string): Promise<void> {
    const id = ++this.query;
    this.busy.set(true);
    try {
      this.token ??= await this.maps.newSessionToken();
      const list = await this.maps.suggest(q, this.token);
      if (id !== this.query) return; // an older request finishing late
      this.suggestions.set(list);
      this.active.set(0);
      this.open.set(list.length > 0 || this.heroActions());
      if (!list.length) this.error.set('No matching places. Try a nearby landmark.');
    } catch (e) {
      if (id === this.query) this.error.set(e instanceof Error ? e.message : 'Could not search places right now.');
    } finally {
      if (id === this.query) this.busy.set(false);
    }
  }

  async choose(s: PlaceSuggestion): Promise<void> {
    this.open.set(false);
    this.busy.set(true);
    try {
      const place = await this.maps.resolve(s);
      this.token = undefined; // a session ends with the details call
      this.suggestions.set([]);
      this.text.set(place.label);
      this.hasChoice = true;
      this.picked.emit(place);
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Could not open that place. Please pick another.');
    } finally {
      this.busy.set(false);
    }
  }

  async useCurrentLocation(): Promise<void> {
    this.open.set(false);
    this.error.set(null);
    this.locating.set(true);
    try {
      const place = await this.maps.currentPlace();
      this.text.set(place.label);
      this.hasChoice = true;
      this.suggestions.set([]);
      this.picked.emit(place);
    } catch (e) {
      this.error.set(e instanceof Error ? e.message : 'Couldn’t find your location.');
    } finally {
      this.locating.set(false);
    }
  }

  askMap(): void {
    this.open.set(false);
    this.mapRequested.emit();
  }

  onKey(e: KeyboardEvent): void {
    const list = this.suggestions();
    if (e.key === 'Escape') {
      this.open.set(false);
      return;
    }
    if (!this.open() || !list.length) return;
    if (e.key === 'ArrowDown') {
      e.preventDefault();
      this.active.set((this.active() + 1) % list.length);
    } else if (e.key === 'ArrowUp') {
      e.preventDefault();
      this.active.set((this.active() - 1 + list.length) % list.length);
    } else if (e.key === 'Enter') {
      e.preventDefault();
      void this.choose(list[this.active()]);
    }
  }

  @HostListener('document:mousedown', ['$event'])
  onOutside(e: MouseEvent): void {
    if (!this.host.nativeElement.contains(e.target as Node)) this.open.set(false);
  }

  ngOnDestroy(): void {
    clearTimeout(this.timer);
  }
}
