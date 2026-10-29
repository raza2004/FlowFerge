import { Component, signal, AfterViewInit, OnDestroy, ElementRef, ViewChild, inject, NgZone } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import gsap from 'gsap';
import { ScrollTrigger } from 'gsap/ScrollTrigger';
import Lenis from 'lenis';

interface FaqItem {
  question: string;
  answer: string;
  open: boolean;
}

interface SceneCard { id: string; title: string; color: string; }
interface SceneColumn { name: string; cards: SceneCard[]; }

const ANSWER = 'Only Owners and Admins can invite people into the workspace. [1]';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule],
  templateUrl: './home.component.html',
  styleUrl: './home.component.scss'
})
export class HomeComponent implements AfterViewInit, OnDestroy {
  private host = inject<ElementRef<HTMLElement>>(ElementRef);
  private zone = inject(NgZone);
  private lenis?: Lenis;
  private lenisTick = (time: number) => this.lenis?.raf(time * 1000);
  private cleanups: Array<() => void> = [];
  private ctx?: gsap.Context;

  @ViewChild('embers') private emberCanvas!: ElementRef<HTMLCanvasElement>;
  @ViewChild('cursor') private cursorEl!: ElementRef<HTMLElement>;

  mobileMenuOpen = signal(false);
  scrolled = signal(false);

  techStack = ['.NET 8', 'Angular 21', 'PostgreSQL', 'pgvector', 'SignalR', 'RabbitMQ', 'Redis', 'Python FastAPI'];
  roles = ['Owner', 'Admin', 'Manager', 'Member', 'Guest'];

  steps = [
    { title: 'Capture', text: 'Every idea becomes a card. Priority, owner and due date live on the card, not in someone\'s head.' },
    { title: 'Move', text: 'Drag a card across the board and everyone watching it sees the move the moment it happens.' },
    { title: 'Ship', text: 'The Done column fills, sprints burn down, and the rules you set tell the right person.' }
  ];

  // Sample data for the scroll scene. FF-14 and FF-23 are the cards that travel.
  columns: SceneColumn[] = [
    { name: 'To do', cards: [
      { id: 'FF-11', title: 'Design onboarding', color: '#B42318' },
      { id: 'FF-12', title: 'Write API docs', color: '#978F7F' },
      { id: 'FF-13', title: 'Seed demo data', color: '#A8480A' },
      { id: 'FF-14', title: 'Fix login redirect', color: '#F2501C' }
    ] },
    { name: 'In progress', cards: [
      { id: 'FF-21', title: 'Burndown chart', color: '#F2501C' },
      { id: 'FF-22', title: 'Invite emails', color: '#A8480A' },
      { id: 'FF-23', title: 'Review pull request', color: '#7D5C06' }
    ] },
    { name: 'Done', cards: [
      { id: 'FF-31', title: 'Set up pipeline', color: '#2D6A32' },
      { id: 'FF-32', title: 'Realtime board', color: '#2D6A32' }
    ] }
  ];

  principles = [
    'Answers come only from the documents your team uploaded.',
    'Every answer points to the page it was found on.',
    'When the documents do not say, it says "I do not know".',
    'Each workspace is searched on its own, never across customers.'
  ];

  faqs = signal<FaqItem[]>([
    {
      question: 'Is FlowForge a finished product?',
      answer: 'It is a working project in active development. Boards, sprints, invitations, automations, notifications and the admin portal run today. The document assistant is built on the server and its screen is the next thing to land.',
      open: true
    },
    {
      question: 'How are workspaces kept separate?',
      answer: 'Every record belongs to a workspace and every query is scoped to the signed in person\'s workspace. The assistant searches each workspace\'s documents separately, so one customer can never retrieve another customer\'s text.',
      open: false
    },
    {
      question: 'What happens when I move a card to Done?',
      answer: 'The card is marked complete, so it counts in dashboards and My Work. Any automation rule watching that list fires, and a matching rule sends a notification in the app, by email and to Slack if you connected it.',
      open: false
    },
    {
      question: 'Who can open the admin portal?',
      answer: 'The first account ever registered on an instance becomes a system admin. From there an admin can suspend workspaces or people, switch features on and off, and read the audit log.',
      open: false
    }
  ]);

  toggleFaq(index: number) {
    this.faqs.update(list => list.map((item, i) => i === index ? { ...item, open: !item.open } : item));
  }

  goTo(event: Event, selector: string) {
    event.preventDefault();
    this.mobileMenuOpen.set(false);
    if (this.lenis) {
      this.lenis.scrollTo(selector === '#top' ? 0 : selector, { duration: 1.4 });
    } else {
      const el = selector === '#top' ? document.body : document.querySelector(selector);
      el?.scrollIntoView({ behavior: 'smooth' });
    }
  }

  ngAfterViewInit() {
    if (typeof window === 'undefined') return;

    const onScroll = () => this.scrolled.set(window.scrollY > 40);
    window.addEventListener('scroll', onScroll, { passive: true });
    this.cleanups.push(() => window.removeEventListener('scroll', onScroll));

    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    if (reduced) {
      // Everything stays in its final, fully visible state; only the typed answer needs filling in.
      const typed = this.host.nativeElement.querySelector<HTMLElement>('.typed');
      if (typed) typed.textContent = ANSWER;
      this.host.nativeElement.querySelector<HTMLElement>('.cite')?.style.setProperty('opacity', '1');
      return;
    }

    // Animation loops must not trigger Angular change detection on every frame.
    this.zone.runOutsideAngular(() => this.initMotion());
  }

  private initMotion() {
    gsap.registerPlugin(ScrollTrigger);
    const root = this.host.nativeElement;
    const q = <T extends HTMLElement = HTMLElement>(s: string) => Array.from(root.querySelectorAll<T>(s));
    const one = <T extends HTMLElement = HTMLElement>(s: string) => root.querySelector<T>(s);

    this.lenis = new Lenis({ duration: 1.15, smoothWheel: true });
    this.lenis.on('scroll', ScrollTrigger.update);
    gsap.ticker.add(this.lenisTick);
    gsap.ticker.lagSmoothing(0);

    this.ctx = gsap.context(() => {
      this.heroIntro(q, one);
      this.boardScene(q, one);
      this.insideRail(q, one);
      this.assistantTyping(one);
      this.scrollReveals(q);

      gsap.to('.big-track', {
        xPercent: -25, ease: 'none',
        scrollTrigger: { trigger: '.big-marquee', start: 'top bottom', end: 'bottom top', scrub: true }
      });
      gsap.from('.cta-title', {
        yPercent: 35, opacity: 0, duration: 1.2, ease: 'power4.out',
        scrollTrigger: { trigger: '.cta-title', start: 'top 85%' }
      });
    }, root);

    this.embers();
    this.pointerEffects(q);
    requestAnimationFrame(() => ScrollTrigger.refresh());
  }

  // ---------- Hero ----------
  private heroIntro(q: <T extends HTMLElement>(s: string) => T[], _one: unknown) {
    gsap.timeline({ defaults: { ease: 'power4.out' } })
      .from('.hero-title .line-in', { yPercent: 115, duration: 1.2, stagger: 0.14 }, 0.1)
      .from('.hero-fade', { opacity: 0, y: 24, duration: 0.9, stagger: 0.1 }, 0.5);
  }

  // ---------- Signature scene: the board assembles itself ----------
  private boardScene(q: <T extends HTMLElement>(s: string) => T[], one: <T extends HTMLElement>(s: string) => T | null) {
    const scene = one('.scene');
    if (!scene) return;

    const cards = q('.slot-card');
    const stepEls = q('.step');
    const colWidth = () => {
      const cols = q('.col');
      return cols.length > 1 ? cols[1].getBoundingClientRect().left - cols[0].getBoundingClientRect().left : 0;
    };
    // Stable pseudo random so the scatter looks the same on every visit.
    const rand = (i: number, salt: number) => {
      const v = Math.sin((i + 1) * 12.9898 + salt * 78.233) * 43758.5453;
      return v - Math.floor(v);
    };

    gsap.set(stepEls, { autoAlpha: 0, y: 24 });
    gsap.set(stepEls[0], { autoAlpha: 1, y: 0 });

    const counts = { todo: 4, doing: 3, done: 2 };
    const renderCounts = () => {
      const els = q('.col-count');
      if (els.length === 3) {
        els[0].textContent = String(Math.round(counts.todo));
        els[1].textContent = String(Math.round(counts.doing));
        els[2].textContent = String(Math.round(counts.done));
      }
    };

    const A = one('[data-card="FF-14"]');
    const B = one('[data-card="FF-23"]');
    const doneCards = q('.col[data-col="2"] .mini-card');

    const tl = gsap.timeline({
      defaults: { ease: 'power2.inOut' },
      scrollTrigger: {
        trigger: scene, start: 'top top', end: '+=320%', pin: true, scrub: 0.7, anticipatePin: 1,
        invalidateOnRefresh: true
      }
    });

    // 1. Capture: scattered cards fly into their columns.
    tl.from(cards, {
      x: (i: number) => (rand(i, 1) - 0.5) * 900,
      y: (i: number) => (rand(i, 2) - 0.5) * 600,
      rotation: (i: number) => (rand(i, 3) - 0.5) * 50,
      scale: 0.7, opacity: 0, duration: 1.1,
      stagger: { each: 0.07, from: 'random' }
    }, 0);

    // Step text 1 to 2
    tl.to(stepEls[0], { autoAlpha: 0, y: -24, duration: 0.22 }, 1.45)
      .fromTo(stepEls[1], { autoAlpha: 0, y: 24 }, { autoAlpha: 1, y: 0, duration: 0.22 }, 1.72);

    // 2. Move: one card moves To do to In progress, another In progress to Done.
    if (A) tl.to(A, { x: () => colWidth(), duration: 0.7 }, 1.8);
    if (B) tl.to(B, { x: () => colWidth(), duration: 0.7 }, 2.0);
    tl.to(counts, { todo: 3, doing: 3, done: 3, duration: 0.7, onUpdate: renderCounts }, 1.9);

    // Step text 2 to 3
    tl.to(stepEls[1], { autoAlpha: 0, y: -24, duration: 0.22 }, 2.75)
      .fromTo(stepEls[2], { autoAlpha: 0, y: 24 }, { autoAlpha: 1, y: 0, duration: 0.22 }, 3.02);

    // 3. Ship: the card keeps going into Done and the column lights up.
    if (A) tl.to(A, { x: () => colWidth() * 2, duration: 0.7 }, 3.1);
    tl.to(counts, { todo: 3, doing: 2, done: 4, duration: 0.7, onUpdate: renderCounts }, 3.2);
    tl.to(doneCards, { backgroundColor: 'rgba(242, 80, 28, 0.22)', borderColor: 'rgba(242, 80, 28, 0.7)', duration: 0.6, stagger: 0.1 }, 3.6);
    tl.to('.scene-progress', { scaleX: 1, ease: 'none', duration: tl.duration() }, 0);
    tl.to({}, { duration: 0.5 }); // a beat of stillness at the end
  }

  // ---------- Horizontal rail ----------
  private insideRail(q: <T extends HTMLElement>(s: string) => T[], one: <T extends HTMLElement>(s: string) => T | null) {
    const rail = one('.rail');
    const track = one('.rail-track');
    if (!rail || !track) return;

    const mm = gsap.matchMedia();
    mm.add('(min-width: 1024px)', () => {
      gsap.to(track, {
        x: () => -(track.scrollWidth - document.documentElement.clientWidth + 40),
        ease: 'none',
        scrollTrigger: {
          trigger: rail, start: 'top top', end: () => '+=' + Math.max(track.scrollWidth - window.innerWidth, 400),
          pin: true, scrub: 0.8, invalidateOnRefresh: true, anticipatePin: 1
        }
      });
    });
  }

  // ---------- Assistant demo types itself out ----------
  private assistantTyping(one: <T extends HTMLElement>(s: string) => T | null) {
    const typed = one('.typed');
    const cite = one('.cite');
    const chat = one('.chat');
    if (!typed || !chat) return;
    const state = { n: 0 };
    ScrollTrigger.create({
      trigger: chat, start: 'top 72%', once: true,
      onEnter: () => {
        gsap.to(state, {
          n: ANSWER.length, duration: 2.6, ease: 'none', snap: { n: 1 },
          onUpdate: () => { typed.textContent = ANSWER.slice(0, state.n); },
          onComplete: () => { if (cite) gsap.to(cite, { opacity: 1, y: 0, duration: 0.5 }); }
        });
      }
    });
    if (cite) gsap.set(cite, { y: 8 });
  }

  private scrollReveals(q: <T extends HTMLElement>(s: string) => T[]) {
    const items = q('.reveal');
    gsap.set(items, { opacity: 0, y: 36 });
    ScrollTrigger.batch(items, {
      start: 'top 88%', once: true,
      onEnter: batch => gsap.to(batch, { opacity: 1, y: 0, duration: 0.9, stagger: 0.09, ease: 'power3.out' })
    });
  }

  // ---------- Embers: a cursor reactive particle field behind the hero ----------
  private embers() {
    const canvas = this.emberCanvas?.nativeElement;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    if (!ctx) return;

    let w = 0, h = 0, dpr = 1;
    const pointer = { x: -999, y: -999 };
    type P = { x: number; y: number; vx: number; vy: number; r: number; a: number; tw: number };
    let parts: P[] = [];

    const spawn = (initial: boolean): P => ({
      x: Math.random() * w,
      y: initial ? Math.random() * h : h + 10,
      vx: (Math.random() - 0.5) * 0.25,
      vy: -(0.15 + Math.random() * 0.5),
      r: 0.8 + Math.random() * 2.4,
      a: 0.25 + Math.random() * 0.55,
      tw: Math.random() * Math.PI * 2
    });
    const resize = () => {
      dpr = Math.min(window.devicePixelRatio || 1, 2);
      w = canvas.clientWidth; h = canvas.clientHeight;
      canvas.width = w * dpr; canvas.height = h * dpr;
      ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
      const count = Math.round(Math.min(110, Math.max(40, (w * h) / 16000)));
      parts = Array.from({ length: count }, () => spawn(true));
    };
    resize();

    const ro = new ResizeObserver(resize);
    ro.observe(canvas);
    const onMove = (e: PointerEvent) => {
      const r = canvas.getBoundingClientRect();
      pointer.x = e.clientX - r.left; pointer.y = e.clientY - r.top;
    };
    window.addEventListener('pointermove', onMove, { passive: true });

    let visible = true;
    const io = new IntersectionObserver(([entry]) => { visible = entry.isIntersecting; });
    io.observe(canvas);

    let raf = 0;
    const frame = () => {
      raf = requestAnimationFrame(frame);
      if (!visible) return;
      ctx.clearRect(0, 0, w, h);
      for (let i = 0; i < parts.length; i++) {
        const p = parts[i];
        const dx = p.x - pointer.x, dy = p.y - pointer.y;
        const d2 = dx * dx + dy * dy;
        if (d2 < 16000) {            // pushed away from the cursor, and flares as it goes
          const d = Math.sqrt(d2) || 1;
          const f = (1 - d / 126) * 1.6;
          p.vx += (dx / d) * f * 0.4;
          p.vy += (dy / d) * f * 0.4;
        }
        p.vx *= 0.97; p.vy = p.vy * 0.985 + (-0.012);
        p.x += p.vx + Math.sin(p.tw += 0.01) * 0.12;
        p.y += p.vy;
        if (p.y < -12 || p.x < -12 || p.x > w + 12) parts[i] = spawn(false);

        const near = d2 < 22000 ? 1 + (1 - d2 / 22000) : 1;
        ctx.beginPath();
        ctx.fillStyle = `rgba(242, 80, 28, ${Math.min(p.a * near, 1)})`;
        ctx.arc(p.x, p.y, p.r * near, 0, Math.PI * 2);
        ctx.fill();
      }
    };
    frame();

    this.cleanups.push(() => {
      cancelAnimationFrame(raf); ro.disconnect(); io.disconnect();
      window.removeEventListener('pointermove', onMove);
    });
  }

  // ---------- Custom cursor and magnetic buttons ----------
  private pointerEffects(q: <T extends HTMLElement>(s: string) => T[]) {
    if (!window.matchMedia('(hover: hover) and (pointer: fine)').matches) return;

    const dot = this.cursorEl?.nativeElement;
    if (dot) {
      const x = gsap.quickTo(dot, 'x', { duration: 0.35, ease: 'power3' });
      const y = gsap.quickTo(dot, 'y', { duration: 0.35, ease: 'power3' });
      const move = (e: PointerEvent) => { x(e.clientX); y(e.clientY); dot.style.opacity = '1'; };
      const over = (e: Event) => {
        const hot = (e.target as HTMLElement).closest('a, button, .magnetic');
        dot.classList.toggle('cursor-hot', !!hot);
      };
      window.addEventListener('pointermove', move, { passive: true });
      document.addEventListener('pointerover', over);
      this.cleanups.push(() => { window.removeEventListener('pointermove', move); document.removeEventListener('pointerover', over); });
    }

    for (const el of q('.magnetic')) {
      const move = (e: PointerEvent) => {
        const r = el.getBoundingClientRect();
        const dx = e.clientX - (r.left + r.width / 2), dy = e.clientY - (r.top + r.height / 2);
        gsap.to(el, { x: dx * 0.32, y: dy * 0.38, duration: 0.4, ease: 'power3.out' });
      };
      const leave = () => gsap.to(el, { x: 0, y: 0, duration: 0.8, ease: 'elastic.out(1, 0.4)' });
      el.addEventListener('pointermove', move);
      el.addEventListener('pointerleave', leave);
      this.cleanups.push(() => { el.removeEventListener('pointermove', move); el.removeEventListener('pointerleave', leave); });
    }
  }

  ngOnDestroy() {
    this.cleanups.forEach(fn => fn());
    gsap.ticker.remove(this.lenisTick);
    this.lenis?.destroy();
    this.ctx?.revert();
    ScrollTrigger.getAll().forEach(trigger => trigger.kill());
  }
}
