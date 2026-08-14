import { DatePipe } from '@angular/common';
import { Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Subscription } from 'rxjs';
import { AuditJob, AuditStatus, FindingSeverity } from '../../models/audit.model';
import { KajabiStatus } from '../../models/kajabi.model';
import { AuditService } from '../../services/audit.service';
import { KajabiService } from '../../services/kajabi.service';
import { ProgressService } from '../../services/progress.service';

const DEMO_AUDIT_ID = 'demo-audit-0001';

@Component({
  selector: 'app-dashboard',
  imports: [RouterLink, DatePipe],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent implements OnInit, OnDestroy {
  private readonly auditService = inject(AuditService);
  private readonly kajabiService = inject(KajabiService);
  private readonly progressService = inject(ProgressService);

  private progressSub?: Subscription;

  readonly isDemo = this.auditService.isDemo;
  readonly targetUrl = signal('https://example.com');
  readonly kajabiStatus = signal<KajabiStatus | null>(null);
  readonly auditJob = signal<AuditJob | null>(null);
  readonly progressMessage = signal<string | null>(null);
  readonly launching = signal(false);
  readonly loadError = signal<string | null>(null);

  readonly urgentCount = computed(
    () =>
      this.auditJob()?.findings.filter((f) => f.severity === FindingSeverity.Critical).length ?? 0,
  );
  readonly importantCount = computed(
    () =>
      this.auditJob()?.findings.filter((f) => f.severity === FindingSeverity.High).length ?? 0,
  );
  readonly totalFindings = computed(() => this.auditJob()?.findings.length ?? 0);

  readonly AuditStatus = AuditStatus;

  ngOnInit(): void {
    this.kajabiService.getStatus().subscribe({
      next: (status) => this.kajabiStatus.set(status),
      error: () => this.loadError.set('Impossible de récupérer le statut de connexion Kajabi.'),
    });

    const lastId = this.isDemo ? DEMO_AUDIT_ID : this.auditService.lastAuditId;
    if (lastId) {
      this.loadAudit(lastId);
    }

    if (!this.isDemo) {
      this.progressSub = this.progressService.onAuditProgress().subscribe((event) => {
        if (event.jobId !== this.auditJob()?.id && event.jobId !== this.auditService.lastAuditId) {
          return;
        }
        this.progressMessage.set(event.message);
        if (event.status === 'completed' || event.status === 'failed') {
          this.loadAudit(event.jobId);
        }
      });
    }
  }

  ngOnDestroy(): void {
    this.progressSub?.unsubscribe();
  }

  launchAudit(): void {
    const url = this.targetUrl().trim();
    if (!url) return;

    this.launching.set(true);
    this.loadError.set(null);
    this.progressMessage.set(this.isDemo ? null : 'Démarrage de l\'audit…');

    this.auditService.startAudit(url).subscribe({
      next: ({ id }) => {
        this.auditService.lastAuditId = id;
        this.launching.set(false);
        if (this.isDemo) {
          this.loadAudit(id);
        }
        // En mode réel, la progression puis le résultat arrivent via SignalR (auditProgress).
      },
      error: (err: unknown) => {
        this.launching.set(false);
        this.progressMessage.set(null);
        this.loadError.set(this.errorMessage(err));
      },
    });
  }

  pdfUrl(): string | null {
    const job = this.auditJob();
    return job && job.status === AuditStatus.Completed ? this.auditService.pdfUrl(job.id) : null;
  }

  connectKajabiUrl(): string {
    return this.kajabiService.connectUrl();
  }

  private loadAudit(id: string): void {
    this.auditService.getAudit(id).subscribe({
      next: (job) => {
        this.auditJob.set(job);
        if (job.status === AuditStatus.Completed || job.status === AuditStatus.Failed) {
          this.progressMessage.set(null);
        }
      },
      error: (err: unknown) => this.loadError.set(this.errorMessage(err)),
    });
  }

  private errorMessage(err: unknown): string {
    if (err && typeof err === 'object' && 'error' in err) {
      const body = (err as { error?: { error?: string } }).error;
      if (body?.error) return body.error;
    }
    return "Une erreur est survenue lors de l'appel à l'API.";
  }
}
