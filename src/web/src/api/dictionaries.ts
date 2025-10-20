import { apiClient } from './client';

export interface DictionaryEntry {
  id: string;
  dictType: string;
  key: string;
  value: string;
  description?: string | null;
  order: number;
}

export interface DictionaryEntryRequest {
  dictType: string;
  key: string;
  value: string;
  description?: string | null;
  order: number;
}

export const getDictionaryEntries = async (type?: string) => {
  const params = type ? { type } : undefined;
  const { data } = await apiClient.get<DictionaryEntry[]>('/dictionaries', { params });
  return data;
};

export const createDictionaryEntry = async (payload: DictionaryEntryRequest) => {
  const { data } = await apiClient.post<DictionaryEntry>('/dictionaries', payload);
  return data;
};

export const updateDictionaryEntry = async (id: string, payload: DictionaryEntryRequest) => {
  const { data } = await apiClient.put<DictionaryEntry>(`/dictionaries/${id}`, payload);
  return data;
};

export const deleteDictionaryEntry = async (id: string) => {
  await apiClient.delete(`/dictionaries/${id}`);
};
