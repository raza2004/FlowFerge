import { AfterViewChecked, Component, ElementRef, OnDestroy, OnInit, ViewChild, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatIconModule } from '@angular/material/icon';
import { Subscription } from 'rxjs';
import { AssistantService } from '../../shared/services/assistant.service';
import { DashboardService } from '../../shared/services/dashboard.service';
import { FeaturesService, FEATURE_AI } from '../../shared/services/features.service';
import { AuthService } from '../../core/services/auth.service';
import { AssistantDocument, AssistantError, ChatMessage, Citation } from '../../shared/models/assistant.models';
import { MEMBERSHIP_ROLES } from '../../shared/models/auth.models';

const MAX_BYTES = 25 * 1024 * 1024;
const ACCEPTED = ['.pdf', '.txt', '.md', '.markdown'];

@Component({
  selector: 'app-assistant',
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule],
  templateUrl: './assistant.component.html'
})
export class AssistantComponent implements OnInit, AfterViewChecked, OnDestroy {
  private assistant = inject(AssistantService);
  private dashboard = inject(DashboardService);
  private auth = inject(AuthService);
  private features = inject(FeaturesService);

  @ViewChild('scroller') private scroller?: ElementRef<HTMLElement>;
  @ViewChild('box') private box?: ElementRef<HTMLTextAreaElement>;

  messages = signal<ChatMessage[]>([]);
  question = '';
  busy = signal(false);

  documents = signal<AssistantDocument[]>([]);
  docsLoading = signal(true);
  docsError = signal<string | null>(null);
  uploading = signal(false);
  uploadMessage = signal<{ ok: boolean; text: string } | null>(null);
  dragging = signal(false);
  myRole = signal('Member');
  openCitation = signal<string | null>(null);

  /** Owners and admins manage documents; everyone in the workspace can ask. The server enforces it either way. */
  canManage = computed(() => MEMBERSHIP_ROLES.indexOf(this.myRole() as any) <= MEMBERSHIP_ROLES.indexOf('Admin'));
  enabled = computed(() => this.features.isEnabled(FEATURE_AI));

  private streamSub?: Subscription;
  private stickToBottom = true;

  readonly suggestions = [
    'Summarize the main points of these documents',
    'What deadlines or dates are mentioned?',
    'Who is responsible for what?'
  ];

  ngOnInit() {
    this.loadDocuments();
    const me = this.auth.user()?.id;
    this.dashboard.getTeamMembers().subscribe({
      next: members => this.myRole.set(members.find(m => m.userId === me)?.role ?? 'Member'),
      error: () => {}
    });
  }

  ngAfterViewChecked() {
    if (this.stickToBottom && this.scroller) {
      const el = this.scroller.nativeElement;
      el.scrollTop = el.scrollHeight;
    }
  }

  ngOnDestroy() {
    this.streamSub?.unsubscribe();
  }

  onScroll() {
    const el = this.scroller?.nativeElement;
    if (!el) return;
    // Keep following the answer only while the reader is at the bottom; scrolling up pauses it.
    this.stickToBottom = el.scrollHeight - el.scrollTop - el.clientHeight < 80;
  }

  // ---------- Asking ----------

  send(text = this.question) {
    const q = text.trim();
    if (!q || this.busy()) return;

    this.question = '';
    this.stickToBottom = true;
    this.busy.set(true);
    this.messages.update(list => [
      ...list,
      { role: 'user', text: q, sources: [], status: 'done' },
      { role: 'assistant', text: '', sources: [], status: 'streaming' }
    ]);

    this.streamSub = this.assistant.stream(q).subscribe({
      next: event => {
        if (event.type === 'sources') this.patchLast({ sources: event.sources });
        else if (event.type === 'token') this.appendToLast(event.text);
        else this.patchLast({ usage: event.usage });
      },
      error: (err: AssistantError) => {
        this.patchLast({ status: 'error', error: err.message });
        this.busy.set(false);
      },
      complete: () => {
        this.patchLast({ status: 'done' });
        this.busy.set(false);
        setTimeout(() => this.box?.nativeElement.focus());
      }
    });
  }

  stop() {
    this.streamSub?.unsubscribe();
    this.patchLast({ status: 'done' });
    this.busy.set(false);
  }

  onKey(event: KeyboardEvent) {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  isUnknown(m: ChatMessage): boolean {
    return m.status === 'done' && /^i do not know/i.test(m.text.trim());
  }

  /**
   * The passages the answer actually points at. The model marks them as [1], [2]; if it marked none we show
   * everything that was searched, so a reader can still check the answer. Numbers keep their original meaning.
   */
  shownSources(m: ChatMessage): { source: Citation; n: number }[] {
    const all = m.sources.map((source, n) => ({ source, n }));
    if (m.status === 'streaming') return all;
    const cited = new Set([...m.text.matchAll(/\[(\d+)\]/g)].map(x => Number(x[1]) - 1));
    const used = all.filter(x => cited.has(x.n));
    return used.length > 0 ? used : all;
  }

  toggleCitation(key: string) {
    this.openCitation.update(open => (open === key ? null : key));
  }

  clear() {
    this.stop();
    this.messages.set([]);
  }

  private patchLast(patch: Partial<ChatMessage>) {
    this.messages.update(list => list.map((m, i) => (i === list.length - 1 ? { ...m, ...patch } : m)));
  }

  private appendToLast(text: string) {
    this.messages.update(list => list.map((m, i) => (i === list.length - 1 ? { ...m, text: m.text + text } : m)));
  }

  // ---------- Documents ----------

  loadDocuments() {
    this.docsLoading.set(true);
    this.docsError.set(null);
    this.assistant.listDocuments().subscribe({
      next: docs => { this.documents.set(docs); this.docsLoading.set(false); },
      error: err => { this.docsError.set(this.assistant.friendly(err).message); this.docsLoading.set(false); }
    });
  }

  onFiles(files: FileList | null) {
    const file = files?.[0];
    if (file) this.upload(file);
  }

  onDrop(event: DragEvent) {
    event.preventDefault();
    this.dragging.set(false);
    if (this.canManage()) this.onFiles(event.dataTransfer?.files ?? null);
  }

  private upload(file: File) {
    const ext = file.name.slice(file.name.lastIndexOf('.')).toLowerCase();
    if (!ACCEPTED.includes(ext)) {
      this.uploadMessage.set({ ok: false, text: 'Only PDF, TXT and Markdown files are supported.' });
      return;
    }
    if (file.size > MAX_BYTES) {
      this.uploadMessage.set({ ok: false, text: 'That file is too large. The limit is 25 MB.' });
      return;
    }

    this.uploading.set(true);
    this.uploadMessage.set(null);
    this.assistant.uploadDocument(file).subscribe({
      next: doc => {
        this.uploading.set(false);
        this.uploadMessage.set({ ok: true, text: `${doc.fileName} is ready: ${doc.pages} ${doc.pages === 1 ? 'page' : 'pages'}, ${doc.chunks} passages.` });
        this.loadDocuments();
      },
      error: err => {
        this.uploading.set(false);
        this.uploadMessage.set({ ok: false, text: this.assistant.friendly(err).message });
      }
    });
  }

  remove(doc: AssistantDocument) {
    if (!confirm(`Remove ${doc.fileName}? The assistant will stop answering from it.`)) return;
    this.assistant.deleteDocument(doc.documentId).subscribe({
      next: () => this.documents.update(list => list.filter(d => d.documentId !== doc.documentId)),
      error: err => this.uploadMessage.set({ ok: false, text: this.assistant.friendly(err).message })
    });
  }
}
