import { apiClient } from './client';

export interface ExportJob {
  id: string;
  scope: string;
  format: string;
  storagePath: string;
  fileName: string;
  status: string;
  fileSizeBytes?: number | null;
  createdAtUtc: string;
  completedAtUtc?: string | null;
  expiresAtUtc?: string | null;
  createdBy: string;
  failureReason?: string | null;
  sendEmail: boolean;
  emailRecipients?: string | null;
  artifactPath?: string | null;
}

export interface QueueExportPayload {
  scope: string;
  format: string;
  storagePath: string;
  filterJson?: string | null;
  sendEmail: boolean;
  emailRecipients?: string | null;
}

export const listExportJobs = async (page = 1, size = 20) => {
  const { data } = await apiClient.get<ExportJob[]>(`/exports/history`, {
    params: { page, size }
  });
  return data;
};

export const queueExportJob = async (payload: QueueExportPayload) => {
  const { data } = await apiClient.post<ExportJob>('/exports', payload);
  return data;
};

export const getExportJob = async (id: string) => {
  const { data } = await apiClient.get<ExportJob>(`/exports/${id}`);
  return data;
};

export const buildExportDownloadUrl = (id: string) => `/api/exports/${id}/artifact`;
