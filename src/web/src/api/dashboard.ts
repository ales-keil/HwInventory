import { apiClient } from './client';

export interface DashboardSummary {
  servers: number;
  networkDevices: number;
  workstations: number;
  latestChanges: AuditEntry[];
  security: SecurityMetrics;
}

export interface AuditEntry {
  entityType: string;
  action: string;
  performedBy: string;
  performedAtUtc: string;
}

export interface SecurityMetrics {
  totalUsers: number;
  activeUsers: number;
  totpEnabled: number;
  totpRequired: number;
  lockedOut: number;
  pendingPasswordResets: number;
  activeSessions: number;
  generatedAtUtc: string;
}

export const getDashboardSummary = async (): Promise<DashboardSummary> => {
  const { data } = await apiClient.get<DashboardSummary>('/dashboard/summary');
  return data;
};
