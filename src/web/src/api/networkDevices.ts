import { apiClient } from './client';
import { PagedResult, NetworkAssignmentRequest } from './servers';

export interface NetworkDevice {
  id: string;
  name: string;
  inventoryNumber: string;
  deviceTypeId: string;
  manufacturer: string;
  model: string;
  locationId: string;
  rackPosition?: string | null;
  primaryAdministratorId?: string | null;
  secondaryAdministratorId?: string | null;
  supportUntil?: string | null;
  notes?: string | null;
  status: string;
  networkAssignments: NetworkAssignmentRequest[];
}

export interface NetworkDeviceRequest {
  name: string;
  inventoryNumber: string;
  deviceTypeId: string;
  manufacturer: string;
  model: string;
  locationId: string;
  rackPosition?: string | null;
  primaryAdministratorId?: string | null;
  secondaryAdministratorId?: string | null;
  supportUntil?: string | null;
  notes?: string | null;
  networkAssignments: NetworkAssignmentRequest[];
}

export interface NetworkDeviceQuery {
  search?: string;
  status?: string;
  locationId?: string;
  deviceTypeId?: string;
}

export const getNetworkDevices = async (page = 1, size = 25, filters: NetworkDeviceQuery = {}) => {
  const params: Record<string, string | number | undefined> = {
    page,
    size,
    search: filters.search?.trim() || undefined,
    status: filters.status || undefined,
    locationId: filters.locationId || undefined,
    deviceTypeId: filters.deviceTypeId || undefined
  };

  const { data } = await apiClient.get<PagedResult<NetworkDevice>>('/network-devices', { params });
  return data;
};

export const createNetworkDevice = async (payload: NetworkDeviceRequest) => {
  const { data } = await apiClient.post<NetworkDevice>('/network-devices', payload);
  return data;
};

export const updateNetworkDevice = async (id: string, payload: NetworkDeviceRequest) => {
  const { data } = await apiClient.put<NetworkDevice>(`/network-devices/${id}`, payload);
  return data;
};

export const retireNetworkDevice = async (id: string) => {
  await apiClient.post(`/network-devices/${id}/retire`, {});
};

export const retireNetworkDevices = async (ids: string[]) => {
  await apiClient.post('/network-devices/batch/retire', { ids });
};

export const restoreNetworkDevice = async (id: string) => {
  await apiClient.post(`/network-devices/${id}/restore`, {});
};

export const restoreNetworkDevices = async (ids: string[]) => {
  await apiClient.post('/network-devices/batch/restore', { ids });
};

export const deleteNetworkDevice = async (id: string) => {
  await apiClient.delete(`/network-devices/${id}`);
};

export const deleteNetworkDevices = async (ids: string[]) => {
  await apiClient.post('/network-devices/batch/delete', { ids });
};
