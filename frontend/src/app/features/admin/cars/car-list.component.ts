import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PagedResult } from '../../../core/models/api-response.model';
import { AdminCarFilter, AdminCarListItem } from '../../../core/models/admin-car.model';
import { ApprovalLabels, CarStatus, FuelTypeLabels, approvalBadgeClass } from '../../../core/models/car.model';
import { AdminCarService } from '../../../core/services/admin-car.service';
import { ImageUrlPipe } from '../../../shared/pipes/image-url.pipe';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';

type LoadState = 'loading' | 'ready' | 'error';

const TABS: { filter: AdminCarFilter; label: string }[] = [
  { filter: AdminCarFilter.All, label: 'All' },
  { filter: AdminCarFilter.Review, label: 'Waiting for review' },
  { filter: AdminCarFilter.Approved, label: 'Approved' },
  { filter: AdminCarFilter.Draft, label: 'Not submitted' },
  { filter: AdminCarFilter.Rejected, label: 'Rejected' },
  { filter: AdminCarFilter.Suspended, label: 'Suspended' },
  { filter: AdminCarFilter.Inactive, label: 'Inactive' },
];

@Component({
  selector: 'app-admin-car-list',
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink, ImageUrlPipe, StatePanelComponent],
  template: `
    <div class="ahead">
      <div>
        <h2>Cars</h2>
        <div class="sub">A car shows in customer search only when the car, its driver and its pricing are all approved.</div>
      </div>
      <div class="row" style="gap:8px">
        <a class="btn sm ghost" routerLink="/admin/cars/pricing">Pricing approvals</a>
        <a class="btn sm" routerLink="/admin/cars/new">Add car</a>
      </div>
    </div>

    <div class="daytabs">
      @for (t of tabs; track t.filter) {
        <button class="daytab" [class.on]="filter() === t.filter" (click)="selectTab(t.filter)">{{ t.label }}</button>
      }
    </div>

    <div class="filters">
      <input class="sel srch" placeholder="Search brand, model, number plate or driver…" [ngModel]="search()" (ngModelChange)="onSearch($event)" />
    </div>

    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading"></app-state-panel> }
      @case ('error') { <app-state-panel kind="error" message="Could not load cars."></app-state-panel> }
      @case ('ready') {
        @let r = result()!;
        @if (r.items.length === 0) {
          <app-state-panel kind="empty" [message]="filter() === AdminCarFilter.Review ? 'No cars waiting for review.' : 'No cars match.'"></app-state-panel>
        } @else {
          <div class="panel" style="padding:16px">
            <div class="tblwrap">
              <table class="tbl">
                <tr><th>Car</th><th>Number plate</th><th>City</th><th>Driver</th><th>Status</th><th>In search</th><th>Added</th></tr>
                @for (c of r.items; track c.carId) {
                  <tr>
                    <td>
                      <a class="row" style="gap:10px" [routerLink]="['/admin/cars', c.carId]">
                        @if (c.coverPhotoUrl) {
                          <img class="acar-thumb" [src]="c.coverPhotoUrl | imageUrl" alt="" />
                        } @else {
                          <span class="acar-thumb acar-av-empty"></span>
                        }
                        <span><b>{{ c.displayName }}</b><div class="note">{{ c.category }} · {{ fuel[c.fuelType] }}</div></span>
                      </a>
                    </td>
                    <td class="mono">{{ c.registrationNumber }}</td>
                    <td>{{ c.baseCity }}</td>
                    <td><a [routerLink]="['/admin/drivers', c.driverId]">{{ c.driverName }}</a></td>
                    <td>
                      <span [class]="'badge ' + badge(c.status)">{{ c.awaitingReview ? 'Waiting for review' : labels[c.status] }}</span>
                      @if (c.hasPendingPricing) { <div><span class="badge wait" style="margin-top:4px">New pricing</span></div> }
                    </td>
                    <td>
                      @if (c.isListed) { <span class="badge ok">Live</span> } @else { <span class="note">No</span> }
                    </td>
                    <td>{{ c.createdAt | date: 'd MMM y' }}</td>
                  </tr>
                }
              </table>
            </div>
            <div class="pager">
              <span>{{ r.totalCount }} car(s) · page {{ page() }} of {{ totalPages }}</span>
              <div class="pg">
                <button (click)="goToPage(page() - 1)" [disabled]="page() <= 1">‹</button>
                <button (click)="goToPage(page() + 1)" [disabled]="page() >= totalPages">›</button>
              </div>
            </div>
          </div>
        }
      }
    }
  `,
})
export class AdminCarListComponent implements OnInit {
  private readonly cars = inject(AdminCarService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly tabs = TABS;
  readonly labels = ApprovalLabels.car;
  readonly fuel = FuelTypeLabels;
  readonly AdminCarFilter = AdminCarFilter;

  readonly state = signal<LoadState>('loading');
  readonly result = signal<PagedResult<AdminCarListItem> | null>(null);
  readonly filter = signal(AdminCarFilter.All);
  readonly search = signal('');
  readonly page = signal(1);
  readonly pageSize = 20;

  ngOnInit(): void {
    const q = this.route.snapshot.queryParamMap;
    const tab = Number(q.get('tab'));
    if (q.has('tab') && this.tabs.some((t) => t.filter === tab)) this.filter.set(tab);
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.cars.cars(this.filter(), this.search(), this.page(), this.pageSize).subscribe({
      next: (r) => {
        this.result.set(r);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  selectTab(filter: AdminCarFilter): void {
    this.filter.set(filter);
    this.page.set(1);
    this.router.navigate([], { queryParams: { tab: filter }, replaceUrl: true });
    this.load();
  }

  onSearch(value: string): void {
    this.search.set(value);
    this.page.set(1);
    this.load();
  }

  goToPage(p: number): void {
    this.page.set(p);
    this.load();
  }

  badge(status: CarStatus): string {
    return approvalBadgeClass(status, CarStatus.Approved, [CarStatus.Rejected, CarStatus.Suspended, CarStatus.Inactive]);
  }

  get totalPages(): number {
    const r = this.result();
    return r ? Math.max(1, Math.ceil(r.totalCount / this.pageSize)) : 1;
  }
}
