import { apiClient } from './client';

export type ImportScope = 'Servers' | 'NetworkDevices' | 'Workstations' | 'Dictionaries';
export type ImportFormat = 'Csv' | 'Xlsx';
export type ImportConflictStrategy = 'Skip' | 'Update' | 'CreateNew';

export interface ImportJob {
  id: string;
  scope: string;
  format: string;
  status: string;
  conflictStrategy: string;
  dryRun: boolean;
  storagePath: string;
  originalFileName: string;
  createdAtUtc: string;
  completedAtUtc?: string;
  createdBy: string;
  failureReason?: string;
  processedRows?: number;
  createdRows?: number;
  updatedRows?: number;
  skippedRows?: number;
  resultLog?: string;
  sendEmail: boolean;
  emailRecipients?: string;
}

export interface QueueImportPayload {
  scope: ImportScope;
  format: ImportFormat;
  conflictStrategy: ImportConflictStrategy;
  dryRun: boolean;
  storagePath: string;
  fileName: string;
  contentBase64: string;
  sendEmail: boolean;
  emailRecipients?: string;
  mappingJson?: string;
}

export async function queueImportJob(payload: QueueImportPayload): Promise<ImportJob> {
  const response = await apiClient.post('/imports', payload);
  return response.data;
}

export async function listImportJobs(page = 1, size = 50): Promise<ImportJob[]> {
  const response = await apiClient.get('/imports', { params: { page, size } });
  return response.data;
}

export function buildImportLogUrl(id: string): string {
  return `/api/imports/${id}/log`;
}
