import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { TermsService } from '../../core/services/terms.service';
import { StatePanelComponent } from '../../shared/components/state-panel.component';

type LoadState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-terms',
  standalone: true,
  imports: [CommonModule, StatePanelComponent],
  template: `
    <div class="container" style="padding:40px 0;max-width:760px">
      <span class="eyebrow" style="display:block;margin-bottom:8px">Legal</span>
      <h1 style="font-size:26px;margin-bottom:20px">Terms &amp; Conditions</h1>

      @switch (state()) {
        @case ('loading') {
          <app-state-panel kind="loading"></app-state-panel>
        }
        @case ('error') {
          <app-state-panel kind="error" message="Could not load the Terms & Conditions."></app-state-panel>
          <button type="button" class="btn sm" (click)="load()">Try again</button>
        }
        @case ('ready') {
          @for (paragraph of paragraphs(); track $index) {
            <p style="font-size:13.5px;line-height:1.7;color:var(--ink);white-space:pre-line;margin-bottom:16px">{{ paragraph }}</p>
          }
          <p class="note" style="margin-top:24px">Version {{ version() }}</p>
        }
      }
    </div>
  `,
})
export class TermsComponent implements OnInit {
  private readonly termsService = inject(TermsService);

  readonly state = signal<LoadState>('loading');
  private readonly text = signal('');
  readonly version = signal('');

  readonly paragraphs = computed(() => this.text().split(/\n\s*\n/).map((p) => p.trim()).filter(Boolean));

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.state.set('loading');
    this.termsService.get().subscribe({
      next: (t) => {
        this.text.set(t.text);
        this.version.set(t.version);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}
