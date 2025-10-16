import { apiClient } from './client';

export interface StorageConnectorModel {
  id: string;
  alias: string;
  enabled: boolean;
  type: string;
  path?: string | null;
  endpoint?: string | null;
  bucket?: string | null;
  folder?: string | null;
  region?: string | null;
  username?: string | null;
  domain?: string | null;
  publicUrlBase?: string | null;
  retentionDays?: number | null;
  useSsl?: boolean | null;
  hasPassword: boolean;
  hasAccessKeys: boolean;
  healthStatus?: string | null;
  lastTestedAtUtc?: string | null;
}

export interface StorageConnectorUpdate {
  alias: string;
  enabled: boolean;
  type: string;
  path?: string | null;
  endpoint?: string | null;
  bucket?: string | null;
  folder?: string | null;
  region?: string | null;
  username?: string | null;
  domain?: string | null;
  publicUrlBase?: string | null;
  retentionDays?: number | null;
  useSsl?: boolean | null;
  rotateSecret: boolean;
  password?: string | null;
  accessKey?: string | null;
  secretKey?: string | null;
}

export interface StorageConnectorTestResult {
  success: boolean;
  message: string;
}

export async function fetchStorageConnector(): Promise<StorageConnectorModel | null> {
  const response = await apiClient.get<StorageConnectorModel | null>('/api/settings/storage');
  return response.data ?? null;
}

export async function saveStorageConnector(payload: StorageConnectorUpdate): Promise<StorageConnectorModel> {
  const response = await apiClient.put<StorageConnectorModel>('/api/settings/storage', payload);
  return response.data;
}

export async function testStorageConnector(): Promise<StorageConnectorTestResult> {
  const response = await apiClient.post<StorageConnectorTestResult>('/api/settings/storage/test', {});
  return response.data;
}
