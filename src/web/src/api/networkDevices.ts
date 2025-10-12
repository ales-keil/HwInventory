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

export const getNetworkDevices = async (page = 1, size = 25) => {
  const { data } = await apiClient.get<PagedResult<NetworkDevice>>('/network-devices', { params: { page, size } });
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

export const restoreNetworkDevice = async (id: string) => {
  await apiClient.post(`/network-devices/${id}/restore`, {});
};

export const deleteNetworkDevice = async (id: string) => {
  await apiClient.delete(`/network-devices/${id}`);
};
