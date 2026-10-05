import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AdminDashboardService } from '../../../core/services/admin-dashboard.service';
import { AdminOrganizerService } from '../../../core/services/admin-organizer.service';
import { AdminHeroService } from '../../../core/services/admin-hero.service';
import { ContactService } from '../../../core/services/contact.service';
import { HeroService } from '../../../core/services/hero.service';
import { ToastService } from '../../../core/services/toast.service';
import { Dashboard } from '../../../core/models/dashboard.model';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { ImageUrlPipe } from '../../../shared/pipes/image-url.pipe';
import { CarsDashboardSectionComponent } from '../cars/cars-dashboard-section.component';
import { TravelMomentsPanelComponent } from './travel-moments-panel.component';

type LoadState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, StatePanelComponent, ImageUrlPipe, CarsDashboardSectionComponent, TravelMomentsPanelComponent],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent implements OnInit {
  private readonly dashboardService = inject(AdminDashboardService);
  private readonly organizerService = inject(AdminOrganizerService);
  private readonly heroService = inject(AdminHeroService);
  private readonly contactService = inject(ContactService);
  private readonly heroPhotoService = inject(HeroService);
  private readonly toast = inject(ToastService);

  readonly state = signal<LoadState>('loading');
  readonly dashboard = signal<Dashboard | null>(null);
  readonly contact = this.contactService.get();
  readonly heroPhoto = this.heroPhotoService.get('home');
  readonly officePhoto = this.heroPhotoService.get('office');
  readonly uploadingPhoto = signal(false);
  readonly uploadingHeroPhoto = signal(false);
  readonly uploadingOfficePhoto = signal(false);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.dashboardService.getDashboard().subscribe({
      next: (data) => {
        this.dashboard.set(data);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  onOrganizerPhotoSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.uploadingPhoto.set(true);
    this.organizerService.setPhoto(file).subscribe({
      next: () => {
        this.uploadingPhoto.set(false);
        this.toast.success('Organizer photo updated.');
        window.location.reload();
      },
      error: () => this.uploadingPhoto.set(false),
    });
    (event.target as HTMLInputElement).value = '';
  }

  onHeroPhotoSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.uploadingHeroPhoto.set(true);
    this.heroService.setPhoto(file).subscribe({
      next: () => {
        this.uploadingHeroPhoto.set(false);
        this.toast.success('Dashboard hero photo updated.');
        window.location.reload();
      },
      error: () => this.uploadingHeroPhoto.set(false),
    });
    (event.target as HTMLInputElement).value = '';
  }

  onOfficePhotoSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0];
    if (!file) return;

    this.uploadingOfficePhoto.set(true);
    this.heroService.setPhoto(file, 'office').subscribe({
      next: () => {
        this.uploadingOfficePhoto.set(false);
        this.toast.success('Office photo updated.');
        window.location.reload();
      },
      error: () => this.uploadingOfficePhoto.set(false),
    });
    (event.target as HTMLInputElement).value = '';
  }
}
