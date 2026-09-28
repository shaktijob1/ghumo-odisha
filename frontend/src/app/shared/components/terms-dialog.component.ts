import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { TermsDialogService } from '../../core/services/terms-dialog.service';
import { TermsSection, TermsService, parseTerms } from '../../core/services/terms.service';
import { StatePanelComponent } from './state-panel.component';

type LoadState = 'loading' | 'ready' | 'error';

/** Terms & Conditions popup — closes on ✕, the Close button, a backdrop click, or Esc. */
@Component({
  selector: 'app-terms-dialog',
  standalone: true,
  imports: [StatePanelComponent],
  template: `
    <div class="modal-backdrop termsbackdrop" (click)="dialog.close()">
      <div class="modal termsmodal" role="dialog" aria-modal="true" aria-labelledby="terms-title" (click)="$event.stopPropagation()">
        <div class="termsmodal-head">
          <div>
            <span class="eyebrow">Legal</span>
            <h4 id="terms-title">Trip Terms &amp; Conditions</h4>
          </div>
          <button type="button" class="termsmodal-x" aria-label="Close" (click)="dialog.close()">✕</button>
        </div>

        <div class="termsmodal-body">
          @switch (state()) {
            @case ('loading') {
              <app-state-panel kind="loading"></app-state-panel>
            }
            @case ('error') {
              <app-state-panel kind="error" message="Could not load the Terms & Conditions."></app-state-panel>
              <button type="button" class="btn sm" (click)="load()">Try again</button>
            }
            @case ('ready') {
              @for (s of sections(); track $index) {
                <section class="termsmodal-sec">
                  @if (s.heading) {
                    <b>{{ s.heading }}</b>
                  }
                  <p>{{ s.body }}</p>
                </section>
              }
              <p class="note">Version {{ version() }}</p>
            }
          }
        </div>

        <div class="termsmodal-foot">
          <button type="button" class="btn block" (click)="dialog.close()">Close</button>
        </div>
      </div>
    </div>
  `,
})
export class TermsDialogComponent implements OnInit {
  readonly dialog = inject(TermsDialogService);
  private readonly termsService = inject(TermsService);

  readonly state = signal<LoadState>('loading');
  readonly sections = signal<TermsSection[]>([]);
  readonly version = signal('');

  ngOnInit(): void {
    this.load();
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.dialog.close();
  }

  load(): void {
    this.state.set('loading');
    this.termsService.get().subscribe({
      next: (t) => {
        this.sections.set(parseTerms(t.text));
        this.version.set(t.version);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}
