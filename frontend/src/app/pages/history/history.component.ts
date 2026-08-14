import { DatePipe, DecimalPipe, NgClass } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { CorrectionAction, CorrectionPlan, CorrectionPlanStatus } from '../../models/correction.model';
import { CorrectionService } from '../../services/correction.service';

@Component({
  selector: 'app-history',
  imports: [DatePipe, DecimalPipe, NgClass],
  templateUrl: './history.component.html',
  styleUrl: './history.component.scss',
})
export class HistoryComponent implements OnInit {
  private readonly correctionService = inject(CorrectionService);
  private readonly router = inject(Router);

  readonly CorrectionPlanStatus = CorrectionPlanStatus;
  readonly plans = signal<CorrectionPlan[]>([]);
  readonly loadError = signal<string | null>(null);

  ngOnInit(): void {
    this.correctionService.list().subscribe({
      next: (plans) => this.plans.set(plans),
      error: () => this.loadError.set("Impossible de charger l'historique des corrections."),
    });
  }

  statusLabel(status: CorrectionPlanStatus): string {
    switch (status) {
      case CorrectionPlanStatus.AwaitingReview:
        return 'En attente de validation';
      case CorrectionPlanStatus.NeedsClarification:
        return 'Précision demandée';
      case CorrectionPlanStatus.Applied:
        return 'Appliqué';
      case CorrectionPlanStatus.PartiallyApplied:
        return 'Partiellement appliqué';
      case CorrectionPlanStatus.Rejected:
        return 'Refusé';
      case CorrectionPlanStatus.Failed:
        return 'Échoué';
    }
  }

  statusClass(status: CorrectionPlanStatus): string {
    switch (status) {
      case CorrectionPlanStatus.Applied:
        return 'sg-badge--success';
      case CorrectionPlanStatus.PartiallyApplied:
      case CorrectionPlanStatus.AwaitingReview:
      case CorrectionPlanStatus.NeedsClarification:
        return 'sg-badge--warning';
      case CorrectionPlanStatus.Rejected:
      case CorrectionPlanStatus.Failed:
        return 'sg-badge--danger';
    }
  }

  /** Rollback manuel (§4.2) : ré-applique « avant » via une nouvelle instruction validée par l'humain. */
  restore(action: CorrectionAction): void {
    const instruction = `Restaure « ${action.target} » à sa valeur précédente : "${action.before}"`;
    this.router.navigate(['/corrections'], { state: { prefillInstruction: instruction } });
  }
}
