import { apiClient } from './client';

export interface AuditLogEntry {
  id: string;
  entityType: string;
  entityId: string;
  action: string;
  performedBy: string;
  roles?: string | null;
  performedAtUtc: string;
  changeSummary?: string | null;
  changedFieldsJson?: string | null;
}

export const getAuditLogs = async (entityType?: string) => {
  const params = entityType ? { entityType } : undefined;
  const { data } = await apiClient.get<AuditLogEntry[]>('/audit', { params });
  return data;
};
