import { DatePipe } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PagedResult } from '../../../core/models/api-response.model';
import { AdminCarFilter, AdminDriverListItem } from '../../../core/models/admin-car.model';
import { ApprovalLabels, DriverStatus, approvalBadgeClass } from '../../../core/models/car.model';
import { AdminCarService } from '../../../core/services/admin-car.service';
import { ImageUrlPipe } from '../../../shared/pipes/image-url.pipe';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';

type LoadState = 'loading' | 'ready' | 'error';

export const DRIVER_TABS: { filter: AdminCarFilter; label: string }[] = [
  { filter: AdminCarFilter.All, label: 'All' },
  { filter: AdminCarFilter.Review, label: 'Waiting for review' },
  { filter: AdminCarFilter.Approved, label: 'Approved' },
  { filter: AdminCarFilter.Draft, label: 'Not submitted' },
  { filter: AdminCarFilter.Rejected, label: 'Rejected' },
  { filter: AdminCarFilter.Suspended, label: 'Suspended' },
];

@Component({
  selector: 'app-admin-driver-list',
  standalone: true,
  imports: [DatePipe, FormsModule, RouterLink, ImageUrlPipe, StatePanelComponent],
  template: `
    <div class="ahead">
      <div>
        <h2>Drivers</h2>
        <div class="sub">Owner-drivers who sign up to list their cars. Approve a driver before any of their cars can go live.</div>
      </div>
    </div>

    <div class="daytabs">
      @for (t of tabs; track t.filter) {
        <button class="daytab" [class.on]="filter() === t.filter" (click)="selectTab(t.filter)">{{ t.label }}</button>
      }
    </div>

    <div class="filters">
      <input class="sel srch" placeholder="Search name, phone or licence number…" [ngModel]="search()" (ngModelChange)="onSearch($event)" />
    </div>

    @switch (state()) {
      @case ('loading') { <app-state-panel kind="loading"></app-state-panel> }
      @case ('error') { <app-state-panel kind="error" message="Could not load drivers."></app-state-panel> }
      @case ('ready') {
        @let r = result()!;
        @if (r.items.length === 0) {
          <app-state-panel kind="empty" [message]="filter() === AdminCarFilter.Review ? 'No drivers waiting for review.' : 'No drivers match.'"></app-state-panel>
        } @else {
          <div class="panel" style="padding:16px">
            <div class="tblwrap">
              <table class="tbl">
                <tr><th>Driver</th><th>City</th><th>Cars</th><th>Status</th><th>Sent for review</th><th>Joined</th></tr>
                @for (d of r.items; track d.driverId) {
                  <tr>
                    <td>
                      <a class="row" style="gap:10px" [routerLink]="['/admin/drivers', d.driverId]">
                        @if (d.profilePhotoUrl) {
                          <img class="acar-av" [src]="d.profilePhotoUrl | imageUrl" alt="" />
                        } @else {
                          <span class="acar-av acar-av-empty">{{ d.name.charAt(0) }}</span>
                        }
                        <span><b>{{ d.name }}</b><div class="note">{{ d.phoneNumber ? '+91 ' + d.phoneNumber : 'No WhatsApp number' }}</div></span>
                      </a>
                    </td>
                    <td>{{ d.city || '—' }}</td>
                    <td>{{ d.approvedCarCount }} of {{ d.carCount }} approved</td>
                    <td>
                      <span [class]="'badge ' + badge(d.status)">{{ d.awaitingReview ? 'Waiting for review' : labels[d.status] }}</span>
                      @if (d.statusReason && d.status !== DriverStatus.Approved) { <div class="note">{{ d.statusReason }}</div> }
                    </td>
                    <td>{{ d.submittedForReviewAt ? (d.submittedForReviewAt | date: 'd MMM y, h:mm a') : '—' }}</td>
                    <td>{{ d.createdAt | date: 'd MMM y' }}</td>
                  </tr>
                }
              </table>
            </div>
            <div class="pager">
              <span>{{ r.totalCount }} driver(s) · page {{ page() }} of {{ totalPages }}</span>
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
export class AdminDriverListComponent implements OnInit {
  private readonly cars = inject(AdminCarService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly tabs = DRIVER_TABS;
  readonly labels = ApprovalLabels.driver;
  readonly AdminCarFilter = AdminCarFilter;
  readonly DriverStatus = DriverStatus;

  readonly state = signal<LoadState>('loading');
  readonly result = signal<PagedResult<AdminDriverListItem> | null>(null);
  readonly filter = signal(AdminCarFilter.All);
  readonly search = signal('');
  readonly page = signal(1);
  readonly pageSize = 20;

  ngOnInit(): void {
    // The tab lives in the URL (?tab=…) so "back" from a driver returns to the same list.
    const tab = Number(this.route.snapshot.queryParamMap.get('tab'));
    if (this.tabs.some((t) => t.filter === tab) && this.route.snapshot.queryParamMap.has('tab')) this.filter.set(tab);
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.cars.drivers(this.filter(), this.search(), this.page(), this.pageSize).subscribe({
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

  badge(status: DriverStatus): string {
    return approvalBadgeClass(status, DriverStatus.Approved, [DriverStatus.Rejected, DriverStatus.Suspended]);
  }

  get totalPages(): number {
    const r = this.result();
    return r ? Math.max(1, Math.ceil(r.totalCount / this.pageSize)) : 1;
  }
}
