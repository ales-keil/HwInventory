import { apiClient } from './client';

export interface BackupJob {
  id: string;
  scope: string;
  fileName: string;
  storagePath: string;
  encryptionEnabled: boolean;
  sendEmail: boolean;
  integrityCheckEnabled: boolean;
  isAutomatic: boolean;
  status: string;
  fileSizeBytes?: number | null;
  completedAtUtc?: string | null;
  integrityPassed?: boolean | null;
  createdAtUtc: string;
  createdBy: string;
  failureReason?: string | null;
}

export interface QueueBackupPayload {
  scope: string;
  storagePath: string;
  encryptionEnabled: boolean;
  password?: string | null;
  passwordConfirmation?: string | null;
  sendEmail: boolean;
  emailRecipients?: string | null;
  integrityCheckEnabled: boolean;
}

export interface RestoreBackupPayload {
  password?: string | null;
  performIntegrityTest: boolean;
}

export interface IntegrityTestPayload {
  password?: string | null;
}

export interface BackupSchedule {
  enabled: boolean;
  frequency: string;
  dayOfWeek?: number | null;
  dayOfMonth?: number | null;
  executionTimeUtc: string;
  scope: string;
  storagePath: string;
  encryptionEnabled: boolean;
  hasStoredPassword: boolean;
  sendEmail: boolean;
  emailRecipients?: string | null;
  integrityCheckEnabled: boolean;
  lastRunAtUtc?: string | null;
  nextRunAtUtc?: string | null;
}

export interface BackupScheduleUpdatePayload {
  enabled: boolean;
  frequency: string;
  dayOfWeek?: number | null;
  dayOfMonth?: number | null;
  executionTimeUtc: string;
  scope: string;
  storagePath: string;
  encryptionEnabled: boolean;
  password?: string | null;
  passwordConfirmation?: string | null;
  rotatePassword: boolean;
  sendEmail: boolean;
  emailRecipients?: string | null;
  integrityCheckEnabled: boolean;
}

export const listBackups = async (page = 1, size = 20) => {
  const { data } = await apiClient.get<BackupJob[]>(`/maintenance/backups/history`, {
    params: { page, size }
  });
  return data;
};

export const queueBackup = async (payload: QueueBackupPayload) => {
  const { data } = await apiClient.post<BackupJob>('/maintenance/backups', payload);
  return data;
};

export const getBackup = async (id: string) => {
  const { data } = await apiClient.get<BackupJob>(`/maintenance/backups/${id}`);
  return data;
};

export const restoreBackup = async (id: string, payload: RestoreBackupPayload) => {
  const { data } = await apiClient.post<{ message: string }>(`/maintenance/backups/${id}/restore`, payload);
  return data;
};

export const testBackupIntegrity = async (id: string, payload: IntegrityTestPayload) => {
  const { data } = await apiClient.post<{ message: string; passed: boolean | null }>(
    `/maintenance/backups/${id}/test`,
    payload
  );
  return data;
};

export const fetchBackupSchedule = async () => {
  const { data } = await apiClient.get<BackupSchedule>('/maintenance/schedule');
  return data;
};

export const updateBackupSchedule = async (payload: BackupScheduleUpdatePayload) => {
  const { data } = await apiClient.put<BackupSchedule>('/maintenance/schedule', payload);
  return data;
};
