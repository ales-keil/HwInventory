import { apiClient } from './client';

export interface LabelTemplate {
  id: string;
  name: string;
  code: string;
  format: string;
  payload: string;
  description?: string | null;
}

export interface LabelPrintJob {
  id: string;
  target: string;
  format: string;
  jobStatus: string;
  createdAtUtc: string;
  createdBy: string;
}

export const getLabelTemplates = async () => {
  const { data } = await apiClient.get<LabelTemplate[]>('/labels/templates');
  return data;
};

export const createLabelTemplate = async (payload: LabelTemplate) => {
  const { data } = await apiClient.post<LabelTemplate>('/labels/templates', payload);
  return data;
};

export const updateLabelTemplate = async (id: string, payload: LabelTemplate) => {
  const { data } = await apiClient.put<LabelTemplate>(`/labels/templates/${id}`, payload);
  return data;
};

export const deleteLabelTemplate = async (id: string) => {
  await apiClient.delete(`/labels/templates/${id}`);
};

export const renderLabelPdf = async (id: string, data: Record<string, string>) => {
  const response = await apiClient.post(`/labels/templates/${id}/render/pdf`, { data }, { responseType: 'blob' });
  return response.data as Blob;
};

export const renderLabelZpl = async (id: string, data: Record<string, string>) => {
  const { data: response } = await apiClient.post<{ zpl: string }>(`/labels/templates/${id}/render/zpl`, { data });
  return response.zpl;
};

export const getLabelPrintJobs = async () => {
  const { data } = await apiClient.get<LabelPrintJob[]>('/labels/print-jobs');
  return data;
};
