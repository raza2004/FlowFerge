import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';

interface FaqItem {
  question: string;
  answer: string;
  open: boolean;
}

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [CommonModule, RouterLink, MatIconModule],
  templateUrl: './home.component.html'
})
export class HomeComponent {
  mobileMenuOpen = signal(false);

  features = [
    {
      number: '01',
      icon: 'workspaces',
      title: 'Multi-tenant workspaces',
      description: 'Every organization gets its own isolated workspace — projects, boards, and members never cross tenant boundaries, even though everyone shares the same database.'
    },
    {
      number: '02',
      icon: 'view_kanban',
      title: 'Boards that sync live',
      description: 'Kanban boards with drag-and-drop, custom lists per project, and every move broadcast over SignalR — drag a card and every teammate watching sees it move instantly.'
    },
    {
      number: '03',
      icon: 'auto_awesome',
      title: 'AI that does real work',
      description: 'Task breakdown into subtasks, workload-aware assignee suggestions, and plain-language project summaries — powered by OpenAI, reviewed by you before anything is applied.'
    },
    {
      number: '04',
      icon: 'bolt',
      title: 'Automations',
      description: '"When a task moves to Done, notify the manager" — build rules that trigger on board activity and fire off a notification or reassignment automatically.'
    },
    {
      number: '05',
      icon: 'notifications_active',
      title: 'Notifications, everywhere',
      description: 'Real-time in-app alerts via SignalR, email digests via SMTP, and a Slack incoming-webhook integration — one event, dispatched to every channel a user has enabled.'
    },
    {
      number: '06',
      icon: 'shield',
      title: 'Admin panel & audit trail',
      description: 'A separate admin application for suspending tenants and users, watching system-wide stats, and reviewing an audit log that every command writes to automatically.'
    }
  ];

  aiCapabilities = [
    {
      icon: 'checklist',
      title: 'Task breakdown',
      description: 'Point it at a task and it proposes 3–6 concrete subtasks. Uncheck the ones you don\'t want — only the ones you keep get created.'
    },
    {
      icon: 'person_search',
      title: 'Assignee suggestions',
      description: 'Looks at who\'s actually free right now — real open-task counts per project member — and suggests the best fit, with a one-line reason.'
    },
    {
      icon: 'summarize',
      title: 'Project summaries',
      description: 'A short, plain-English status update generated from real task and overdue counts. Not a chatbot bolted on — a feature that reads your actual data.'
    }
  ];

  techStack = [
    '.NET 8', 'Angular 18', 'PostgreSQL', 'SignalR', 'OpenAI', 'RabbitMQ', 'Docker', 'GitHub Actions'
  ];

  faqs = signal<FaqItem[]>([
    {
      question: 'Is this a real, working product or a mockup?',
      answer: 'Every feature described on this page is implemented and running — multi-tenancy, real-time board sync, the AI features, the automation engine, email/Slack notifications, the admin panel, and audit logging. It\'s backed by a real test suite (unit, application, and Testcontainers-based integration tests) and a CI pipeline that runs on every push.',
      open: true
    },
    {
      question: 'How does multi-tenancy actually work?',
      answer: 'Every row that belongs to a workspace carries a TenantId, and every query is scoped to the current user\'s tenant at the application layer. Two organizations can use FlowForge side by side and never see a byte of each other\'s data, despite sharing the same PostgreSQL database.',
      open: false
    },
    {
      question: 'What happens when I move a task to "Done"?',
      answer: 'The list you drop it in decides everything: the task is marked complete (so it shows up correctly in dashboards and "My Work"), any automation rule watching that list fires, and — if a rule matches — a notification goes out over SignalR, email, and Slack in parallel.',
      open: false
    },
    {
      question: 'Who can access the admin panel?',
      answer: 'The very first account ever registered on a FlowForge instance automatically becomes a system admin — there\'s no separate admin sign-up. From there, an admin can suspend tenants or users and review the full audit log across every workspace.',
      open: false
    }
  ]);

  toggleFaq(index: number) {
    this.faqs.update(list =>
      list.map((item, i) => i === index ? { ...item, open: !item.open } : item));
  }
}
