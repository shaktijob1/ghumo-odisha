import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { Observable, debounceTime, distinctUntilChanged, filter, switchMap, of, catchError } from 'rxjs';
import { AdminCrmService } from '../../../../core/services/admin-crm.service';
import { AdminTripService } from '../../../../core/services/admin-trip.service';
import { ToastService } from '../../../../core/services/toast.service';
import { AdminTripListItem } from '../../../../core/models/trip.model';
import {
  CrmAdminUser,
  CrmDeparture,
  CrmDuplicateCheck,
  CrmEnquiryStatuses,
  CrmStatus,
  CrmStatusLabels,
  LeadSource,
  LeadSourceLabels,
  LeadSources,
} from '../../../../core/models/crm.model';
import { StatePanelComponent } from '../../../../shared/components/state-panel.component';
import { apiErrorMessage } from '../../../../shared/utils/api-error';
import { formatPhone } from '../crm-utils';

/** 10-digit Indian mobile, after dropping spaces, dashes, +91 / 91 / 0. */
export function normalizeMobile(value: string | null | undefined): string | null {
  let digits = (value ?? '').replace(/\D/g, '');
  if (digits.length === 12 && digits.startsWith('91')) digits = digits.slice(2);
  if (digits.length === 11 && digits.startsWith('0')) digits = digits.slice(1);
  return /^[6-9]\d{9}$/.test(digits) ? digits : null;
}

const mobileValidator = (control: { value: string | null }) =>
  !control.value || normalizeMobile(control.value) ? null : { mobile: true };

/** Add a CRM customer (with an optional first trip interest and follow-up), or edit one. */
@Component({
  selector: 'app-crm-customer-form',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink, StatePanelComponent],
  templateUrl: './crm-customer-form.component.html',
  styleUrls: ['../crm-shared.css'],
})
export class CrmCustomerFormComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly crm = inject(AdminCrmService);
  private readonly tripService = inject(AdminTripService);
  private readonly toast = inject(ToastService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly sources = LeadSources;
  readonly sourceLabels = LeadSourceLabels;
  readonly stages = CrmEnquiryStatuses;
  readonly statusLabels = CrmStatusLabels;
  readonly formatPhone = formatPhone;

  readonly customerId = signal<number | null>(null);
  readonly state = signal<'loading' | 'ready' | 'error'>('ready');
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly duplicate = signal<CrmDuplicateCheck | null>(null);
  readonly checkingPhone = signal(false);
  /** A website sign-in uses the mobile as the login, so the CRM can't change it. */
  readonly phoneLocked = signal(false);
  readonly trips = signal<AdminTripListItem[]>([]);
  readonly departures = signal<CrmDeparture[]>([]);
  readonly admins = signal<CrmAdminUser[]>([]);

  readonly form = this.fb.group({
    name: ['', [Validators.required, Validators.maxLength(150)]],
    phoneNumber: ['', [Validators.required, mobileValidator]],
    alternatePhoneNumber: ['', [mobileValidator]],
    email: ['', [Validators.email, Validators.maxLength(200)]],
    whatsAppAvailable: [true],
    city: ['', [Validators.maxLength(100)]],
    leadSource: [LeadSource.Instagram as LeadSource, Validators.required],
    status: [CrmStatus.Interested as CrmStatus, Validators.required],
    summary: ['', [Validators.maxLength(2000)]],
    assignedAdminUserId: [null as number | null],
    addInterest: [true],
    tripId: [null as number | null],
    tripDateSlotId: [null as number | null],
    travellers: [2, [Validators.min(1), Validators.max(100)]],
    interestStatus: [CrmStatus.Interested as CrmStatus],
    expectedAmount: [null as number | null, [Validators.min(0)]],
    interestNotes: ['', [Validators.maxLength(2000)]],
    nextFollowUpDate: [''],
    followUpNote: ['', [Validators.maxLength(1000)]],
  });

  get isEdit(): boolean {
    return this.customerId() !== null;
  }

  ngOnInit(): void {
    const id = Number(this.route.snapshot.paramMap.get('id'));
    if (id) {
      this.customerId.set(id);
      this.loadCustomer(id);
    } else {
      const q = this.route.snapshot.queryParamMap;
      const phone = q.get('phone');
      if (phone) this.form.controls.phoneNumber.setValue(phone);
      // Arrived from a departure's "+ New customer": that trip and date are pre-selected.
      this.pendingSlotId = Number(q.get('slotId')) || null;
      const tripId = Number(q.get('tripId'));
      if (tripId) queueMicrotask(() => this.form.controls.tripId.setValue(tripId));
    }

    this.tripService.getTrips(1, 100).subscribe({ next: (r) => this.trips.set(r.items), error: () => undefined });
    this.crm.admins().subscribe({ next: (a) => this.admins.set(a), error: () => undefined });

    // Duplicate check as soon as the number is a complete mobile — before the admin fills the rest in.
    this.form.controls.phoneNumber.valueChanges
      .pipe(
        debounceTime(350),
        filter(() => !this.phoneLocked()),
        distinctUntilChanged(),
        switchMap((value) => {
          const phone = normalizeMobile(value);
          this.duplicate.set(null);
          if (!phone) return of(null);
          this.checkingPhone.set(true);
          return this.crm.checkPhone(phone, this.customerId()).pipe(catchError(() => of(null)));
        }),
      )
      .subscribe((result) => {
        this.checkingPhone.set(false);
        this.duplicate.set(result?.exists ? result : null);
      });

    this.form.controls.tripId.valueChanges.subscribe((tripId) => {
      this.form.controls.tripDateSlotId.setValue(null);
      this.departures.set([]);
      if (tripId) {
        this.crm.departures({ tripId, pageSize: 100 }).subscribe({
          next: (r) => {
            this.departures.set(r.items);
            if (this.pendingSlotId && r.items.some((d) => d.tripDateSlotId === this.pendingSlotId)) this.form.controls.tripDateSlotId.setValue(this.pendingSlotId);
            this.pendingSlotId = null;
          },
          error: () => undefined,
        });
      }
    });

    this.form.controls.tripDateSlotId.valueChanges.subscribe(() => this.suggestAmount());
    this.form.controls.travellers.valueChanges.subscribe(() => this.suggestAmount());
  }

  private pendingSlotId: number | null = null;

  /** Expected amount = today's price for the chosen departure × travellers, unless the admin typed one. */
  private amountTouched = false;

  onAmountInput(): void {
    this.amountTouched = true;
  }

  private suggestAmount(): void {
    if (this.amountTouched) return;
    const slot = this.departures().find((d) => d.tripDateSlotId === this.form.controls.tripDateSlotId.value);
    const travellers = this.form.controls.travellers.value ?? 0;
    this.form.controls.expectedAmount.setValue(slot && travellers > 0 ? slot.currentPrice * travellers : null, { emitEvent: false });
  }

  selectedDeparture(): CrmDeparture | undefined {
    return this.departures().find((d) => d.tripDateSlotId === this.form.controls.tripDateSlotId.value);
  }

  invalid(name: keyof typeof this.form.controls): boolean {
    const c = this.form.controls[name];
    return c.invalid && (c.touched || c.dirty);
  }

  submit(): void {
    this.errorMessage.set(null);
    this.form.markAllAsTouched();
    if (this.duplicate()) {
      this.errorMessage.set('This mobile number is already a customer. Open their profile and add another trip interest instead.');
      return;
    }

    const v = this.form.getRawValue();
    if (this.form.invalid) {
      this.errorMessage.set('Please fix the highlighted fields.');
      return;
    }

    const wantsInterest = !this.isEdit && v.addInterest;
    if (wantsInterest && !v.tripId) {
      this.errorMessage.set('Choose the trip they are interested in, or untick "Add a trip interest".');
      return;
    }

    const common = {
      name: v.name!.trim(),
      phoneNumber: normalizeMobile(v.phoneNumber)!,
      alternatePhoneNumber: normalizeMobile(v.alternatePhoneNumber),
      email: v.email?.trim() || null,
      whatsAppAvailable: !!v.whatsAppAvailable,
      city: v.city?.trim() || null,
      leadSource: v.leadSource!,
      summary: v.summary?.trim() || null,
      assignedAdminUserId: v.assignedAdminUserId,
    };

    this.saving.set(true);
    const request$: Observable<{ customerId: number } | void> = this.isEdit
      ? this.crm.updateCustomer(this.customerId()!, common)
      : this.crm.createCustomer({
          ...common,
          status: v.status!,
          interest: wantsInterest
            ? {
                tripId: v.tripId!,
                tripDateSlotId: v.tripDateSlotId,
                travellers: v.travellers ?? 1,
                status: v.interestStatus!,
                notes: v.interestNotes?.trim() || null,
                expectedAmount: v.expectedAmount,
              }
            : null,
          nextFollowUpDate: v.nextFollowUpDate || null,
          followUpNote: v.nextFollowUpDate ? v.followUpNote?.trim() || null : null,
        });

    request$.subscribe({
      next: (result) => {
        this.saving.set(false);
        const id = this.isEdit ? this.customerId()! : (result as { customerId: number }).customerId;
        this.toast.success(this.isEdit ? 'Customer updated.' : 'Customer added.');
        this.router.navigate(['/admin/crm/customers', id]);
      },
      error: (err: unknown) => {
        this.saving.set(false);
        this.errorMessage.set(apiErrorMessage(err, 'Could not save the customer.'));
      },
    });
  }

  private loadCustomer(id: number): void {
    this.state.set('loading');
    this.crm.customer(id).subscribe({
      next: (c) => {
        this.phoneLocked.set(c.isRegistered && !!c.phoneNumber);
        this.form.patchValue({
          name: c.name,
          phoneNumber: c.phoneNumber ?? '',
          alternatePhoneNumber: c.alternatePhoneNumber ?? '',
          email: c.email ?? '',
          whatsAppAvailable: c.whatsAppAvailable,
          city: c.city ?? '',
          leadSource: c.leadSource,
          summary: c.summary ?? '',
          assignedAdminUserId: c.assignedAdminUserId,
          addInterest: false,
        });
        if (this.phoneLocked()) this.form.controls.phoneNumber.disable();
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}
