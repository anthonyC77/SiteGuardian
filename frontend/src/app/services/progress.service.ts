import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../environments/environment';
import { AuditProgressEvent } from '../models/audit.model';

/**
 * Connexion SignalR vers /hubs/progress (ProgressHub) pour la progression d'audit
 * en direct (§2/§8 du plan). No-op en mode démo : il n'y a pas de backend.
 */
@Injectable({ providedIn: 'root' })
export class ProgressService {
  private connection?: signalR.HubConnection;
  private readonly progress$ = new Subject<AuditProgressEvent>();

  constructor() {
    if (environment.demo) {
      return;
    }

    this.connection = new signalR.HubConnectionBuilder()
      .withUrl(`${environment.apiBaseUrl}/hubs/progress`)
      .withAutomaticReconnect()
      .build();

    this.connection.on('auditProgress', (event: AuditProgressEvent) => this.progress$.next(event));

    this.connection.start().catch((err: unknown) => {
      console.error('Connexion SignalR /hubs/progress échouée', err);
    });
  }

  onAuditProgress() {
    return this.progress$.asObservable();
  }
}
