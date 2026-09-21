import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { PublicTripService } from '../../core/services/public-trip.service';
import { ContactService } from '../../core/services/contact.service';
import { TripSummary } from '../../core/models/trip.model';
import { TripCardComponent } from '../../shared/components/trip-card.component';
import { DestinationCardComponent } from '../../shared/components/destination-card.component';
import { FeatureCardComponent, FeatureIcon } from '../../shared/components/feature-card.component';
import { ImageUrlPipe } from '../../shared/pipes/image-url.pipe';

type LoadState = 'loading' | 'ready' | 'error';

interface Destination {
  name: string;
  tagline: string;
  gradient: string;
}

interface Feature {
  icon: FeatureIcon;
  title: string;
  description: string;
}

const DESTINATION_GRADIENTS = [
  'linear-gradient(135deg, #0F6F5C, #0A4C3F)',
  'linear-gradient(135deg, #0F1416, #2B3538)',
  'linear-gradient(135deg, #3A5B7A, #22405A)',
  'linear-gradient(135deg, #6A7478, #454C4F)',
];

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterLink, TripCardComponent, DestinationCardComponent, FeatureCardComponent, ImageUrlPipe],
  templateUrl: './dashboard.component.html',
})
export class DashboardComponent implements OnInit {
  private readonly tripService = inject(PublicTripService);
  private readonly contactService = inject(ContactService);

  readonly contact = this.contactService.get();

  readonly state = signal<LoadState>('loading');
  private readonly trips = signal<TripSummary[]>([]);

  readonly upcomingTrips = computed(() => this.trips().filter((t) => this.hasUpcomingSlot(t)));

  private readonly heroImageCandidates = computed(() =>
    this.trips()
      .map((t) => t.coverImageUrl)
      .filter((url): url is string => !!url),
  );
  private readonly heroImageIndex = signal(0);
  readonly heroImage = computed(() => this.heroImageCandidates()[this.heroImageIndex()] ?? null);

  onHeroImageError(): void {
    this.heroImageIndex.update((i) => i + 1);
  }

  // Six destinations for a clean 3x2 grid. The spec's original list also included "Araku",
  // which is in Andhra Pradesh, not Odisha — dropped rather than substituted since the
  // remaining six are exactly the Odisha places named in the request.
  readonly destinations: Destination[] = [
    { name: 'Koraput', tagline: 'Mountains • Waterfalls • Culture', gradient: DESTINATION_GRADIENTS[0] },
    { name: 'Puri', tagline: 'Temple • Beach • Heritage', gradient: DESTINATION_GRADIENTS[1] },
    { name: 'Konark', tagline: 'History • Architecture • Culture', gradient: DESTINATION_GRADIENTS[2] },
    { name: 'Mahendragiri', tagline: 'Mountains • Nature • Adventure', gradient: DESTINATION_GRADIENTS[3] },
    { name: 'Gopalpur', tagline: 'Beach • Sunset • Relaxation', gradient: DESTINATION_GRADIENTS[0] },
    { name: 'Daringbadi', tagline: 'Hills • Clouds • Nature', gradient: DESTINATION_GRADIENTS[1] },
  ];

  readonly features: Feature[] = [
    { icon: 'compass', title: 'Curated Trips', description: 'Thoughtfully planned travel experiences.' },
    { icon: 'users', title: 'Small Group Experiences', description: 'Travel with a comfortable group and meet new people.' },
    { icon: 'map-pin', title: 'Local Experiences', description: 'Explore destinations beyond the usual tourist routes.' },
    { icon: 'luggage', title: 'Comfortable Travel', description: 'Planned transportation and accommodation where included.' },
    { icon: 'chat', title: 'Simple Booking', description: 'Easy booking through WhatsApp and direct communication.' },
    { icon: 'life-buoy', title: 'Personal Trip Support', description: 'Get in touch with Ghumo Odisha organizers when you need help.' },
  ];

  ngOnInit(): void {
    this.loadTrips();
  }

  loadTrips(): void {
    this.state.set('loading');
    this.tripService.getTrips(1, 50).subscribe({
      next: (r) => {
        this.trips.set(r.items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  scrollTo(id: string): void {
    document.getElementById(id)?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  openWhatsApp(): void {
    const message = "Hi! I'd like to know more about your upcoming Odisha trips.";
    window.open(this.contactService.buildWhatsAppLink(message), '_blank');
  }

  private hasUpcomingSlot(trip: TripSummary): boolean {
    if (!trip.nextSlotStartDate) return false;
    const start = new Date(trip.nextSlotStartDate);
    const todayStart = new Date();
    todayStart.setHours(0, 0, 0, 0);
    return start >= todayStart;
  }
}
