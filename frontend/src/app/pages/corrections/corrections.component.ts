import { Location } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Finding } from '../../models/audit.model';
import { CorrectionPlan, CorrectionPlanStatus } from '../../models/correction.model';
import { AuditService } from '../../services/audit.service';
import { CorrectionService } from '../../services/correction.service';

interface CorrectionsNavState {
  prefillInstruction?: string;
}

const DEMO_AUDIT_ID = 'demo-audit-0001';

@Component({
  selector: 'app-corrections',
  imports: [FormsModule],
  templateUrl: './corrections.component.html',
  styleUrl: './corrections.component.scss',
})
export class CorrectionsComponent implements OnInit {
  private readonly auditService = inject(AuditService);
  private readonly correctionService = inject(CorrectionService);
  private readonly location = inject(Location);

  readonly isDemo = this.correctionService.isDemo;
  readonly CorrectionPlanStatus = CorrectionPlanStatus;

  readonly instruction = signal('');
  readonly clarificationAnswer = signal('');
  readonly findings = signal<Finding[]>([]);
  readonly currentPlan = signal<CorrectionPlan | null>(null);
  readonly submitting = signal(false);
  readonly deciding = signal(false);
  readonly errorMsg = signal<string | null>(null);

  ngOnInit(): void {
    const lastId = this.isDemo ? DEMO_AUDIT_ID : this.auditService.lastAuditId;
    if (lastId) {
      this.auditService.getAudit(lastId).subscribe({
        next: (job) => this.findings.set(job.findings),
        error: () => undefined,
      });
    }

    const navState = this.location.getState() as CorrectionsNavState | null;
    if (navState?.prefillInstruction) {
      this.instruction.set(navState.prefillInstruction);
    }
  }

  prefillFromFinding(finding: Finding): void {
    this.instruction.set(finding.fix ?? `Corrige : ${finding.title}`);
    this.currentPlan.set(null);
    this.errorMsg.set(null);
  }

  submit(): void {
    const text = this.instruction().trim();
    if (!text) return;

    this.submitting.set(true);
    this.errorMsg.set(null);
    this.correctionService.propose(text).subscribe({
      next: (plan) => {
        this.submitting.set(false);
        this.currentPlan.set(plan);
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.errorMsg.set(this.errorMessage(err));
      },
    });
  }

  submitClarification(): void {
    const plan = this.currentPlan();
    const answer = this.clarificationAnswer().trim();
    if (!plan || !answer) return;

    const combined = `${plan.instruction} — précision : ${answer}`;
    this.instruction.set(combined);
    this.clarificationAnswer.set('');
    this.submit();
  }

  apply(): void {
    const plan = this.currentPlan();
    if (!plan) return;

    this.deciding.set(true);
    this.correctionService.apply(plan.id).subscribe({
      next: (updated) => {
        this.deciding.set(false);
        this.currentPlan.set(updated);
      },
      error: (err: unknown) => {
        this.deciding.set(false);
        this.errorMsg.set(this.errorMessage(err));
      },
    });
  }

  reject(): void {
    const plan = this.currentPlan();
    if (!plan) return;

    this.deciding.set(true);
    this.correctionService.reject(plan.id).subscribe({
      next: (updated) => {
        this.deciding.set(false);
        this.currentPlan.set(updated);
      },
      error: (err: unknown) => {
        this.deciding.set(false);
        this.errorMsg.set(this.errorMessage(err));
      },
    });
  }

  reset(): void {
    this.instruction.set('');
    this.clarificationAnswer.set('');
    this.currentPlan.set(null);
    this.errorMsg.set(null);
  }

  hasApplicableActions(plan: CorrectionPlan): boolean {
    return plan.actions.some((a) => !a.blocked);
  }

  private errorMessage(err: unknown): string {
    if (err && typeof err === 'object' && 'error' in err) {
      const body = (err as { error?: { error?: string } }).error;
      if (body?.error) return body.error;
    }
    if (err instanceof Error) return err.message;
    return "Une erreur est survenue lors de l'appel à l'API.";
  }
}
