import { apiClient } from './client';

export interface EmailSettings {
  id: string;
  alias: string;
  enabled: boolean;
  host: string;
  port: number;
  useTls: boolean;
  username?: string | null;
  hasPassword: boolean;
  fromAddress?: string | null;
  replyToAddress?: string | null;
  healthStatus?: string | null;
  lastTestedAtUtc?: string | null;
}

export interface EmailSettingsUpdate {
  alias: string;
  enabled: boolean;
  host: string;
  port: number;
  useTls: boolean;
  username?: string | null;
  fromAddress?: string | null;
  replyToAddress?: string | null;
  rotateSecret: boolean;
  password?: string | null;
}

export interface EmailTestRequest {
  recipient: string;
  subject: string;
  body: string;
}

export interface EmailTestResult {
  success: boolean;
  message: string;
}

export async function fetchEmailSettings(): Promise<EmailSettings | null> {
  const response = await apiClient.get<EmailSettings | null>('/api/settings/email');
  return response.data ?? null;
}

export async function saveEmailSettings(payload: EmailSettingsUpdate): Promise<EmailSettings> {
  const response = await apiClient.put<EmailSettings>('/api/settings/email', payload);
  return response.data;
}

export async function sendEmailTest(payload: EmailTestRequest): Promise<EmailTestResult> {
  const response = await apiClient.post<EmailTestResult>('/api/settings/email/test', payload);
  return response.data;
}
