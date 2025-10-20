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

export interface WorkstationQuery {
  search?: string;
  status?: string;
  locationId?: string;
  operatingSystemId?: string;
  workstationTypeId?: string;
}

export const getWorkstations = async (page = 1, size = 25, filters: WorkstationQuery = {}) => {
  const params: Record<string, string | number | undefined> = {
    page,
    size,
    search: filters.search?.trim() || undefined,
    status: filters.status || undefined,
    locationId: filters.locationId || undefined,
    operatingSystemId: filters.operatingSystemId || undefined,
    workstationTypeId: filters.workstationTypeId || undefined
  };

  const { data } = await apiClient.get<PagedResult<Workstation>>('/workstations', { params });
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

export const retireWorkstations = async (ids: string[]) => {
  await apiClient.post('/workstations/batch/retire', { ids });
};

export const restoreWorkstation = async (id: string) => {
  await apiClient.post(`/workstations/${id}/restore`, {});
};

export const restoreWorkstations = async (ids: string[]) => {
  await apiClient.post('/workstations/batch/restore', { ids });
};

export const deleteWorkstation = async (id: string) => {
  await apiClient.delete(`/workstations/${id}`);
};

export const deleteWorkstations = async (ids: string[]) => {
  await apiClient.post('/workstations/batch/delete', { ids });
};

export interface WorkstationHandoverRequest {
  newOwnerId?: string | null;
  newOwnerDisplayName?: string | null;
  newOwnerDepartment?: string | null;
  newLocationId: string;
  newLocationNote?: string | null;
  newAdminId?: string | null;
  to?: string[];
  cc?: string[];
  bcc?: string[];
  subject?: string | null;
  messageBody?: string | null;
  comment?: string | null;
  force?: boolean;
}

export interface WorkstationHandoverResponse {
  emailSent: boolean;
  message?: string | null;
  pendingApproval: boolean;
  handoverId?: string;
  acceptUrl?: string;
  declineUrl?: string;
  forced?: boolean;
}

export const handoverWorkstation = async (id: string, payload: WorkstationHandoverRequest) => {
  const { data } = await apiClient.post<WorkstationHandoverResponse>(`/workstations/${id}/handover`, payload);
  return data;
};
