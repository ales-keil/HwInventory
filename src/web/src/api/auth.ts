import { apiClient } from './client';

export interface CurrentUser {
  id: string;
  userName: string;
  displayName: string;
  email: string;
  roles: string[];
  twoFactorEnabled: boolean;
  twoFactorRequired: boolean;
}

export interface LoginRequest {
  userNameOrEmail: string;
  password: string;
  rememberMe: boolean;
}

export async function fetchCurrentUser(): Promise<CurrentUser | null> {
  try {
    const response = await apiClient.get<CurrentUser>('/auth/me');
    return response.data;
  } catch (error) {
    if ((error as Error & { status?: number }).status === 401) {
      return null;
    }
    throw error;
  }
}

export async function login(request: LoginRequest): Promise<void> {
  await apiClient.post('/auth/login', request);
}

export async function logout(): Promise<void> {
  await apiClient.post('/auth/logout');
}
