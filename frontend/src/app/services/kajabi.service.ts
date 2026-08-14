import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { environment } from '../../environments/environment';
import { KajabiStatus } from '../models/kajabi.model';
import { DemoDataService } from './demo-data.service';

@Injectable({ providedIn: 'root' })
export class KajabiService {
  private readonly http = inject(HttpClient);
  private readonly demo = inject(DemoDataService);

  readonly isDemo = environment.demo;

  getStatus(): Observable<KajabiStatus> {
    if (environment.demo) {
      return of(this.demo.kajabiStatus);
    }
    return this.http.get<KajabiStatus>(`${environment.apiBaseUrl}/api/kajabi/status`);
  }

  /** URL de démarrage de l'OAuth Kajabi — navigation directe (redirection serveur), pas un appel HTTP. */
  connectUrl(): string {
    return `${environment.apiBaseUrl}/api/kajabi/connect`;
  }
}
