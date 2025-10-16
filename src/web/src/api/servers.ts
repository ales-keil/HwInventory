import { apiClient } from './client';

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface NetworkAssignmentRequest {
  label: string;
  vlanId?: string | null;
  ipAddress?: string | null;
}

export interface Server {
  id: string;
  name: string;
  inventoryNumber: string;
  manufacturer: string;
  model: string;
  environmentId: string;
  wsusPriorityId: string;
  operatingSystemId: string;
  serverRoleId: string;
  primaryAdministratorId?: string | null;
  secondaryAdministratorId?: string | null;
  locationId: string;
  rackPosition?: string | null;
  purchasedAt?: string | null;
  supportUntil?: string | null;
  notes?: string | null;
  status: string;
  networkAssignments: NetworkAssignmentRequest[];
}

export interface ServerRequest {
  name: string;
  inventoryNumber: string;
  manufacturer: string;
  model: string;
  environmentId: string;
  wsusPriorityId: string;
  operatingSystemId: string;
  serverRoleId: string;
  primaryAdministratorId?: string | null;
  secondaryAdministratorId?: string | null;
  locationId: string;
  rackPosition?: string | null;
  purchasedAt?: string | null;
  supportUntil?: string | null;
  notes?: string | null;
  networkAssignments: NetworkAssignmentRequest[];
}

export interface ServerQuery {
  search?: string;
  status?: string;
  locationId?: string;
  environmentId?: string;
}

export const getServers = async (page = 1, size = 25, filters: ServerQuery = {}) => {
  const params: Record<string, string | number | undefined> = {
    page,
    size,
    search: filters.search?.trim() || undefined,
    status: filters.status || undefined,
    locationId: filters.locationId || undefined,
    environmentId: filters.environmentId || undefined
  };

  const { data } = await apiClient.get<PagedResult<Server>>('/servers', { params });
  return data;
};

export const createServer = async (payload: ServerRequest) => {
  const { data } = await apiClient.post<Server>('/servers', payload);
  return data;
};

export const updateServer = async (id: string, payload: ServerRequest) => {
  const { data } = await apiClient.put<Server>(`/servers/${id}`, payload);
  return data;
};

export const retireServer = async (id: string) => {
  await apiClient.post(`/servers/${id}/retire`, {});
};

export const retireServers = async (ids: string[]) => {
  await apiClient.post('/servers/batch/retire', { ids });
};

export const restoreServer = async (id: string) => {
  await apiClient.post(`/servers/${id}/restore`, {});
};

export const restoreServers = async (ids: string[]) => {
  await apiClient.post('/servers/batch/restore', { ids });
};

export const deleteServer = async (id: string) => {
  await apiClient.delete(`/servers/${id}`);
};

export const deleteServers = async (ids: string[]) => {
  await apiClient.post('/servers/batch/delete', { ids });
};
