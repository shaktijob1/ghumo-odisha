import { CommonModule } from '@angular/common';
import { Component, ElementRef, OnInit, computed, inject, signal, viewChild } from '@angular/core';
import { ContactService } from '../../core/services/contact.service';
import { HolidayService } from '../../core/services/holiday.service';
import { HolidayDestination, HolidayPackage } from '../../core/models/holiday.model';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';

type LoadState = 'loading' | 'ready' | 'error';

/**
 * The home page's Holidays tab: "Holiday Destinations" (a row like Trending Places, but its own
 * list from Admin → Holidays) and "Holiday Packages" (cards like Upcoming Adventures). Picking a
 * destination filters the packages. No online booking: each package's button opens WhatsApp.
 */
@Component({
  selector: 'app-home-holidays',
  standalone: true,
  imports: [CommonModule, ImageUrlPipe],
  template: `
    <section id="holiday-destinations" class="sect tight" aria-labelledby="hd-title">
      <div class="container">
        <div class="hpanel">
          <div class="sechead">
            <div class="sechead-text">
              <h2 id="hd-title"><span class="up-g">Holiday </span><span class="up-o">Destinations</span></h2>
            </div>
          </div>
          @switch (destinationsState()) {
            @case ('loading') {
              <div class="hd-row">
                @for (i of [1, 2, 3, 4]; track i) { <div class="hd-card skel" aria-hidden="true"></div> }
              </div>
            }
            @case ('error') {
              <div class="state-panel">
                <p class="mut" style="margin-bottom:14px">Unable to load holiday destinations.</p>
                <button class="btn sm" (click)="loadDestinations()">Try Again</button>
              </div>
            }
            @case ('ready') {
              @if (destinations().length === 0) {
                <div class="state-panel"><p class="mut">Holiday destinations are coming soon. Tell us where you'd like to go on WhatsApp.</p></div>
              } @else {
                <div class="hd-row" role="group" aria-label="Filter holiday packages by destination">
                  @for (d of destinations(); track d.holidayDestinationId) {
                    <button type="button" class="hd-card" [class.on]="selectedId() === d.holidayDestinationId" [attr.aria-pressed]="selectedId() === d.holidayDestinationId" (click)="selectDestination(d.holidayDestinationId)">
                      <span class="hd-ph">
                        @if (d.coverImageUrl) { <img [src]="d.coverImageUrl | imageUrl" [alt]="d.name" loading="lazy" decoding="async" /> }
                      </span>
                      <span class="hd-cnt">{{ d.packageCount }} {{ d.packageCount === 1 ? 'package' : 'packages' }}</span>
                      <span class="hd-info">
                        <b>{{ d.name }}</b>
                        @if (d.startingPrice !== null) {
                          <span class="hd-price">Starting at &#8377;{{ d.startingPrice | number: '1.0-0' }}</span>
                        } @else if (d.tagline) {
                          <span class="hd-price">{{ d.tagline }}</span>
                        }
                      </span>
                    </button>
                  }
                </div>
              }
            }
          }
        </div>
      </div>
    </section>

    <section id="holiday-packages" class="sect" aria-labelledby="hp-title" #packagesSection>
      <div class="container">
        <div class="hpanel">
          <div class="sechead hp-head">
            <div>
              <h2 id="hp-title"><span class="up-g">Holiday </span><span class="up-o">Packages</span></h2>
              @if (selectedDestination(); as sd) {
                <p class="hp-sub">Showing packages for <b>{{ sd.name }}</b> · <button type="button" class="linkbtn" (click)="selectDestination(null)">Show all</button></p>
              } @else {
                <p class="hp-sub">Custom holidays planned around you — tell us your dates and we'll handle the rest.</p>
              }
            </div>
            @if (packagesState() === 'ready') {
              <span class="hp-count"><b>{{ packages().length }}</b> {{ packages().length === 1 ? 'package' : 'packages' }}</span>
            }
          </div>

          @switch (packagesState()) {
            @case ('loading') {
              <div class="hp-grid">
                @for (i of [1, 2, 3]; track i) { <div class="hp-card skel" aria-hidden="true"><div class="hp-shot"></div><div class="hp-body"></div></div> }
              </div>
            }
            @case ('error') {
              <div class="state-panel">
                <p class="mut" style="margin-bottom:14px">Unable to load holiday packages.</p>
                <button class="btn sm" (click)="loadPackages()">Try Again</button>
              </div>
            }
            @case ('ready') {
              @if (packages().length === 0) {
                <div class="state-panel">
                  <p class="mut" style="margin-bottom:14px">No holiday packages here yet — we can still plan one just for you.</p>
                  <button class="btn sm" (click)="enquire(null)">Plan my holiday on WhatsApp</button>
                </div>
              } @else {
                <div class="hp-grid">
                  @for (p of packages(); track p.holidayPackageId) {
                    <article class="hp-card">
                      <div class="hp-shot">
                        @if (p.coverImageUrl) { <img [src]="p.coverImageUrl | imageUrl" [alt]="p.title" loading="lazy" decoding="async" /> }
                        <span class="hp-dur">{{ p.durationLabel }}</span>
                      </div>
                      <div class="hp-body">
                        @if (p.destinationName) { <span class="hp-dest">{{ p.destinationName }}</span> }
                        <h3>{{ p.title }}</h3>
                        @if (p.summary) { <p class="hp-sum">{{ p.summary }}</p> }
                        @if (p.highlights.length > 0) {
                          <ul class="hp-hl">
                            @for (h of p.highlights.slice(0, 4); track h) { <li>{{ h }}</li> }
                          </ul>
                        }
                        <div class="hp-foot">
                          <div class="hp-price">
                            @if (p.priceFrom !== null) {
                              <small>From</small><b>&#8377;{{ p.priceFrom | number: '1.0-0' }} <small>/ person</small></b>
                            } @else {
                              <b class="hp-por">Price on request</b>
                            }
                          </div>
                          <button type="button" class="btn hp-enq" (click)="enquire(p)">
                            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M21 11.5a8.4 8.4 0 0 1-12.4 7.4L3 20.5l1.6-5.4A8.5 8.5 0 1 1 21 11.5z"></path></svg>
                            Enquire
                          </button>
                        </div>
                      </div>
                    </article>
                  }
                </div>
              }
            }
          }
        </div>
      </div>
    </section>
  `,
  styles: `
    .hpanel { background: #fff; border: 1px solid var(--line); border-radius: var(--radius-card); box-shadow: 0 10px 30px rgba(15,20,22,.06); padding: 26px 36px 30px; }
    .sechead { margin-bottom: 16px; }
    .sechead h2 { font-size: 30px; margin-bottom: 2px; }
    .hp-head { display: flex; align-items: flex-end; justify-content: space-between; gap: 12px; flex-wrap: wrap; }
    .hp-sub { margin: 4px 0 0; font-size: 13.5px; color: var(--muted); }
    .hp-count { display: inline-flex; align-items: baseline; gap: 5px; padding: 8px 14px; border-radius: 999px; background: #FFF1E8; color: var(--orange); font-size: 13px; }
    .hp-count b { font-size: 16px; }
    .linkbtn { border: 0; background: none; padding: 0; color: var(--accent); font: inherit; font-weight: 600; cursor: pointer; }

    /* Destinations: a sideways row of photo cards. */
    .hd-row { display: flex; gap: 14px; overflow-x: auto; scroll-snap-type: x proximity; padding: 2px 2px 10px; scrollbar-width: none; }
    .hd-row::-webkit-scrollbar { display: none; }
    .hd-card { position: relative; flex: 0 0 220px; height: 260px; scroll-snap-align: start; border: 0; border-radius: 16px; overflow: hidden; padding: 0; background: var(--canvas); color: #fff; font: inherit; text-align: left; cursor: pointer; box-shadow: 0 0 0 0 transparent; transition: transform .18s ease, box-shadow .18s ease; }
    .hd-card.skel { cursor: default; }
    .hd-card.on { box-shadow: 0 0 0 3px var(--orange), 0 12px 24px -12px rgba(238,106,42,.7); }
    @media (hover: hover) { .hd-card:not(.skel):hover { transform: translateY(-3px); } }
    .hd-ph { position: absolute; inset: 0; background: linear-gradient(135deg, #CFE7DD, #F6D9C4); }
    .hd-ph img { width: 100%; height: 100%; object-fit: cover; display: block; }
    .hd-ph::after { content: ""; position: absolute; inset: 0; background: linear-gradient(180deg, rgba(0,0,0,0) 40%, rgba(0,0,0,.72) 100%); }
    .hd-cnt { position: absolute; top: 10px; right: 10px; z-index: 1; padding: 3px 9px; border-radius: 999px; background: #fff; color: var(--ink); font-size: 11.5px; font-weight: 700; }
    .hd-info { position: absolute; left: 14px; right: 14px; bottom: 14px; z-index: 1; display: grid; gap: 2px; }
    .hd-info b { font-family: var(--font-heading); font-size: 20px; line-height: 1.15; }
    .hd-price { font-size: 12.5px; opacity: .92; }

    /* Packages: cards like Upcoming Adventures. */
    .hp-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(270px, 1fr)); gap: 18px; }
    .hp-card { display: flex; flex-direction: column; border: 1px solid var(--line); border-radius: 16px; overflow: hidden; background: #fff; box-shadow: 0 8px 22px -16px rgba(15,20,22,.35); transition: transform .18s ease, box-shadow .18s ease; }
    @media (hover: hover) { .hp-card:not(.skel):hover { transform: translateY(-3px); box-shadow: 0 0 0 1px var(--orange), 0 14px 28px -14px rgba(238,106,42,.45); } }
    .hp-shot { position: relative; height: 190px; background: linear-gradient(135deg, #CFE7DD, #F6D9C4); }
    .hp-shot img { width: 100%; height: 100%; object-fit: cover; display: block; }
    .hp-dur { position: absolute; top: 10px; left: 10px; padding: 4px 10px; border-radius: 999px; background: rgba(15,20,22,.72); color: #fff; font-size: 11.5px; font-weight: 700; }
    .hp-body { display: flex; flex-direction: column; gap: 6px; flex: 1; padding: 14px 16px 16px; }
    .hp-card.skel .hp-body { min-height: 150px; }
    .hp-dest { font-size: 11.5px; font-weight: 700; letter-spacing: .05em; text-transform: uppercase; color: var(--accent); }
    .hp-body h3 { margin: 0; font-family: var(--font-heading); font-size: 19px; line-height: 1.2; }
    .hp-sum { margin: 0; font-size: 13px; line-height: 1.55; color: var(--muted); display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
    .hp-hl { display: flex; flex-wrap: wrap; gap: 6px; margin: 2px 0 0; padding: 0; list-style: none; }
    .hp-hl li { padding: 3px 9px; border-radius: 999px; background: var(--accent-soft); color: var(--accent); font-size: 11.5px; font-weight: 600; }
    .hp-foot { display: flex; align-items: center; justify-content: space-between; gap: 10px; margin-top: auto; padding-top: 10px; border-top: 1px dashed var(--line); }
    .hp-price { display: flex; flex-direction: column; line-height: 1.15; white-space: nowrap; }
    .hp-price b { font-size: 19px; }
    .hp-price small { font-size: 11.5px; font-weight: 500; color: var(--muted); }
    .hp-por { font-size: 14px !important; color: var(--ink); }
    .hp-enq { flex: none; display: inline-flex; align-items: center; gap: 6px; padding-left: 14px; padding-right: 14px; background: var(--orange); }
    .hp-enq:hover:not(:disabled) { background: var(--orange-hover); box-shadow: 0 8px 20px rgba(238,106,42,.35); }

    @media (max-width: 900px) { .hpanel { padding: 22px 30px 24px; } }
    @media (max-width: 640px) {
      .hpanel { padding: 16px 14px 16px; }
      .sechead h2 { font-size: 22px; }
      .hd-card { flex-basis: 160px; height: 200px; }
      .hd-info b { font-size: 17px; }
      .hp-grid { grid-template-columns: 1fr; gap: 14px; }
      .hp-shot { height: 180px; }
    }
  `,
})
export class HomeHolidaysComponent implements OnInit {
  private readonly holidays = inject(HolidayService);
  private readonly contactService = inject(ContactService);
  private readonly packagesSection = viewChild<ElementRef<HTMLElement>>('packagesSection');

  readonly destinations = signal<HolidayDestination[]>([]);
  readonly destinationsState = signal<LoadState>('loading');
  readonly packages = signal<HolidayPackage[]>([]);
  readonly packagesState = signal<LoadState>('loading');
  readonly selectedId = signal<number | null>(null);
  readonly selectedDestination = computed(() => this.destinations().find((d) => d.holidayDestinationId === this.selectedId()) ?? null);

  // Loads contact details for the WhatsApp link.
  private readonly contact = this.contactService.get();

  ngOnInit(): void {
    this.loadDestinations();
    this.loadPackages();
  }

  loadDestinations(): void {
    this.destinationsState.set('loading');
    this.holidays.getDestinations().subscribe({
      next: (d) => {
        this.destinations.set(d);
        this.destinationsState.set('ready');
      },
      error: () => this.destinationsState.set('error'),
    });
  }

  // Only the newest request may update the list, so quick taps can't land out of order.
  private packagesSeq = 0;

  loadPackages(): void {
    const seq = ++this.packagesSeq;
    this.packagesState.set('loading');
    this.holidays.getPackages(this.selectedId()).subscribe({
      next: (p) => {
        if (seq !== this.packagesSeq) return;
        this.packages.set(p);
        this.packagesState.set('ready');
      },
      error: () => {
        if (seq === this.packagesSeq) this.packagesState.set('error');
      },
    });
  }

  /** Tapping the chosen destination again shows every package. */
  selectDestination(id: number | null): void {
    this.selectedId.set(this.selectedId() === id ? null : id);
    this.loadPackages();
    this.packagesSection()?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  enquire(p: HolidayPackage | null): void {
    if (!this.contact()?.whatsAppNumber) return;
    const lines = p
      ? [
          "Hi Ghumo Odisha! I'm interested in this holiday package:",
          `${p.title}${p.destinationName ? ' (' + p.destinationName + ')' : ''} — ${p.durationLabel}`,
          'Please share the details and available dates.',
        ]
      : ["Hi Ghumo Odisha! I'd like a custom holiday package. Please help me plan it."];
    window.open(this.contactService.buildWhatsAppLink(lines.join('\n')), '_blank', 'noopener');
  }
}
