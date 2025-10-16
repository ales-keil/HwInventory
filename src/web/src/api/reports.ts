import { apiClient } from './client';
import { ReportFormat, ReportRecurrence, ReportScope } from './reports.types';

export interface ReportDefinition {
  id: string;
  name: string;
  description?: string | null;
  scope: ReportScope;
  format: ReportFormat;
  recurrence: ReportRecurrence;
  filterJson?: string | null;
  recipients?: string | null;
  storagePath: string;
  runAtTime?: string | null;
  runOnDayOfWeek?: number | null;
  runOnDayOfMonth?: number | null;
  enabled: boolean;
  nextRunAtUtc?: string | null;
  lastRunAtUtc?: string | null;
  createdAtUtc: string;
  modifiedAtUtc?: string | null;
  createdBy: string;
  modifiedBy?: string | null;
}

export interface ReportRun {
  id: string;
  reportDefinitionId: string;
  startedAtUtc: string;
  completedAtUtc?: string | null;
  status: string;
  artifactPath?: string | null;
  failureReason?: string | null;
}

export interface CreateReportPayload {
  name: string;
  description?: string | null;
  scope: ReportScope;
  format: ReportFormat;
  recurrence: ReportRecurrence;
  filterJson?: string | null;
  recipients?: string | null;
  storagePath: string;
  runAtTime?: string | null;
  runOnDayOfWeek?: number | null;
  runOnDayOfMonth?: number | null;
  enabled: boolean;
}

export interface UpdateReportPayload extends CreateReportPayload {}

export const listReports = async (page = 1, size = 20) => {
  const { data } = await apiClient.get<ReportDefinition[]>('/reports', { params: { page, size } });
  return data;
};

export const getReport = async (id: string) => {
  const { data } = await apiClient.get<ReportDefinition>(`/reports/${id}`);
  return data;
};

export const createReport = async (payload: CreateReportPayload) => {
  const { data } = await apiClient.post<ReportDefinition>('/reports', payload);
  return data;
};

export const updateReport = async (id: string, payload: UpdateReportPayload) => {
  const { data } = await apiClient.put<ReportDefinition>(`/reports/${id}`, payload);
  return data;
};

export const deleteReport = async (id: string) => {
  await apiClient.delete(`/reports/${id}`);
};

export const triggerReport = async (id: string) => {
  const { data } = await apiClient.post<ReportRun>(`/reports/${id}/run`, {});
  return data;
};

export const listReportRuns = async (id: string, page = 1, size = 20) => {
  const { data } = await apiClient.get<ReportRun[]>(`/reports/${id}/runs`, { params: { page, size } });
  return data;
};

export const buildReportDownloadUrl = (runId: string) => `/api/reports/runs/${runId}/download`;
