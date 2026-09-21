import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminTripService } from '../../../core/services/admin-trip.service';
import { AdminTripDetail } from '../../../core/models/trip.model';
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
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  readonly TripStatus = TripStatus;

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

  // add-pickup-point draft
  newPickupPoint = { location: '', time: '' };

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.tripId = idParam ? Number(idParam) : null;

    if (this.tripId) {
      this.loadTrip(this.tripId);
    } else {
      this.loading.set(false);
    }
  }

  loadTrip(id: number): void {
    this.loading.set(true);
    this.tripService.getTrip(id).subscribe({
      next: (t) => {
        this.trip.set(t);
        for (const day of t.itineraryDays) {
          this.newPoint[day.itineraryDayId] ??= { time: '', description: '' };
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
    const v = this.form.getRawValue();

    if (this.tripId) {
      this.tripService.updateTrip(this.tripId, v).subscribe({
        next: () => {
          this.saving.set(false);
          this.toast.success('Trip updated.');
          this.loadTrip(this.tripId!);
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
        this.loadTrip(this.tripId!);
      },
    });
    (event.target as HTMLInputElement).value = '';
  }

  deletePhoto(id: number): void {
    this.tripService.deleteTripPhoto(id).subscribe(() => this.loadTrip(this.tripId!));
  }

  movePhoto(index: number, direction: -1 | 1): void {
    const photos = this.trip()?.photos;
    if (!photos) return;
    const target = photos[index + direction];
    if (!target) return;

    this.tripService.updateTripPhotoOrder(photos[index].tripPhotoId, target.displayOrder).subscribe(() => {
      this.tripService.updateTripPhotoOrder(target.tripPhotoId, photos[index].displayOrder).subscribe(() => this.loadTrip(this.tripId!));
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
        this.loadTrip(this.tripId!);
      },
    });
  }

  deleteHighlight(id: number): void {
    this.tripService.deleteHighlight(id).subscribe(() => this.loadTrip(this.tripId!));
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
        this.loadTrip(this.tripId!);
      },
    });
  }

  deleteDateSlot(id: number): void {
    this.tripService.deleteDateSlot(id).subscribe(() => this.loadTrip(this.tripId!));
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
        this.newDay = { dayNumber: (this.trip()?.itineraryDays.length ?? 0) + 2, title: '', description: '' };
        this.loadTrip(this.tripId!);
      },
    });
  }

  deleteDay(id: number): void {
    this.tripService.deleteItineraryDay(id).subscribe(() => this.loadTrip(this.tripId!));
  }

  addPoint(dayId: number): void {
    const draft = this.newPoint[dayId];
    if (!draft?.time || !draft?.description) {
      this.toast.error('Enter a time and description for the point.');
      return;
    }

    this.tripService.addItineraryPoint(dayId, draft).subscribe({
      next: () => {
        this.newPoint[dayId] = { time: '', description: '' };
        this.loadTrip(this.tripId!);
      },
    });
  }

  deletePoint(id: number): void {
    this.tripService.deleteItineraryPoint(id).subscribe(() => this.loadTrip(this.tripId!));
  }

  // ---------- room photos ----------

  onRoomPhotoSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file || !this.tripId) return;

    this.tripService.addRoomPhoto(this.tripId, file).subscribe({
      next: () => {
        this.toast.success('Room photo added.');
        this.loadTrip(this.tripId!);
      },
    });
    (event.target as HTMLInputElement).value = '';
  }

  deleteRoomPhoto(id: number): void {
    this.tripService.deleteRoomPhoto(id).subscribe(() => this.loadTrip(this.tripId!));
  }

  // ---------- vehicle photos ----------

  onVehiclePhotoSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file || !this.tripId) return;

    this.tripService.addVehiclePhoto(this.tripId, file).subscribe({
      next: () => {
        this.toast.success('Vehicle photo added.');
        this.loadTrip(this.tripId!);
      },
    });
    (event.target as HTMLInputElement).value = '';
  }

  deleteVehiclePhoto(id: number): void {
    this.tripService.deleteVehiclePhoto(id).subscribe(() => this.loadTrip(this.tripId!));
  }

  // ---------- pickup points ----------

  addPickupPoint(): void {
    if (!this.tripId || !this.newPickupPoint.location || !this.newPickupPoint.time) {
      this.toast.error('Enter a pickup location and time.');
      return;
    }

    this.tripService.addPickupPoint(this.tripId, this.newPickupPoint).subscribe({
      next: () => {
        this.toast.success('Pickup point added.');
        this.newPickupPoint = { location: '', time: '' };
        this.loadTrip(this.tripId!);
      },
    });
  }

  deletePickupPoint(id: number): void {
    this.tripService.deletePickupPoint(id).subscribe(() => this.loadTrip(this.tripId!));
  }
}
