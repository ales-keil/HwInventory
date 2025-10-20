import { apiClient } from './client';

export interface FeatureModule {
  id: string;
  key: string;
  name: string;
  description?: string | null;
  enabled: boolean;
  modifiedAtUtc?: string | null;
}

export const getFeatureModules = async () => {
  const { data } = await apiClient.get<FeatureModule[]>('/modules');
  return data;
};

export const toggleFeatureModule = async (key: string, enabled: boolean) => {
  const { data } = await apiClient.post<FeatureModule>(`/modules/${key}/toggle`, undefined, { params: { enabled } });
  return data;
};
