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

interface AuditLogFilters {
  entityType?: string;
  user?: string;
  action?: string;
}

export const getAuditLogs = async (filters: AuditLogFilters = {}) => {
  const params = Object.fromEntries(
    Object.entries(filters).filter(([, value]) => value !== undefined && value !== null && value !== '')
  );
  const { data } = await apiClient.get<AuditLogEntry[]>('/audit', { params: Object.keys(params).length ? params : undefined });
  return data;
};
