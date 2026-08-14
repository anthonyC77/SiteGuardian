// Miroir de SiteGuardian.Api.Models.CorrectionPlan / CorrectionAction (mêmes
// remarques sur la sérialisation des enums que audit.model.ts).

export enum CorrectionPlanStatus {
  AwaitingReview = 0,
  NeedsClarification = 1,
  Applied = 2,
  PartiallyApplied = 3,
  Rejected = 4,
  Failed = 5,
}

export interface CorrectionAction {
  id: string;
  correctionPlanId: string;
  tool: string;
  target: string;
  before?: string | null;
  after?: string | null;
  argsJson: string;
  blocked: boolean;
  blockedReason?: string | null;
  applied: boolean;
  appliedAt?: string | null;
  postWriteVerified?: boolean | null;
  verificationNote?: string | null;
  applyError?: string | null;
}

export interface CorrectionPlan {
  id: string;
  instruction: string;
  resume?: string | null;
  status: CorrectionPlanStatus;
  clarification?: string | null;
  modelUsed: string;
  escalatedToFallback: boolean;
  inputTokens: number;
  outputTokens: number;
  toolCallCount: number;
  estimatedCostEur: number;
  error?: string | null;
  createdAt: string;
  decidedAt?: string | null;
  actions: CorrectionAction[];
}
