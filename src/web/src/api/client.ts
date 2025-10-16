import axios, { AxiosError } from 'axios';

export const apiClient = axios.create({
  baseURL: '/api',
  withCredentials: true,
  headers: {
    'Content-Type': 'application/json'
  }
});

apiClient.interceptors.response.use(
  (response) => response,
  (error: AxiosError) => {
    const message = (error.response?.data as { detail?: string } | undefined)?.detail || error.message || 'Neznámá chyba';
    const enhancedError = new Error(message) as Error & { status?: number };
    enhancedError.status = error.response?.status;
    return Promise.reject(enhancedError);
  }
);
