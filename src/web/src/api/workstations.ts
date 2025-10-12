import { apiClient } from './client';
import { NetworkAssignmentRequest, PagedResult } from './servers';

export interface Workstation {
  id: string;
  name: string;
  inventoryNumber: string;
  operatingSystemId: string;
  workstationTypeId: string;
  ownerId?: string | null;
  ownerDisplayName?: string | null;
  ownerDepartment?: string | null;
  locationId: string;
  locationNote?: string | null;
  cpu: string;
  ram: string;
  storage: string;
  macAddress: string;
  purchasedAt?: string | null;
  supportUntil?: string | null;
  primaryAdministratorId?: string | null;
  secondaryAdministratorId?: string | null;
  notes?: string | null;
  status: string;
  networkAssignments: NetworkAssignmentRequest[];
}

export interface WorkstationRequest {
  name: string;
  inventoryNumber: string;
  operatingSystemId: string;
  workstationTypeId: string;
  ownerId?: string | null;
  ownerDisplayName?: string | null;
  ownerDepartment?: string | null;
  locationId: string;
  locationNote?: string | null;
  cpu: string;
  ram: string;
  storage: string;
  macAddress: string;
  purchasedAt?: string | null;
  supportUntil?: string | null;
  primaryAdministratorId?: string | null;
  secondaryAdministratorId?: string | null;
  notes?: string | null;
  networkAssignments: NetworkAssignmentRequest[];
}

export const getWorkstations = async (page = 1, size = 25) => {
  const { data } = await apiClient.get<PagedResult<Workstation>>('/workstations', { params: { page, size } });
  return data;
};

export const createWorkstation = async (payload: WorkstationRequest) => {
  const { data } = await apiClient.post<Workstation>('/workstations', payload);
  return data;
};

export const updateWorkstation = async (id: string, payload: WorkstationRequest) => {
  const { data } = await apiClient.put<Workstation>(`/workstations/${id}`, payload);
  return data;
};

export const retireWorkstation = async (id: string) => {
  await apiClient.post(`/workstations/${id}/retire`, {});
};

export const restoreWorkstation = async (id: string) => {
  await apiClient.post(`/workstations/${id}/restore`, {});
};

export const deleteWorkstation = async (id: string) => {
  await apiClient.delete(`/workstations/${id}`);
};

export const handoverWorkstation = async (
  id: string,
  payload: {
    newOwnerId?: string | null;
    newOwnerEmail: string;
    oldLocation: string;
    newLocation: string;
    to?: string[];
    cc?: string[];
    bcc?: string[];
    notes?: string | null;
  }
) => {
  await apiClient.post(`/workstations/${id}/handover`, payload);
};
