import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminTripService } from '../../../core/services/admin-trip.service';
import { AdminDestinationService } from '../../../core/services/admin-destination.service';
import { AdminTripDetail, DateSlot, ItineraryDay, ItineraryPoint, PickupPoint } from '../../../core/models/trip.model';
import { TRIP_CATEGORIES } from '../../../shared/utils/trip-categories';
import { toLocalDateKey } from '../../../shared/utils/date-key';
import { BookingStateSwitchComponent } from './booking-state-switch.component';
import { AdminDestinationListItem } from '../../../core/models/destination.model';
import { TripStatus } from '../../../core/models/enums.model';
import { ToastService } from '../../../core/services/toast.service';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { ImageUrlPipe } from '../../../shared/pipes/image-url.pipe';

@Component({
  selector: 'app-trip-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, RouterLink, StatePanelComponent, ImageUrlPipe, BookingStateSwitchComponent],
  templateUrl: './trip-form.component.html',
  styles: `
    .places-chips { gap: 8px; margin: 4px 0 8px; }
    .places-chips .chip { padding: 6px 6px 6px 12px; }
    .cat-pick { gap: 8px; margin-top: 4px; }
    .cat-opt { display: inline-flex; align-items: center; gap: 6px; padding: 7px 13px; border-radius: 999px; border: 1.5px solid var(--line); background: #fff; color: var(--ink); font: inherit; font-size: 12.5px; font-weight: 600; cursor: pointer; transition: background .15s, border-color .15s, color .15s; }
    .cat-opt:hover { border-color: var(--c-fg); }
    .cat-opt.on { background: var(--c-bg); border-color: var(--c-fg); color: var(--c-fg); }
    .bstate { border-left: 4px solid var(--accent); }
    .slot-f { grid-template-columns: repeat(4, minmax(0, 1fr)); }
    .slot-f.slot-add { grid-template-columns: repeat(4, minmax(0, 1fr)) auto; }
    @media (max-width: 900px) { .slot-f, .slot-f.slot-add { grid-template-columns: 1fr 1fr; } }
    @media (max-width: 520px) { .slot-f, .slot-f.slot-add { grid-template-columns: 1fr; } }
    @media (max-width: 640px) { .pk-row .sel[style*="width:110px"] { width: auto !important; } }
  `,
})
export class TripFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly tripService = inject(AdminTripService);
  private readonly destinationService = inject(AdminDestinationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly TripStatus = TripStatus;

  readonly destinationOptions = signal<AdminDestinationListItem[]>([]);
  readonly selectedDestinationIds = signal<Set<number>>(new Set());

  // "Places covered" list (saved with the basic info) and the place being typed.
  readonly placesCovered = signal<string[]>([]);
  newPlace = '';

  // Home page filter categories (saved with the basic info).
  readonly tripCategories = TRIP_CATEGORIES;
  readonly selectedCategories = signal<Set<string>>(new Set());

  // pickup and itinerary-point times, every 15 minutes, e.g. { value: '09:15', label: '9:15 AM' }
  readonly quarterHourOptions = Array.from({ length: 96 }, (_, i) => {
    const value = `${Math.floor(i / 4).toString().padStart(2, '0')}:${((i % 4) * 15).toString().padStart(2, '0')}`;
    return { value, label: this.to12Hour(value) };
  });

  tripId: number | null = null;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly trip = signal<AdminTripDetail | null>(null);

  readonly form = this.fb.nonNullable.group({
    title: ['', Validators.required],
    description: ['', Validators.required],
    amountPerPerson: [0, [Validators.required, Validators.min(0)]],
    // Both or neither: the trip's own days / nights (a night departure isn't a day). Empty = counted from the dates.
    durationDays: this.fb.control<number | null>(null),
    durationNights: this.fb.control<number | null>(null),
    includesBreakfast: [false],
    includesLunch: [false],
    includesDinner: [false],
    includesStay: [false],
    includesCoordinator: [false],
    includesAcVehicle: [false],
    includesPushbackVehicle: [false],
    includesCamping: [false],
    includesBonfire: [false],
    includesMusicalNight: [false],
    includesSwimmingPool: [false],
    allowCoupons: [true],
    status: [TripStatus.Active],
  });

  // add-highlight draft
  newHighlight = { placeName: '', description: '' };
  newHighlightFile: File | null = null;

  // add-date-slot draft
  newSlot: { startDate: string; endDate: string; totalSeats: number; pricePerPerson: number | null } = { startDate: '', endDate: '', totalSeats: 10, pricePerPerson: null };

  // inline edit of a saved date slot
  readonly editingSlotId = signal<number | null>(null);
  slotDraft: { startDate: string; endDate: string; totalSeats: number; pricePerPerson: number | null } = { startDate: '', endDate: '', totalSeats: 0, pricePerPerson: null };

  // add-itinerary-day draft
  newDay = { dayNumber: 1, title: '', description: '' };

  // add-point draft, keyed by dayId
  newPoint: Record<number, { time: string; description: string }> = {};

  // inline edit of a saved day / point (one at a time)
  readonly editingDayId = signal<number | null>(null);
  dayDraft = { dayNumber: 1, title: '', description: '' };
  readonly editingPointId = signal<number | null>(null);
  pointDraft = { time: '', description: '' };

  // add-pickup-point draft
  newPickupPoint = { location: '', time: '' };

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.tripId = idParam ? Number(idParam) : null;

    this.destinationService.getAllForPicker().subscribe((d) => this.destinationOptions.set(d));

    if (this.tripId) {
      this.loadTrip(this.tripId);
    } else {
      this.loading.set(false);
    }
  }

  /** Adds what's typed (one place, or several separated by commas), skipping repeats. */
  addPlaces(text = this.newPlace): void {
    const typed = text.split(/[,\n]/).map((p) => p.trim()).filter((p) => p.length > 0);
    if (typed.length === 0) return;
    this.placesCovered.update((current) => {
      const next = [...current];
      for (const place of typed) {
        if (!next.some((p) => p.toLowerCase() === place.toLowerCase())) next.push(place.slice(0, 80));
      }
      return next;
    });
    this.newPlace = '';
  }

  onPlacesPaste(event: ClipboardEvent): void {
    const text = event.clipboardData?.getData('text') ?? '';
    if (!/[,\n]/.test(text)) return;
    event.preventDefault();
    this.addPlaces(this.newPlace + text);
  }

  removePlace(place: string): void {
    this.placesCovered.update((current) => current.filter((p) => p !== place));
  }

  toggleCategory(key: string): void {
    this.selectedCategories.update((current) => {
      const next = new Set(current);
      if (next.has(key)) next.delete(key);
      else next.add(key);
      return next;
    });
  }

  toggleDestination(destinationId: number, checked: boolean): void {
    this.selectedDestinationIds.update((current) => {
      const next = new Set(current);
      if (checked) next.add(destinationId);
      else next.delete(destinationId);
      return next;
    });
  }

  // `silent` skips the full-page loading state — used after add/delete actions so the
  // form doesn't collapse to a spinner and reset scroll to top on every refresh.
  loadTrip(id: number, silent = false): void {
    if (!silent) {
      this.loading.set(true);
    }
    this.tripService.getTrip(id).subscribe({
      next: (t) => {
        this.trip.set(t);
        this.selectedDestinationIds.set(new Set(t.destinationIds));
        this.placesCovered.set(t.placesCovered ?? []);
        this.selectedCategories.set(new Set(t.categories ?? []));
        for (const day of t.itineraryDays) {
          this.newPoint[day.itineraryDayId] ??= { time: '', description: '' };
          // Next point starts just after this day's last one, so the time list opens in the right place.
          const draft = this.newPoint[day.itineraryDayId];
          if (!draft.time && !draft.description) draft.time = this.nextPointTime(day);
        }
        // Suggest the next free day number (Day 1 when empty) unless a new day is half-typed.
        if (!this.newDay.title && !this.newDay.description) {
          this.newDay.dayNumber = Math.max(0, ...t.itineraryDays.map((d) => d.dayNumber)) + 1;
        }
        this.form.patchValue({
          title: t.title,
          description: t.description,
          amountPerPerson: t.amountPerPerson,
          includesBreakfast: t.inclusions.breakfast,
          includesLunch: t.inclusions.lunch,
          includesDinner: t.inclusions.dinner,
          includesStay: t.inclusions.stay,
          includesCoordinator: t.inclusions.coordinator,
          includesAcVehicle: t.inclusions.acVehicle,
          includesPushbackVehicle: t.inclusions.pushbackVehicle,
          includesCamping: t.inclusions.camping,
          includesBonfire: t.inclusions.bonfire,
          includesMusicalNight: t.inclusions.musicalNight,
          includesSwimmingPool: t.inclusions.swimmingPool,
          allowCoupons: t.allowCoupons,
          durationDays: t.durationDays ?? null,
          durationNights: t.durationNights ?? null,
          status: t.status,
        });
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  saveBasicInfo(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const raw = this.form.getRawValue();
    const days = this.countOrNull(raw.durationDays);
    const nights = this.countOrNull(raw.durationNights);
    if ((days === null) !== (nights === null)) {
      this.toast.error('Enter both days and nights, or leave both empty to count them from the dates.');
      return;
    }

    this.saving.set(true);
    this.addPlaces(); // a place typed but not yet added still counts
    const v = { ...raw, durationDays: days, durationNights: nights,
      destinationIds: Array.from(this.selectedDestinationIds()), placesCovered: this.placesCovered(),
      // Kept in the home page filter's order.
      categories: TRIP_CATEGORIES.map((c) => c.key).filter((k) => this.selectedCategories().has(k)) };

    if (this.tripId) {
      this.tripService.updateTrip(this.tripId, v).subscribe({
        next: () => {
          this.saving.set(false);
          this.toast.success('Trip updated.');
          this.loadTrip(this.tripId!, true);
        },
        error: () => this.saving.set(false),
      });
    } else {
      this.tripService.createTrip(v).subscribe({
        next: (res) => {
          this.saving.set(false);
          this.toast.success('Trip created. Now add photos, dates and itinerary.');
          this.router.navigate(['/admin/trips', res.tripId, 'edit']);
        },
        error: () => this.saving.set(false),
      });
    }
  }

  // ---------- photos ----------

  onPhotoSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file || !this.tripId) return;

    this.tripService.addTripPhoto(this.tripId, file).subscribe({
      next: () => {
        this.toast.success('Photo added.');
        this.loadTrip(this.tripId!, true);
      },
    });
    (event.target as HTMLInputElement).value = '';
  }

  deletePhoto(id: number): void {
    this.tripService.deleteTripPhoto(id).subscribe(() => this.loadTrip(this.tripId!, true));
  }

  movePhoto(index: number, direction: -1 | 1): void {
    const photos = this.trip()?.photos;
    if (!photos) return;
    const target = photos[index + direction];
    if (!target) return;

    this.tripService.updateTripPhotoOrder(photos[index].tripPhotoId, target.displayOrder).subscribe(() => {
      this.tripService.updateTripPhotoOrder(target.tripPhotoId, photos[index].displayOrder).subscribe(() => this.loadTrip(this.tripId!, true));
    });
  }

  // ---------- highlights ----------

  onHighlightFileSelected(event: Event): void {
    this.newHighlightFile = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  addHighlight(): void {
    if (!this.tripId || !this.newHighlightFile || !this.newHighlight.placeName || !this.newHighlight.description) {
      this.toast.error('Place name, description and a photo are all required.');
      return;
    }

    this.tripService.addHighlight(this.tripId, this.newHighlight.placeName, this.newHighlight.description, this.newHighlightFile).subscribe({
      next: () => {
        this.toast.success('Highlight added.');
        this.newHighlight = { placeName: '', description: '' };
        this.newHighlightFile = null;
        this.loadTrip(this.tripId!, true);
      },
    });
  }

  deleteHighlight(id: number): void {
    this.tripService.deleteHighlight(id).subscribe(() => this.loadTrip(this.tripId!, true));
  }

  // ---------- date slots ----------

  /** Today (local) as "yyyy-mm-dd" — the earliest start date the picker offers. */
  readonly todayKey = toLocalDateKey(new Date());

  /**
   * Picking a start date fills (or fixes) the end date: start + the itinerary's length, so the end
   * date's calendar opens on the right month instead of today's and is usually already right.
   */
  onStartDateChange(draft: { startDate: string; endDate: string }): void {
    if (!draft.startDate) return;
    if (!draft.endDate || draft.endDate < draft.startDate) {
      const days = Math.max(1, Math.max(0, ...(this.trip()?.itineraryDays ?? []).map((d) => d.dayNumber)));
      const end = new Date(`${draft.startDate}T00:00:00`);
      end.setDate(end.getDate() + days - 1);
      draft.endDate = toLocalDateKey(end);
    }
  }

  /** Empty / non-numeric = not set; days and nights are whole numbers. */
  private countOrNull(value: number | string | null): number | null {
    if (value === null || value === '') return null;
    const n = Number(value);
    return Number.isInteger(n) && n >= 0 ? n : null;
  }

  /** Empty / zero / non-numeric price = use the trip's price. */
  private priceOrNull(value: number | string | null): number | null {
    const n = Number(value);
    return value === null || value === '' || !Number.isFinite(n) || n <= 0 ? null : n;
  }

  // One price for every date in a month.
  monthPrice: { month: string; price: number | null } = { month: '', price: null };

  /** Months that have dates, for the month price picker: { key: '2026-11', label: 'November 2026', count }. */
  slotMonthsForPricing(t: AdminTripDetail): { key: string; label: string; count: number }[] {
    const map = new Map<string, number>();
    for (const s of t.dateSlots) map.set(s.startDate.slice(0, 7), (map.get(s.startDate.slice(0, 7)) ?? 0) + 1);
    return Array.from(map.entries())
      .sort(([a], [b]) => a.localeCompare(b))
      .map(([key, count]) => ({ key, count, label: new Date(`${key}-01T00:00:00`).toLocaleDateString('en-IN', { month: 'long', year: 'numeric' }) }));
  }

  applyMonthPrice(): void {
    if (!this.tripId || !this.monthPrice.month) {
      this.toast.error('Choose a month.');
      return;
    }
    const price = this.priceOrNull(this.monthPrice.price);
    this.tripService.setMonthPrice(this.tripId, this.monthPrice.month, price).subscribe({
      next: (message) => {
        this.toast.success(message);
        this.monthPrice = { month: '', price: null };
        this.loadTrip(this.tripId!, true);
      },
    });
  }

  addDateSlot(): void {
    if (!this.tripId || !this.newSlot.startDate || !this.newSlot.endDate || this.newSlot.totalSeats <= 0) {
      this.toast.error('Enter a start date, end date and total seats.');
      return;
    }

    this.tripService.addDateSlot(this.tripId, { ...this.newSlot, pricePerPerson: this.priceOrNull(this.newSlot.pricePerPerson) }).subscribe({
      next: () => {
        this.toast.success('Date slot added.');
        this.newSlot = { startDate: '', endDate: '', totalSeats: this.newSlot.totalSeats, pricePerPerson: null };
        this.loadTrip(this.tripId!, true);
      },
    });
  }

  /** Seats held by paid bookings — the backend refuses a total below this. */
  bookedSeats(s: DateSlot): number {
    return s.totalSeats - s.availableSeats;
  }

  startEditDateSlot(s: DateSlot): void {
    this.slotDraft = { startDate: s.startDate, endDate: s.endDate, totalSeats: s.totalSeats, pricePerPerson: s.slotPrice ?? null };
    this.editingSlotId.set(s.tripDateSlotId);
  }

  saveDateSlot(s: DateSlot): void {
    if (!this.slotDraft.startDate || !this.slotDraft.endDate || !this.slotDraft.totalSeats || this.slotDraft.totalSeats <= 0) {
      this.toast.error('Enter a start date, end date and total seats.');
      return;
    }
    if (this.slotDraft.totalSeats < this.bookedSeats(s)) {
      this.toast.error(`Total seats can't be below the ${this.bookedSeats(s)} already booked.`);
      return;
    }

    this.tripService.updateDateSlot(s.tripDateSlotId, { ...this.slotDraft, pricePerPerson: this.priceOrNull(this.slotDraft.pricePerPerson), status: s.status }).subscribe({
      next: () => {
        this.toast.success('Date slot updated.');
        this.editingSlotId.set(null);
        this.loadTrip(this.tripId!, true);
      },
    });
  }

  /** The API refuses (and the error toast says why) while the date has active bookings, a completed paid trip or a pending refund. */
  deleteDateSlot(s: DateSlot): void {
    if (!confirm(`Delete the ${s.startDate} departure? This can't be undone.`)) return;
    this.tripService.deleteDateSlot(s.tripDateSlotId).subscribe(() => {
      this.toast.success('Date slot deleted.');
      this.loadTrip(this.tripId!, true);
    });
  }

  // ---------- itinerary ----------

  addDay(): void {
    if (!this.tripId || !this.newDay.title || !this.newDay.description) {
      this.toast.error('Enter a day title and description.');
      return;
    }

    this.tripService.addItineraryDay(this.tripId, this.newDay).subscribe({
      next: () => {
        this.toast.success('Itinerary day added.');
        this.newDay = { dayNumber: this.newDay.dayNumber + 1, title: '', description: '' };
        this.loadTrip(this.tripId!, true);
      },
    });
  }

  deleteDay(id: number): void {
    this.tripService.deleteItineraryDay(id).subscribe(() => this.loadTrip(this.tripId!, true));
  }

  addPoint(dayId: number): void {
    const draft = this.newPoint[dayId];
    if (!draft?.time || !draft?.description) {
      this.toast.error('Enter a time and description for the point.');
      return;
    }

    this.tripService.addItineraryPoint(dayId, { ...draft, time: this.to12Hour(draft.time) }).subscribe({
      next: () => {
        this.newPoint[dayId] = { time: '', description: '' };
        this.loadTrip(this.tripId!, true);
      },
    });
  }

  /** 15 minutes after the day's last point ("08:00 PM" → "20:15"); 6:00 AM for an empty day. */
  private nextPointTime(day: ItineraryDay): string {
    const times = day.points.map((p) => this.to24Hour(p.time)).filter((t) => t.length > 0);
    if (times.length === 0) return '06:00';
    const [h, m] = times[times.length - 1].split(':').map(Number);
    const next = (h * 60 + m + 15) % (24 * 60);
    return `${String(Math.floor(next / 60)).padStart(2, '0')}:${String(next % 60).padStart(2, '0')}`;
  }

  deletePoint(id: number): void {
    this.tripService.deleteItineraryPoint(id).subscribe(() => this.loadTrip(this.tripId!, true));
  }

  startEditDay(d: ItineraryDay): void {
    this.editingPointId.set(null);
    this.dayDraft = { dayNumber: d.dayNumber, title: d.title, description: d.description };
    this.editingDayId.set(d.itineraryDayId);
  }

  saveDay(d: ItineraryDay): void {
    if (!this.dayDraft.dayNumber || this.dayDraft.dayNumber < 1 || !this.dayDraft.title.trim() || !this.dayDraft.description.trim()) {
      this.toast.error('Enter a day number, title and description.');
      return;
    }
    this.tripService
      .updateItineraryDay(d.itineraryDayId, { ...this.dayDraft, displayOrder: d.displayOrder })
      .subscribe({
        next: () => {
          this.toast.success('Itinerary day updated.');
          this.editingDayId.set(null);
          this.loadTrip(this.tripId!, true);
        },
      });
  }

  startEditPoint(p: ItineraryPoint): void {
    this.editingDayId.set(null);
    this.pointDraft = { time: this.to24Hour(p.time), description: p.description };
    this.editingPointId.set(p.itineraryPointId);
  }

  savePoint(p: ItineraryPoint): void {
    if (!this.pointDraft.time || !this.pointDraft.description.trim()) {
      this.toast.error('Enter a time and description for the point.');
      return;
    }
    this.tripService
      .updateItineraryPoint(p.itineraryPointId, { time: this.to12Hour(this.pointDraft.time), description: this.pointDraft.description, displayOrder: p.displayOrder })
      .subscribe({
        next: () => {
          this.toast.success('Itinerary point updated.');
          this.editingPointId.set(null);
          this.loadTrip(this.tripId!, true);
        },
      });
  }

  // ---------- room photos ----------

  onRoomPhotoSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file || !this.tripId) return;

    this.tripService.addRoomPhoto(this.tripId, file).subscribe({
      next: () => {
        this.toast.success('Room photo added.');
        this.loadTrip(this.tripId!, true);
      },
    });
    (event.target as HTMLInputElement).value = '';
  }

  deleteRoomPhoto(id: number): void {
    this.tripService.deleteRoomPhoto(id).subscribe(() => this.loadTrip(this.tripId!, true));
  }

  // ---------- vehicle photos ----------

  onVehiclePhotoSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file || !this.tripId) return;

    this.tripService.addVehiclePhoto(this.tripId, file).subscribe({
      next: () => {
        this.toast.success('Vehicle photo added.');
        this.loadTrip(this.tripId!, true);
      },
    });
    (event.target as HTMLInputElement).value = '';
  }

  deleteVehiclePhoto(id: number): void {
    this.tripService.deleteVehiclePhoto(id).subscribe(() => this.loadTrip(this.tripId!, true));
  }

  // ---------- itinerary PDF ----------

  readonly uploadingItineraryPdf = signal(false);

  onItineraryPdfSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file || !this.tripId) return;

    this.uploadingItineraryPdf.set(true);
    this.tripService.uploadItineraryPdf(this.tripId, file).subscribe({
      next: () => {
        this.uploadingItineraryPdf.set(false);
        this.toast.success('Itinerary PDF uploaded.');
        this.loadTrip(this.tripId!, true);
      },
      error: () => this.uploadingItineraryPdf.set(false),
    });
    (event.target as HTMLInputElement).value = '';
  }

  deleteItineraryPdf(): void {
    if (!this.tripId) return;
    this.tripService.deleteItineraryPdf(this.tripId).subscribe(() => this.loadTrip(this.tripId!, true));
  }

  // ---------- pickup points ----------

  addPickupPoint(): void {
    if (!this.tripId || !this.newPickupPoint.location || !this.newPickupPoint.time) {
      this.toast.error('Enter a pickup location and time.');
      return;
    }

    this.tripService.addPickupPoint(this.tripId, { ...this.newPickupPoint, time: this.to12Hour(this.newPickupPoint.time) }).subscribe({
      next: () => {
        this.toast.success('Pickup point added.');
        this.newPickupPoint = { location: '', time: '' };
        this.loadTrip(this.tripId!, true);
      },
    });
  }

  readonly editingPickupId = signal<number | null>(null);
  pickupDraft = { location: '', time: '' };

  startEditPickupPoint(p: PickupPoint): void {
    this.pickupDraft = { location: p.location, time: this.to24Hour(p.time) };
    this.editingPickupId.set(p.pickupPointId);
  }

  savePickupPoint(id: number, displayOrder: number): void {
    const location = this.pickupDraft.location.trim();
    if (!location || !this.pickupDraft.time) {
      this.toast.error('Enter a pickup location and time.');
      return;
    }
    this.tripService.updatePickupPoint(id, { location, time: this.to12Hour(this.pickupDraft.time), displayOrder }).subscribe({
      next: () => {
        this.toast.success('Pickup point updated.');
        this.editingPickupId.set(null);
        this.loadTrip(this.tripId!, true);
      },
    });
  }

  deletePickupPoint(id: number): void {
    this.tripService.deletePickupPoint(id).subscribe(() => this.loadTrip(this.tripId!, true));
  }

  // converts a native <input type="time"> value ("HH:mm", 24h) into the
  // "h:mm AM/PM" string stored and shown to customers
  private to12Hour(time24: string): string {
    const [hoursStr, minutes] = time24.split(':');
    const hours = Number(hoursStr);
    const period = hours >= 12 ? 'PM' : 'AM';
    const hour12 = hours % 12 === 0 ? 12 : hours % 12;
    return `${hour12}:${minutes} ${period}`;
  }

  // "7:30 PM" → "19:30", so a saved point's time can be picked again in the edit dropdown.
  // Anything that isn't in that shape comes back empty and the admin re-picks the time.
  private to24Hour(time12: string): string {
    const match = /^(\d{1,2}):(\d{2})\s*(AM|PM)$/i.exec(time12.trim());
    if (!match) return '';
    let hours = Number(match[1]) % 12;
    if (match[3].toUpperCase() === 'PM') hours += 12;
    return `${hours.toString().padStart(2, '0')}:${match[2]}`;
  }
}
