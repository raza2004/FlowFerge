import { Component, inject, signal, computed, OnInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { TeamService } from '../../shared/services/team.service';
import { AuthService } from '../../core/services/auth.service';
import { InvitationDto, MEMBERSHIP_ROLES, TenantMemberDto } from '../../shared/models/auth.models';

const rank = (role: string) => MEMBERSHIP_ROLES.indexOf(role as typeof MEMBERSHIP_ROLES[number]);

@Component({
  selector: 'app-team',
  standalone: true,
  imports: [CommonModule, DatePipe, FormsModule, MatIconModule, MatButtonModule, MatTooltipModule],
  templateUrl: './team.component.html'
})
export class TeamComponent implements OnInit {
  private team = inject(TeamService);
  private auth = inject(AuthService);

  readonly currentUserId = this.auth.user()?.id;

  members = signal<TenantMemberDto[]>([]);
  invitations = signal<InvitationDto[]>([]);
  loading = signal(true);
  error = signal<string | null>(null);

  inviteOpen = signal(false);
  inviteEmail = '';
  inviteRole = 'Member';
  inviting = signal(false);
  lastInvite = signal<InvitationDto | null>(null);
  copiedId = signal<string | null>(null);
  confirmRemoveId = signal<string | null>(null);

  myRole = computed(() => this.members().find(m => m.userId === this.currentUserId)?.role ?? 'Member');
  canManage = computed(() => rank(this.myRole()) <= rank('Admin'));

  /** Roles this user may hand out: never Owner, never above their own role. */
  assignableRoles = computed(() => MEMBERSHIP_ROLES.filter(r => r !== 'Owner' && rank(r) >= rank(this.myRole())));

  ngOnInit() {
    this.load();
  }

  load() {
    this.team.getMembers().subscribe({
      next: members => {
        this.members.set(members);
        this.loading.set(false);
        if (this.canManage()) this.team.getInvitations().subscribe(list => this.invitations.set(list));
      },
      error: () => this.loading.set(false)
    });
  }

  canManageMember(m: TenantMemberDto): boolean {
    if (!this.canManage() || m.userId === this.currentUserId || m.role === 'Owner') return false;
    return this.myRole() === 'Owner' || rank(m.role) > rank(this.myRole());
  }

  sendInvite() {
    const email = this.inviteEmail.trim();
    if (!email) return;
    this.inviting.set(true);
    this.error.set(null);
    this.team.invite(email, this.inviteRole).subscribe({
      next: invitation => {
        this.inviting.set(false);
        this.inviteEmail = '';
        this.lastInvite.set(invitation);
        this.invitations.update(list => [invitation, ...list.filter(i => i.email !== invitation.email)]);
      },
      error: (err: HttpErrorResponse) => {
        this.inviting.set(false);
        this.error.set(err.error?.detail ?? 'Could not send the invitation.');
      }
    });
  }

  revoke(invitation: InvitationDto) {
    this.team.revokeInvitation(invitation.id).subscribe({
      next: () => {
        this.invitations.update(list => list.filter(i => i.id !== invitation.id));
        if (this.lastInvite()?.id === invitation.id) this.lastInvite.set(null);
      },
      error: (err: HttpErrorResponse) => this.error.set(err.error?.detail ?? 'Could not revoke the invitation.')
    });
  }

  async copyLink(invitation: InvitationDto) {
    try {
      await navigator.clipboard.writeText(invitation.inviteUrl);
      this.copiedId.set(invitation.id);
      setTimeout(() => this.copiedId.set(null), 2000);
    } catch {
      this.error.set('Copy failed. Select the link and copy it manually.');
    }
  }

  changeRole(member: TenantMemberDto, select: HTMLSelectElement) {
    const role = select.value;
    if (role === member.role) return;
    this.team.changeRole(member.userId, role).subscribe({
      next: () => this.members.update(list => list.map(m => m.userId === member.userId ? { ...m, role } : m)),
      error: (err: HttpErrorResponse) => {
        this.error.set(err.error?.detail ?? 'Could not change the role.');
        select.value = member.role;
      }
    });
  }

  remove(member: TenantMemberDto) {
    this.team.removeMember(member.userId).subscribe({
      next: () => {
        this.members.update(list => list.filter(m => m.userId !== member.userId));
        this.confirmRemoveId.set(null);
      },
      error: (err: HttpErrorResponse) => this.error.set(err.error?.detail ?? 'Could not remove this member.')
    });
  }

  initials(name: string): string {
    return name.split(' ').filter(Boolean).map(n => n[0]).slice(0, 2).join('').toUpperCase();
  }
}
