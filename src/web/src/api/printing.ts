import { apiClient } from './client';

export interface PrintingConnectorModel {
  id: string;
  alias: string;
  enabled: boolean;
  host: string;
  port: number;
  queueType: string;
  timeoutSeconds?: number | null;
  retryCount?: number | null;
  hasSecret: boolean;
  healthStatus?: string | null;
  lastTestedAtUtc?: string | null;
}

export interface PrintingConnectorRequest {
  alias: string;
  enabled: boolean;
  host: string;
  port: number;
  queueType: string;
  timeoutSeconds?: number | null;
  retryCount?: number | null;
  rotateSecret: boolean;
  sharedSecret?: string | null;
}

export interface PrintingConnectorTestResponse {
  success: boolean;
  message: string;
}

export const getPrintingConnector = async () => {
  const response = await apiClient.get<PrintingConnectorModel>('/api/settings/printing');
  return response.data;
};

export const savePrintingConnector = async (payload: PrintingConnectorRequest) => {
  const response = await apiClient.post<PrintingConnectorModel>('/api/settings/printing', payload);
  return response.data;
};

export const testPrintingConnector = async () => {
  const response = await apiClient.post<PrintingConnectorTestResponse>('/api/settings/printing/test');
  return response.data;
};
