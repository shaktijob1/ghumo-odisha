import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, inject, signal, SimpleChanges } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject, debounceTime, switchMap, of, catchError } from 'rxjs';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { CrmCustomerListItem, CrmStatusLabels, CrmStatusTone } from '../../../../core/models/crm.model';
import { formatPhone } from '../crm-utils';

/**
 * Find an existing customer (by name or mobile) to add to a departure — or go to "Add customer"
 * with the trip and departure already chosen, when they're new.
 */
@Component({
  selector: 'app-crm-customer-picker',
  standalone: true,
  imports: [FormsModule, RouterLink],
  template: `
    @if (open) {
      <div class="modal-backdrop" (click)="closed.emit()">
        <div class="modal crm-modal" (click)="$event.stopPropagation()">
          <h4>{{ title }}</h4>
          <input class="inp" type="search" [(ngModel)]="query" (ngModelChange)="searches.next(query)" placeholder="Name or mobile number" autofocus style="margin-bottom:10px" />
          @if (searching()) {
            <p class="note">Searching…</p>
          } @else if (query.trim().length >= 2 && results().length === 0) {
            <p class="note">No customer found.</p>
          }
          <ul class="crm-list">
            @for (c of results(); track c.customerId) {
              <li class="crm-pick" (click)="picked.emit(c)" (keydown.enter)="picked.emit(c)" tabindex="0">
                <div class="grow">
                  <b>{{ c.name || 'No name yet' }}</b>
                  <div class="note">{{ formatPhone(c.phoneNumber) || c.email }} · {{ c.interestCount }} interest{{ c.interestCount === 1 ? '' : 's' }} · {{ c.totalBookings }} booking{{ c.totalBookings === 1 ? '' : 's' }}</div>
                </div>
                <span class="badge" [class]="'badge ' + tone[c.status]">{{ labels[c.status] }}</span>
              </li>
            }
          </ul>
          <div class="row sp" style="margin-top:14px;gap:8px">
            <a class="btn sm ghost" routerLink="/admin/crm/customers/new" [queryParams]="newCustomerParams">+ New customer</a>
            <button type="button" class="btn sm ghost" (click)="closed.emit()">Close</button>
          </div>
        </div>
      </div>
    }
  `,
  styleUrls: ['../crm-shared.css'],
})
export class CrmCustomerPickerComponent implements OnChanges, OnDestroy {
  private readonly crm = inject(AdminCrmService);

  @Input() open = false;
  @Input() title = 'Add a customer';
  /** Query string for "+ New customer" (e.g. the trip and departure to pre-select). */
  @Input() newCustomerParams: Record<string, number | string> = {};
  @Output() closed = new EventEmitter<void>();
  @Output() picked = new EventEmitter<CrmCustomerListItem>();

  readonly labels = CrmStatusLabels;
  readonly tone = CrmStatusTone;
  readonly formatPhone = formatPhone;
  readonly results = signal<CrmCustomerListItem[]>([]);
  readonly searching = signal(false);
  readonly searches = new Subject<string>();
  query = '';

  constructor() {
    this.searches
      .pipe(
        debounceTime(300),
        switchMap((q) => {
          if (q.trim().length < 2) {
            this.searching.set(false);
            return of(null);
          }
          this.searching.set(true);
          return this.crm.customers({ search: q.trim(), pageSize: 8 }).pipe(catchError(() => of(null)));
        }),
      )
      .subscribe((r) => {
        this.searching.set(false);
        this.results.set(r?.items ?? []);
      });
  }

  ngOnChanges(changes: SimpleChanges): void {
    // Reset only when the dialog opens — the page behind may pass new inputs while it is open.
    if (!changes['open']?.currentValue || changes['open'].previousValue) return;
    if (!this.open) return;
    this.query = '';
    this.results.set([]);
  }

  ngOnDestroy(): void {
    this.searches.complete();
  }
}
