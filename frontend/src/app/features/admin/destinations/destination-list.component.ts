import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AdminDestinationService } from '../../../core/services/admin-destination.service';
import { AdminDestinationListItem } from '../../../core/models/destination.model';
import { PagedResult } from '../../../core/models/api-response.model';
import { StatePanelComponent } from '../../../shared/components/state-panel.component';
import { ConfirmDialogComponent } from '../../../shared/components/confirm-dialog.component';
import { ToastService } from '../../../core/services/toast.service';

type LoadState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-destination-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink, StatePanelComponent, ConfirmDialogComponent],
  templateUrl: './destination-list.component.html',
})
export class DestinationListComponent implements OnInit {
  private readonly destinationService = inject(AdminDestinationService);
  private readonly toast = inject(ToastService);

  readonly state = signal<LoadState>('loading');
  readonly result = signal<PagedResult<AdminDestinationListItem> | null>(null);
  readonly search = signal('');
  readonly page = signal(1);
  readonly pageSize = 10;

  readonly deleteTarget = signal<AdminDestinationListItem | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.destinationService.getDestinations(this.page(), this.pageSize, this.search() || undefined).subscribe({
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

  confirmDelete(): void {
    const destination = this.deleteTarget();
    if (!destination) return;

    this.destinationService.deleteDestination(destination.destinationId).subscribe({
      next: () => {
        this.toast.success('Destination deleted.');
        this.deleteTarget.set(null);
        this.load();
      },
      error: () => this.deleteTarget.set(null),
    });
  }
}
