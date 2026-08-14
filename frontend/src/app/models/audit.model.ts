// Miroir de SiteGuardian.Api.Models.AuditJob / Finding (System.Text.Json sérialise
// les enums C# en entiers par défaut — l'ordre des membres ci-dessous doit rester
// identique à celui du backend).

export enum AuditStatus {
  Pending = 0,
  Running = 1,
  Completed = 2,
  Failed = 3,
}

export enum FindingSeverity {
  Critical = 0,
  High = 1,
  Medium = 2,
  Low = 3,
  Info = 4,
}

export enum FindingEffort {
  Low = 0,
  Medium = 1,
  High = 2,
}

export interface Finding {
  id: string;
  auditJobId: string;
  findingId: string;
  title: string;
  severity: FindingSeverity;
  category: string;
  evidence?: string | null;
  impact?: string | null;
  fix?: string | null;
  effort: FindingEffort;
  sourceAgent: string;
  pages: string[];
  pageCount: number;
}

export interface AuditJob {
  id: string;
  targetUrl: string;
  status: AuditStatus;
  createdAt: string;
  completedAt?: string | null;
  estimatedCostEur: number;
  source: string;
  pagesAudited: number;
  error?: string | null;
  findings: Finding[];
}

export interface AuditProgressEvent {
  jobId: string;
  status: string;
  message: string;
}
