import { apiClient } from './client';

export interface UpdatePackageResponse {
  id: string;
  version: string;
  fileName: string;
  status: string;
  sha256: string;
  preserveDatabaseConfiguration: boolean;
  createBackupBeforeInstall: boolean;
  performIntegrityCheck: boolean;
  confirmedBackupAvailable: boolean;
  notes?: string;
  createdAtUtc: string;
  completedAtUtc?: string;
  createdBy: string;
  failureReason?: string;
  manifestJson?: string;
  logPath?: string;
}

export interface UploadUpdatePayload {
  file: File;
  version?: string;
  preserveDatabaseConfiguration: boolean;
  createBackupBeforeInstall: boolean;
  backupStoragePath?: string;
  performIntegrityCheck: boolean;
  confirmedBackupAvailable: boolean;
  notes?: string;
}

export async function listUpdateHistory(page = 1, size = 20) {
  const response = await apiClient.get<UpdatePackageResponse[]>(`/api/updates/history?page=${page}&size=${size}`);
  return response.data;
}

export async function uploadUpdatePackage(payload: UploadUpdatePayload) {
  const form = new FormData();
  form.append('Package', payload.file);
  if (payload.version) {
    form.append('Version', payload.version);
  }
  form.append('PreserveDatabaseConfiguration', String(payload.preserveDatabaseConfiguration));
  form.append('CreateBackupBeforeInstall', String(payload.createBackupBeforeInstall));
  form.append('PerformIntegrityCheck', String(payload.performIntegrityCheck));
  form.append('ConfirmedBackupAvailable', String(payload.confirmedBackupAvailable));
  if (payload.backupStoragePath) {
    form.append('BackupStoragePath', payload.backupStoragePath);
  }
  if (payload.notes) {
    form.append('Notes', payload.notes);
  }

  const response = await apiClient.post<UpdatePackageResponse>('/api/updates', form, {
    headers: { 'Content-Type': 'multipart/form-data' }
  });
  return response.data;
}

export async function downloadUpdateLog(id: string) {
  const response = await apiClient.get(`/api/updates/${id}/log`, { responseType: 'blob' });
  return response.data;
}
