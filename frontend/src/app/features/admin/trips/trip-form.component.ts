import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminTripService } from '../../../core/services/admin-trip.service';
import { AdminDestinationService } from '../../../core/services/admin-destination.service';
import { AdminTripDetail, ItineraryDay, ItineraryPoint } from '../../../core/models/trip.model';
import { AdminDestinationListItem } from '../../../core/models/destination.model';
import { TripStatus } from '../../../core/models/enums.model';
import { ToastService } from '../../../core/services/toast.service';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { ImageUrlPipe } from '../../../shared/pipes/image-url.pipe';

@Component({
  selector: 'app-trip-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, FormsModule, RouterLink, StatePanelComponent, ImageUrlPipe],
  templateUrl: './trip-form.component.html',
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
  newSlot = { startDate: '', endDate: '', totalSeats: 10 };

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
        for (const day of t.itineraryDays) {
          this.newPoint[day.itineraryDayId] ??= { time: '', description: '' };
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

    this.saving.set(true);
    const v = { ...this.form.getRawValue(), destinationIds: Array.from(this.selectedDestinationIds()) };

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

  addDateSlot(): void {
    if (!this.tripId || !this.newSlot.startDate || !this.newSlot.endDate || this.newSlot.totalSeats <= 0) {
      this.toast.error('Enter a start date, end date and total seats.');
      return;
    }

    this.tripService.addDateSlot(this.tripId, this.newSlot).subscribe({
      next: () => {
        this.toast.success('Date slot added.');
        this.newSlot = { startDate: '', endDate: '', totalSeats: 10 };
        this.loadTrip(this.tripId!, true);
      },
    });
  }

  deleteDateSlot(id: number): void {
    this.tripService.deleteDateSlot(id).subscribe(() => this.loadTrip(this.tripId!, true));
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
