// Miroir de la réponse anonyme de GET /api/kajabi/status (Program.cs).

export interface KajabiStatus {
  connected: boolean;
  siteId?: string | null;
  scopes?: string | null;
  connectedAt?: string | null;
  expiresAt?: string | null;
  needsReconnect: boolean;
}
