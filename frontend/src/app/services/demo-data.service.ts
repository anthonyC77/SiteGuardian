import { Injectable } from '@angular/core';
import { BehaviorSubject } from 'rxjs';
import {
  AuditJob,
  AuditStatus,
  Finding,
  FindingEffort,
  FindingSeverity,
} from '../models/audit.model';
import { KajabiStatus } from '../models/kajabi.model';
import { CorrectionAction, CorrectionPlan, CorrectionPlanStatus } from '../models/correction.model';

/**
 * Données figées servant le mode démo GitHub Pages (§8 du plan) : aucune écriture
 * réelle, aucun appel réseau — juste de quoi montrer les 3 écrans avec un scénario
 * crédible, calqué sur les tests d'acceptation du §10.
 */
@Injectable({ providedIn: 'root' })
export class DemoDataService {
  private readonly demoAuditId = 'demo-audit-0001';

  readonly kajabiStatus: KajabiStatus = {
    connected: true,
    siteId: 'demo-site-example',
    scopes: 'read write',
    connectedAt: '2026-08-01T09:00:00Z',
    expiresAt: '2026-09-01T09:00:00Z',
    needsReconnect: false,
  };

  readonly auditJob: AuditJob = {
    id: this.demoAuditId,
    targetUrl: 'https://example.com',
    status: AuditStatus.Completed,
    createdAt: '2026-08-10T08:00:00Z',
    completedAt: '2026-08-10T08:04:00Z',
    estimatedCostEur: 0.18,
    source: 'script',
    pagesAudited: 49,
    error: null,
    findings: this.buildFindings(),
  };

  private readonly correctionPlans$ = new BehaviorSubject<CorrectionPlan[]>(this.buildCorrectionHistory());

  private buildFindings(): Finding[] {
    return [
      {
        id: 'demo-finding-0001',
        auditJobId: this.demoAuditId,
        findingId: 'content-typo-accompagnement',
        title: '« acccompagnement » (faute de frappe) dans le bandeau du thème',
        severity: FindingSeverity.High,
        category: 'content',
        evidence: 'Announcement Bar — « …notre acccompagnement personnalisé… »',
        impact: "Faute visible sur chaque page publique du site, nuit à la crédibilité.",
        fix: 'Remplacer « acccompagnement » par « accompagnement » dans le contenu du thème.',
        effort: FindingEffort.Low,
        sourceAgent: 'web-audit-script',
        pages: this.samplePages(43),
        pageCount: 43,
      },
      {
        id: 'demo-finding-0002',
        auditJobId: this.demoAuditId,
        findingId: 'content-default-kajabi-footer',
        title: 'Mention « © Kajabi » restée dans le pied de page',
        severity: FindingSeverity.Medium,
        category: 'content',
        evidence: 'Footer — « © Kajabi, tous droits réservés »',
        impact: "Texte par défaut de la plateforme, jamais remplacé par le nom du site.",
        fix: 'Remplacer « © Kajabi » par « © Example » dans le contenu du thème (footer).',
        effort: FindingEffort.Low,
        sourceAgent: 'web-audit-script',
        pages: this.samplePages(49),
        pageCount: 49,
      },
      {
        id: 'demo-finding-0003',
        auditJobId: this.demoAuditId,
        findingId: 'links-broken-youtube-footer',
        title: 'Lien YouTube cassé dans le pied de page',
        severity: FindingSeverity.High,
        category: 'links',
        evidence: 'Footer — lien vers "youtube.com/@example-channel-old" → 404',
        impact: 'Lien social visible sur tout le site pointant vers une page inexistante.',
        fix: 'Mettre à jour le lien vers https://www.youtube.com/@example-channel.',
        effort: FindingEffort.Low,
        sourceAgent: 'web-audit-script',
        pages: this.samplePages(49),
        pageCount: 49,
      },
      {
        id: 'demo-finding-0004',
        auditJobId: this.demoAuditId,
        findingId: 'content-typo-blog-category-finitude',
        title: '« finitiude » (faute de frappe) dans une catégorie de blog',
        severity: FindingSeverity.Medium,
        category: 'content',
        evidence: 'Blog — catégorie « finitiude » utilisée sur 6 articles',
        impact: 'Faute visible dans la navigation du blog et les URLs de catégorie.',
        fix: 'Renommer la catégorie « finitiude » en « finitude » et corriger les 6 articles associés.',
        effort: FindingEffort.Medium,
        sourceAgent: 'web-audit-script',
        pages: this.samplePages(6),
        pageCount: 6,
      },
      {
        id: 'demo-finding-0005',
        auditJobId: this.demoAuditId,
        findingId: 'seo-meta-description-missing',
        title: 'Meta description absente sur des landing pages',
        severity: FindingSeverity.Medium,
        category: 'seo',
        evidence: '4 landing pages sans balise <meta name="description">',
        impact: 'Google génère un extrait aléatoire dans les résultats de recherche.',
        fix: 'Rédiger une meta description de 120-155 caractères par landing page concernée.',
        effort: FindingEffort.Medium,
        sourceAgent: 'seo-auditor',
        pages: this.samplePages(4),
        pageCount: 4,
      },
      {
        id: 'demo-finding-0006',
        auditJobId: this.demoAuditId,
        findingId: 'accessibility-images-missing-alt',
        title: 'Images sans attribut alt',
        severity: FindingSeverity.Low,
        category: 'accessibility',
        evidence: '11 images sans texte alternatif détectées sur le site',
        impact: 'Inaccessible aux lecteurs d\'écran, pénalise aussi le SEO image.',
        fix: 'Ajouter un attribut alt descriptif à chaque image concernée.',
        effort: FindingEffort.Medium,
        sourceAgent: 'accessibility-auditor',
        pages: this.samplePages(9),
        pageCount: 9,
      },
    ];
  }

  private samplePages(count: number): string[] {
    const paths = ['/', '/a-propos', '/programmes', '/blog', '/contact', '/tarifs'];
    return Array.from({ length: Math.min(count, paths.length) }, (_, i) => `https://example.com${paths[i]}`);
  }

  private buildCorrectionHistory(): CorrectionPlan[] {
    const applied = this.correctionPlan({
      id: 'demo-plan-applied-0001',
      instruction: 'Corrige acccompagnement partout',
      resume: '1 remplacement dans le bandeau du thème, visible sur 43 pages.',
      status: CorrectionPlanStatus.Applied,
      createdAt: '2026-08-05T10:00:00Z',
      decidedAt: '2026-08-05T10:02:00Z',
      inputTokens: 3400,
      outputTokens: 210,
      toolCallCount: 3,
      estimatedCostEur: 0.006,
      actions: [
        this.action({
          id: 'demo-action-applied-0001',
          tool: 'update_theme_content',
          target: 'Announcement Bar — thème du site',
          before: 'Découvrez notre acccompagnement personnalisé',
          after: 'Découvrez notre accompagnement personnalisé',
          applied: true,
          appliedAt: '2026-08-05T10:02:00Z',
          postWriteVerified: true,
          verificationNote: 'Relecture get_theme_content : contenu conforme.',
        }),
      ],
    });

    const rejected = this.correctionPlan({
      id: 'demo-plan-rejected-0001',
      instruction: 'Répare le lien YouTube du footer',
      resume: '1 remplacement du lien YouTube dans le footer, visible sur 49 pages.',
      status: CorrectionPlanStatus.Rejected,
      createdAt: '2026-08-06T14:00:00Z',
      decidedAt: '2026-08-06T14:05:00Z',
      inputTokens: 2900,
      outputTokens: 180,
      toolCallCount: 2,
      estimatedCostEur: 0.005,
      actions: [
        this.action({
          id: 'demo-action-rejected-0001',
          tool: 'update_theme_content',
          target: 'Footer — icône sociale YouTube',
          before: 'https://youtube.com/@example-channel-old',
          after: 'https://www.youtube.com/@example-channel',
          applied: false,
        }),
      ],
    });

    const needsClarification = this.correctionPlan({
      id: 'demo-plan-clarification-0001',
      instruction: 'Améliore le SEO',
      resume: null,
      status: CorrectionPlanStatus.NeedsClarification,
      clarification:
        "Instruction trop générale : quelles pages viser (landing pages, articles de blog) et sur quel élément (title, meta description, balises H1) ?",
      createdAt: '2026-08-07T09:30:00Z',
      decidedAt: null,
      inputTokens: 1500,
      outputTokens: 90,
      toolCallCount: 1,
      estimatedCostEur: 0.002,
      actions: [],
    });

    return [needsClarification, rejected, applied];
  }

  private correctionPlan(p: {
    id: string;
    instruction: string;
    resume: string | null;
    status: CorrectionPlanStatus;
    clarification?: string;
    createdAt: string;
    decidedAt: string | null;
    inputTokens: number;
    outputTokens: number;
    toolCallCount: number;
    estimatedCostEur: number;
    actions: CorrectionAction[];
  }): CorrectionPlan {
    return {
      id: p.id,
      instruction: p.instruction,
      resume: p.resume,
      status: p.status,
      clarification: p.clarification ?? null,
      modelUsed: 'claude-haiku-4-5-20251001',
      escalatedToFallback: false,
      inputTokens: p.inputTokens,
      outputTokens: p.outputTokens,
      toolCallCount: p.toolCallCount,
      estimatedCostEur: p.estimatedCostEur,
      error: null,
      createdAt: p.createdAt,
      decidedAt: p.decidedAt,
      actions: p.actions,
    };
  }

  private action(a: {
    id: string;
    tool: string;
    target: string;
    before: string;
    after: string;
    applied: boolean;
    appliedAt?: string;
    postWriteVerified?: boolean;
    verificationNote?: string;
  }): CorrectionAction {
    return {
      id: a.id,
      correctionPlanId: '',
      tool: a.tool,
      target: a.target,
      before: a.before,
      after: a.after,
      argsJson: '{}',
      blocked: false,
      blockedReason: null,
      applied: a.applied,
      appliedAt: a.appliedAt ?? null,
      postWriteVerified: a.postWriteVerified ?? null,
      verificationNote: a.verificationNote ?? null,
      applyError: null,
    };
  }

  listCorrectionPlans(): CorrectionPlan[] {
    return this.correctionPlans$.value;
  }

  watchCorrectionPlans() {
    return this.correctionPlans$.asObservable();
  }

  addCorrectionPlan(plan: CorrectionPlan): void {
    this.correctionPlans$.next([plan, ...this.correctionPlans$.value]);
  }

  updateCorrectionPlan(plan: CorrectionPlan): void {
    this.correctionPlans$.next(
      this.correctionPlans$.value.map((p) => (p.id === plan.id ? plan : p)),
    );
  }

  findCorrectionPlan(id: string): CorrectionPlan | undefined {
    return this.correctionPlans$.value.find((p) => p.id === id);
  }
}
