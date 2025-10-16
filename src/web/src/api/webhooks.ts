import { apiClient } from './client';

export interface WebhookConnector {
  id: string;
  alias: string;
  enabled: boolean;
  url: string;
  method: string;
  contentType?: string | null;
  headers: Record<string, string>;
  useSignature: boolean;
  hasSecret: boolean;
  healthStatus?: string | null;
  lastTestedAtUtc?: string | null;
}

export interface WebhookConnectorUpdate {
  alias?: string | null;
  enabled: boolean;
  url: string;
  method: string;
  contentType?: string | null;
  headers: Record<string, string>;
  useSignature: boolean;
  signingSecret?: string | null;
  rotateSecret: boolean;
}

export interface WebhookTestRequest {
  eventType?: string | null;
  payloadJson?: string | null;
}

export interface WebhookTestResponse {
  success: boolean;
  message: string;
  responseSnippet?: string | null;
}

export const fetchWebhookConnector = async (): Promise<WebhookConnector | null> => {
  const response = await apiClient.get<WebhookConnector | null>('/api/settings/webhooks');
  return response.data;
};

export const saveWebhookConnector = async (payload: WebhookConnectorUpdate): Promise<WebhookConnector> => {
  const response = await apiClient.put<WebhookConnector>('/api/settings/webhooks', payload);
  return response.data;
};

export const testWebhookConnector = async (payload: WebhookTestRequest): Promise<WebhookTestResponse> => {
  const response = await apiClient.post<WebhookTestResponse>('/api/settings/webhooks/test', payload);
  return response.data;
};
