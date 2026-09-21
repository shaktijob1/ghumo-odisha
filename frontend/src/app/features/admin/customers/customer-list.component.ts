import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminCustomerService } from '../../../core/services/admin-customer.service';
import { AdminCustomerListItem } from '../../../core/models/customer.model';
import { PagedResult } from '../../../core/models/api-response.model';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';

type LoadState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-customer-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent],
  templateUrl: './customer-list.component.html',
})
export class CustomerListComponent implements OnInit {
  private readonly customerService = inject(AdminCustomerService);

  readonly state = signal<LoadState>('loading');
  readonly result = signal<PagedResult<AdminCustomerListItem> | null>(null);
  readonly search = signal('');
  readonly page = signal(1);
  readonly pageSize = 10;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.customerService.getCustomers(this.page(), this.pageSize, this.search() || undefined).subscribe({
      next: (r) => {
        this.result.set(r);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  onSearchChange(value: string): void {
    this.search.set(value);
    this.page.set(1);
    this.load();
  }

  goToPage(p: number): void {
    this.page.set(p);
    this.load();
  }

  get totalPages(): number {
    const r = this.result();
    return r ? Math.max(1, Math.ceil(r.totalCount / this.pageSize)) : 1;
  }
}
