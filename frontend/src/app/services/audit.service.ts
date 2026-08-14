import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, delay, of } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuditJob } from '../models/audit.model';
import { DemoDataService } from './demo-data.service';

const LAST_AUDIT_ID_KEY = 'siteguardian.lastAuditId';

@Injectable({ providedIn: 'root' })
export class AuditService {
  private readonly http = inject(HttpClient);
  private readonly demo = inject(DemoDataService);

  readonly isDemo = environment.demo;

  startAudit(url: string): Observable<{ id: string }> {
    if (environment.demo) {
      return of({ id: this.demo.auditJob.id }).pipe(delay(300));
    }
    return this.http.post<{ id: string }>(`${environment.apiBaseUrl}/api/audit`, { url });
  }

  getAudit(id: string): Observable<AuditJob> {
    if (environment.demo) {
      return of(this.demo.auditJob).pipe(delay(200));
    }
    return this.http.get<AuditJob>(`${environment.apiBaseUrl}/api/audit/${id}`);
  }

  pdfUrl(id: string): string {
    return `${environment.apiBaseUrl}/api/audit/${id}/pdf`;
  }

  /** Le navigateur est la seule mémoire d'un « dernier audit » : l'API n'expose pas de liste (§7). */
  get lastAuditId(): string | null {
    return localStorage.getItem(LAST_AUDIT_ID_KEY);
  }

  set lastAuditId(id: string | null) {
    if (id) {
      localStorage.setItem(LAST_AUDIT_ID_KEY, id);
    } else {
      localStorage.removeItem(LAST_AUDIT_ID_KEY);
    }
  }
}
