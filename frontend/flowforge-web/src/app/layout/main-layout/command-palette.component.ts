import { Component, ElementRef, EventEmitter, HostListener, Input, Output, ViewChild, computed, effect, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { ProjectSummaryDto } from '../../shared/models/project.models';

interface PaletteItem {
  group: 'Go to' | 'Projects' | 'Account';
  label: string;
  hint?: string;
  icon: string;
  run: () => void;
}

/**
 * Ctrl/Cmd+K quick switcher: jump to any page or project, or sign out, without touching the mouse.
 * The parent decides when it is open so the sidebar button and the keyboard shortcut share one switch.
 */
@Component({
  selector: 'app-command-palette',
  standalone: true,
  imports: [MatIconModule],
  template: `
    @if (open) {
      <div class="fixed inset-0 z-50 flex items-start justify-center pt-[14vh] px-4" (click)="close()">
        <div class="absolute inset-0 bg-ink/40 backdrop-blur-[2px] palette-fade"></div>
        <div class="relative w-full max-w-xl bg-panel border border-line-strong rounded-xl shadow-lift overflow-hidden palette-pop"
             role="dialog" aria-label="Command palette" (click)="$event.stopPropagation()">
          <div class="flex items-center gap-3 px-4 border-b border-line">
            <mat-icon class="!text-lg !w-5 !h-5 text-ink-faint">search</mat-icon>
            <input #field type="text" [value]="query()" (input)="onInput($any($event.target).value)"
                   placeholder="Jump to a page or project" autocomplete="off" spellcheck="false"
                   class="flex-1 h-14 bg-transparent text-[15px] text-ink placeholder:text-ink-faint focus:outline-none">
            <kbd class="font-mono text-[10px] text-ink-muted border border-line rounded px-1.5 py-0.5">ESC</kbd>
          </div>

          <div class="max-h-[340px] overflow-y-auto py-2">
            @for (item of results(); track item.group + item.label; let i = $index) {
              @if (i === 0 || results()[i - 1].group !== item.group) {
                <div class="eyebrow px-4 pt-3 pb-1">{{ item.group }}</div>
              }
              <button type="button" (click)="choose(item)" (mouseenter)="active.set(i)"
                      class="w-full flex items-center gap-3 px-4 py-2.5 text-left text-sm transition-colors"
                      [class]="active() === i ? 'bg-ember-50 text-ink' : 'text-ink-soft'">
                <mat-icon class="!text-lg !w-5 !h-5" [class]="active() === i ? 'text-ember-600' : 'text-ink-faint'">{{ item.icon }}</mat-icon>
                <span class="flex-1 truncate">{{ item.label }}</span>
                @if (item.hint) { <span class="font-mono text-[10px] text-ink-faint">{{ item.hint }}</span> }
              </button>
            } @empty {
              <div class="px-4 py-8 text-center text-sm text-ink-muted">Nothing matches "{{ query() }}"</div>
            }
          </div>

          <div class="flex items-center gap-4 px-4 py-2 border-t border-line bg-sunken/50 font-mono text-[10px] text-ink-muted">
            <span>↑↓ move</span><span>↵ open</span><span class="flex-1"></span><span>FlowForge</span>
          </div>
        </div>
      </div>
    }
  `,
  styles: [`
    .palette-fade { animation: pfade 0.2s var(--ff-ease); }
    .palette-pop { animation: ppop 0.28s var(--ff-ease); }
    @keyframes pfade { from { opacity: 0; } to { opacity: 1; } }
    @keyframes ppop { from { opacity: 0; transform: translateY(10px) scale(0.985); } to { opacity: 1; transform: none; } }
    @media (prefers-reduced-motion: reduce) { .palette-fade, .palette-pop { animation: none; } }
  `]
})
export class CommandPaletteComponent {
  private router = inject(Router);

  @Input() open = false;
  @Input() set projects(value: ProjectSummaryDto[]) { this.projectList.set(value ?? []); }
  @Output() closed = new EventEmitter<void>();
  @Output() signOut = new EventEmitter<void>();
  @ViewChild('field') private field?: ElementRef<HTMLInputElement>;

  private projectList = signal<ProjectSummaryDto[]>([]);
  query = signal('');
  active = signal(0);

  private all = computed<PaletteItem[]>(() => [
    ...[
      ['Inbox', '/inbox', 'inbox'], ['My Work', '/my-work', 'check_circle'], ['Dashboard', '/dashboard', 'space_dashboard'], ['Assistant', '/assistant', 'auto_awesome'],
      ['Projects', '/projects', 'folder'], ['Team', '/team', 'group'], ['Settings', '/settings', 'settings']
    ].map(([label, path, icon]): PaletteItem => ({ group: 'Go to', label, icon, hint: path, run: () => this.router.navigateByUrl(path) })),
    ...this.projectList().map((p): PaletteItem => ({
      group: 'Projects', label: p.name, icon: 'tag', hint: p.key, run: () => this.router.navigate(['/projects', p.id])
    })),
    { group: 'Account', label: 'Sign out', icon: 'logout', run: () => this.signOut.emit() }
  ]);

  results = computed(() => {
    const q = this.query().trim().toLowerCase();
    return q ? this.all().filter(i => i.label.toLowerCase().includes(q) || (i.hint ?? '').toLowerCase().includes(q)) : this.all();
  });

  constructor() {
    // Focus the field and start fresh every time the palette opens.
    effect(() => {
      if (this.open) {
        this.query.set('');
        this.active.set(0);
        setTimeout(() => this.field?.nativeElement.focus(), 30);
      }
    });
  }

  onInput(value: string) { this.query.set(value); this.active.set(0); }

  choose(item: PaletteItem) { item.run(); this.close(); }

  close() { this.closed.emit(); }

  @HostListener('document:keydown', ['$event'])
  onKey(e: KeyboardEvent) {
    if (!this.open) return;
    const count = this.results().length;
    if (e.key === 'Escape') { e.preventDefault(); this.close(); }
    else if (e.key === 'ArrowDown') { e.preventDefault(); this.active.set((this.active() + 1) % Math.max(count, 1)); }
    else if (e.key === 'ArrowUp') { e.preventDefault(); this.active.set((this.active() - 1 + count) % Math.max(count, 1)); }
    else if (e.key === 'Enter') { e.preventDefault(); const item = this.results()[this.active()]; if (item) this.choose(item); }
  }
}
