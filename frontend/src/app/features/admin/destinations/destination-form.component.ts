import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AdminDestinationService } from '../../../core/services/admin-destination.service';
import { AdminDestinationDetail } from '../../../core/models/destination.model';
import { ToastService } from '../../../core/services/toast.service';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { ImageUrlPipe } from '../../../shared/pipes/image-url.pipe';

@Component({
  selector: 'app-destination-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, StatePanelComponent, ImageUrlPipe],
  templateUrl: './destination-form.component.html',
})
export class DestinationFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly destinationService = inject(AdminDestinationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  destinationId: number | null = null;
  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly uploadingHero = signal(false);
  readonly destination = signal<AdminDestinationDetail | null>(null);

  // Picked on the "create" screen, before a destinationId exists — uploaded right after the
  // create call succeeds so the admin can attach a photo in one step instead of two.
  readonly pendingHeroImageFile = signal<File | null>(null);
  readonly pendingHeroImagePreviewUrl = signal<string | null>(null);

  private slugTouched = false;

  readonly form = this.fb.nonNullable.group({
    name: ['', Validators.required],
    slug: ['', [Validators.required, Validators.pattern('^[a-z0-9]+(-[a-z0-9]+)*$')]],
    tagline: [''],
    region: [''],
    aboutText: [''],
    bestSeason: [''],
    distanceFromBhubaneswar: [''],
    idealDuration: [''],
    knownFor: [''],
    displayOrder: [0, [Validators.required, Validators.min(0)]],
    isActive: [true],
  });

  ngOnInit(): void {
    const idParam = this.route.snapshot.paramMap.get('id');
    this.destinationId = idParam ? Number(idParam) : null;

    this.form.controls.name.valueChanges.subscribe((value) => this.onNameChange(value));
    this.form.controls.slug.valueChanges.subscribe(() => this.onSlugChange());

    if (this.destinationId) {
      this.slugTouched = true;
      this.loadDestination(this.destinationId);
    } else {
      this.loading.set(false);
    }
  }

  loadDestination(id: number): void {
    this.loading.set(true);
    this.destinationService.getDestination(id).subscribe({
      next: (d) => {
        this.destination.set(d);
        this.form.patchValue({
          name: d.name,
          slug: d.slug,
          tagline: d.tagline ?? '',
          region: d.region ?? '',
          aboutText: d.aboutText ?? '',
          bestSeason: d.bestSeason ?? '',
          distanceFromBhubaneswar: d.distanceFromBhubaneswar ?? '',
          idealDuration: d.idealDuration ?? '',
          knownFor: d.knownFor ?? '',
          displayOrder: d.displayOrder,
          isActive: d.isActive,
        });
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onNameChange(value: string): void {
    if (this.slugTouched) return;
    const slug = value
      .toLowerCase()
      .trim()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/(^-|-$)/g, '');
    this.form.patchValue({ slug }, { emitEvent: false });
  }

  onSlugChange(): void {
    this.slugTouched = true;
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const v = this.form.getRawValue();
    const request = {
      name: v.name,
      slug: v.slug,
      tagline: v.tagline || null,
      region: v.region || null,
      aboutText: v.aboutText || null,
      bestSeason: v.bestSeason || null,
      distanceFromBhubaneswar: v.distanceFromBhubaneswar || null,
      idealDuration: v.idealDuration || null,
      knownFor: v.knownFor || null,
      isActive: v.isActive,
      displayOrder: v.displayOrder,
    };

    if (this.destinationId) {
      this.destinationService.updateDestination(this.destinationId, request).subscribe({
        next: () => {
          this.saving.set(false);
          this.toast.success('Destination updated.');
          this.loadDestination(this.destinationId!);
        },
        error: () => this.saving.set(false),
      });
    } else {
      this.destinationService.createDestination(request).subscribe({
        next: (res) => {
          const pendingFile = this.pendingHeroImageFile();
          if (!pendingFile) {
            this.saving.set(false);
            this.toast.success('Destination created.');
            this.router.navigate(['/admin/destinations', res.destinationId, 'edit']);
            return;
          }

          this.destinationService.updateHeroImage(res.destinationId, pendingFile).subscribe({
            next: () => {
              this.saving.set(false);
              this.toast.success('Destination created with hero photo.');
              this.router.navigate(['/admin/destinations', res.destinationId, 'edit']);
            },
            error: () => {
              this.saving.set(false);
              this.toast.error('Destination created, but the hero photo failed to upload. Add it from the edit page.');
              this.router.navigate(['/admin/destinations', res.destinationId, 'edit']);
            },
          });
        },
        error: () => this.saving.set(false),
      });
    }
  }

  // Create screen: just stages the file (with a local preview) — actually uploaded once the
  // destination exists, right after save() creates it.
  onPendingHeroImageSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.pendingHeroImageFile.set(file);
    this.pendingHeroImagePreviewUrl.set(URL.createObjectURL(file));
  }

  onHeroImageSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file || !this.destinationId) return;

    this.uploadingHero.set(true);
    this.destinationService.updateHeroImage(this.destinationId, file).subscribe({
      next: () => {
        this.uploadingHero.set(false);
        this.toast.success('Hero photo updated.');
        this.loadDestination(this.destinationId!);
      },
      error: () => this.uploadingHero.set(false),
    });
    (event.target as HTMLInputElement).value = '';
  }
}
