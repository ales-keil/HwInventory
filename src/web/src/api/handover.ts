import { apiClient } from './client';

export interface HandoverConfiguration {
  defaultTo: string[];
  defaultCc: string[];
  defaultBcc: string[];
  defaultSubject?: string | null;
  defaultBody?: string | null;
  pdfLogoBase64?: string | null;
  useMinimalPdf: boolean;
  pdfFooterNote?: string | null;
}

export type HandoverConfigurationUpdate = {
  defaultTo?: string[];
  defaultCc?: string[];
  defaultBcc?: string[];
  defaultSubject?: string | null;
  defaultBody?: string | null;
  pdfLogoBase64?: string | null;
  useMinimalPdf: boolean;
  pdfFooterNote?: string | null;
};

export const fetchHandoverConfiguration = async () => {
  const { data } = await apiClient.get<HandoverConfiguration>('/settings/handover');
  return data;
};

export const saveHandoverConfiguration = async (payload: HandoverConfigurationUpdate) => {
  const { data } = await apiClient.put<HandoverConfiguration>('/settings/handover', payload);
  return data;
};
