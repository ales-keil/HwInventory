import { apiClient } from './client';

export interface ObservabilityConfiguration {
  logLevel: string;
  healthEndpointEnabled: boolean;
  metricsEndpointEnabled: boolean;
  correlationIdsEnabled: boolean;
  includeTraceIdentifier: boolean;
  otelExporterEnabled: boolean;
  otelEndpoint?: string | null;
  hasOtelAuthToken: boolean;
  resourceAttributes?: string | null;
}

export interface ObservabilityConfigurationUpdate {
  logLevel: string;
  healthEndpointEnabled: boolean;
  metricsEndpointEnabled: boolean;
  correlationIdsEnabled: boolean;
  includeTraceIdentifier: boolean;
  otelExporterEnabled: boolean;
  otelEndpoint?: string | null;
  otelAuthToken?: string | null;
  rotateOtelAuthToken: boolean;
  resourceAttributes?: string | null;
}

export const fetchObservabilityConfiguration = async () => {
  const { data } = await apiClient.get<ObservabilityConfiguration>('/observability/config');
  return data;
};

export const saveObservabilityConfiguration = async (payload: ObservabilityConfigurationUpdate) => {
  const { data } = await apiClient.put<ObservabilityConfiguration>('/observability/config', payload);
  return data;
};

export const fetchMetricsPreview = async () => {
  const response = await apiClient.get<string>('/observability/metrics/preview', {
    responseType: 'text'
  });
  return response.data;
};
