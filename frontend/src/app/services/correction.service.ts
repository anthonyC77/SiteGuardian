import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, delay, of, throwError } from 'rxjs';
import { environment } from '../../environments/environment';
import { CorrectionAction, CorrectionPlan, CorrectionPlanStatus } from '../models/correction.model';
import { DemoDataService } from './demo-data.service';

interface DemoScenario {
  /** Mots-clés dont au moins un doit apparaître dans l'instruction (insensible à la casse). */
  keywords: string[];
  resume: string;
  actions: Array<{
    tool: string;
    target: string;
    before: string;
    after: string;
    blocked?: boolean;
    blockedReason?: string;
  }>;
}

// Scénarios calqués sur les tests d'acceptation du §10 du plan — le mode démo n'a
// pas de backend, donc pas de vrai appel LLM/MCP, mais reste crédible sur ces instructions.
const DEMO_SCENARIOS: DemoScenario[] = [
  {
    keywords: ['acccompagnement', 'accompagnement'],
    resume: '1 remplacement dans le bandeau du thème, visible sur 43 pages.',
    actions: [
      {
        tool: 'update_theme_content',
        target: 'Announcement Bar — thème du site',
        before: 'Découvrez notre acccompagnement personnalisé',
        after: 'Découvrez notre accompagnement personnalisé',
      },
    ],
  },
  {
    keywords: ['kajabi', 'droits réservés', 'copyright'],
    resume: '1 remplacement dans le pied de page, visible sur 49 pages.',
    actions: [
      {
        tool: 'update_theme_content',
        target: 'Footer — mention de copyright',
        before: '© Kajabi, tous droits réservés',
        after: '© Example, tous droits réservés',
      },
    ],
  },
  {
    keywords: ['youtube'],
    resume: '1 remplacement du lien YouTube dans le pied de page, visible sur 49 pages.',
    actions: [
      {
        tool: 'update_theme_content',
        target: 'Footer — icône sociale YouTube',
        before: 'https://youtube.com/@example-channel-old',
        after: 'https://www.youtube.com/@example-channel',
      },
    ],
  },
  {
    keywords: ['finitiude', 'finitude'],
    resume: 'Renommage de catégorie sur 6 articles de blog.',
    actions: [
      {
        tool: 'update_blog_post',
        target: 'Catégorie de blog « finitiude »',
        before: 'finitiude',
        after: 'finitude',
      },
    ],
  },
  {
    keywords: ['supprime le site', 'delete_site', 'ferme le compte'],
    resume: '',
    actions: [
      {
        tool: 'delete_site',
        target: 'Site complet',
        before: '',
        after: '',
        blocked: true,
        blockedReason: "Tool hors whitelist d'écriture — bloqué côté backend (§4.2/§10 test 6).",
      },
    ],
  },
];

const AMBIGUOUS_KEYWORDS = ['améliore le seo', 'améliore le référencement', 'rends ça mieux'];

@Injectable({ providedIn: 'root' })
export class CorrectionService {
  private readonly http = inject(HttpClient);
  private readonly demo = inject(DemoDataService);

  readonly isDemo = environment.demo;

  propose(instruction: string): Observable<CorrectionPlan> {
    if (environment.demo) {
      const plan = this.buildDemoPlan(instruction);
      this.demo.addCorrectionPlan(plan);
      return of(plan).pipe(delay(500));
    }
    return this.http.post<CorrectionPlan>(`${environment.apiBaseUrl}/api/corrections`, { instruction });
  }

  apply(id: string): Observable<CorrectionPlan> {
    if (environment.demo) {
      const plan = this.demo.findCorrectionPlan(id);
      if (!plan) return throwError(() => new Error('Plan introuvable.'));

      const now = new Date().toISOString();
      const actions = plan.actions.map((a) =>
        a.blocked
          ? a
          : ({
              ...a,
              applied: true,
              appliedAt: now,
              postWriteVerified: true,
              verificationNote: 'Relecture simulée (mode démo) : contenu conforme.',
            } satisfies CorrectionAction),
      );
      const allBlocked = plan.actions.length > 0 && plan.actions.every((a) => a.blocked);
      const applied: CorrectionPlan = {
        ...plan,
        status: allBlocked ? CorrectionPlanStatus.Failed : CorrectionPlanStatus.Applied,
        decidedAt: now,
        actions,
      };
      this.demo.updateCorrectionPlan(applied);
      return of(applied).pipe(delay(400));
    }
    return this.http.post<CorrectionPlan>(`${environment.apiBaseUrl}/api/corrections/${id}/apply`, {});
  }

  reject(id: string): Observable<CorrectionPlan> {
    if (environment.demo) {
      const plan = this.demo.findCorrectionPlan(id);
      if (!plan) return throwError(() => new Error('Plan introuvable.'));

      const rejected: CorrectionPlan = {
        ...plan,
        status: CorrectionPlanStatus.Rejected,
        decidedAt: new Date().toISOString(),
      };
      this.demo.updateCorrectionPlan(rejected);
      return of(rejected).pipe(delay(200));
    }
    return this.http.post<CorrectionPlan>(`${environment.apiBaseUrl}/api/corrections/${id}/reject`, {});
  }

  list(): Observable<CorrectionPlan[]> {
    if (environment.demo) {
      return this.demo.watchCorrectionPlans();
    }
    return this.http.get<CorrectionPlan[]>(`${environment.apiBaseUrl}/api/corrections`);
  }

  get(id: string): Observable<CorrectionPlan> {
    if (environment.demo) {
      const plan = this.demo.findCorrectionPlan(id);
      return plan ? of(plan) : throwError(() => new Error('Plan introuvable.'));
    }
    return this.http.get<CorrectionPlan>(`${environment.apiBaseUrl}/api/corrections/${id}`);
  }

  /**
   * Simule la boucle agentique en mode démo (pas de backend) : reconnaît quelques
   * instructions calquées sur les tests d'acceptation du §10, sinon reproduit un
   * comportement d'échec/clarification cohérent avec l'agent réel.
   */
  private buildDemoPlan(instruction: string): CorrectionPlan {
    const id = `demo-plan-${crypto.randomUUID()}`;
    const lower = instruction.toLowerCase();
    const now = new Date().toISOString();

    if (AMBIGUOUS_KEYWORDS.some((k) => lower.includes(k))) {
      return {
        id,
        instruction,
        resume: null,
        status: CorrectionPlanStatus.NeedsClarification,
        clarification:
          "Instruction trop générale : quelles pages viser (landing pages, articles de blog) et sur quel élément (title, meta description, balises H1) ?",
        modelUsed: 'claude-haiku-4-5-20251001',
        escalatedToFallback: false,
        inputTokens: 1500,
        outputTokens: 90,
        toolCallCount: 1,
        estimatedCostEur: 0.002,
        error: null,
        createdAt: now,
        decidedAt: null,
        actions: [],
      };
    }

    const scenario = DEMO_SCENARIOS.find((s) => s.keywords.some((k) => lower.includes(k)));
    if (!scenario) {
      return {
        id,
        instruction,
        resume: null,
        status: CorrectionPlanStatus.Failed,
        clarification: null,
        modelUsed: 'claude-haiku-4-5-20251001',
        escalatedToFallback: false,
        inputTokens: 800,
        outputTokens: 40,
        toolCallCount: 1,
        estimatedCostEur: 0.001,
        error:
          "Mode démo : aucune correspondance avec les scénarios pré-enregistrés. Essayez par exemple « corrige acccompagnement partout » ou « répare le lien YouTube du footer ».",
        createdAt: now,
        decidedAt: null,
        actions: [],
      };
    }

    const actions: CorrectionAction[] = scenario.actions.map((a, i) => ({
      id: `${id}-action-${i}`,
      correctionPlanId: id,
      tool: a.tool,
      target: a.target,
      before: a.before,
      after: a.after,
      argsJson: '{}',
      blocked: a.blocked ?? false,
      blockedReason: a.blockedReason ?? null,
      applied: false,
      appliedAt: null,
      postWriteVerified: null,
      verificationNote: null,
      applyError: null,
    }));
    const allBlocked = actions.every((a) => a.blocked);

    return {
      id,
      instruction,
      resume: scenario.resume || null,
      status: allBlocked ? CorrectionPlanStatus.Failed : CorrectionPlanStatus.AwaitingReview,
      clarification: null,
      modelUsed: 'claude-haiku-4-5-20251001',
      escalatedToFallback: false,
      inputTokens: 3200,
      outputTokens: 200,
      toolCallCount: 3,
      estimatedCostEur: 0.006,
      error: allBlocked ? "Toutes les actions proposées sont hors whitelist d'écriture." : null,
      createdAt: now,
      decidedAt: null,
      actions,
    };
  }
}
