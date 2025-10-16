import { apiClient } from './client';

export interface ConnectorSummary {
  id?: string;
  key: string;
  type: string;
  name: string;
  enabled: boolean;
  healthStatus?: string | null;
  lastTestedAtUtc?: string | null;
  hasSecret: boolean;
  supportsToggle: boolean;
  supportsTest: boolean;
  supportsRotateSecret: boolean;
  requiresConfiguration: boolean;
  description?: string | null;
}

export interface ConnectorTestResponse {
  success: boolean;
  message: string;
  connector?: ConnectorSummary;
}

export interface ConnectorTestRequest {
  target?: string;
}

export const fetchConnectors = async () => {
  const { data } = await apiClient.get<ConnectorSummary[]>('/connectors');
  return data;
};

export const toggleConnector = async (id: string, enabled: boolean) => {
  const { data } = await apiClient.post<ConnectorSummary>(`/connectors/${id}/toggle?enabled=${enabled}`);
  return data;
};

export const rotateConnectorSecret = async (id: string) => {
  const { data } = await apiClient.post<ConnectorSummary>(`/connectors/${id}/rotate-secret`);
  return data;
};

export const testConnector = async (id: string, payload: ConnectorTestRequest) => {
  const { data } = await apiClient.post<ConnectorTestResponse>(`/connectors/${id}/test`, payload);
  return data;
};
