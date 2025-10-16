import { createContext, useContext, useMemo, type ReactNode } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { CurrentUser, LoginRequest } from '../api/auth';
import { fetchCurrentUser, login as loginRequest, logout as logoutRequest } from '../api/auth';

interface SessionContextValue {
  user: CurrentUser | null;
  isLoading: boolean;
  error: Error | null;
  isLoggingIn: boolean;
  isLoggingOut: boolean;
  login: (request: LoginRequest) => Promise<void>;
  logout: () => Promise<void>;
  refresh: () => Promise<CurrentUser | null>;
}

const SessionContext = createContext<SessionContextValue | undefined>(undefined);

interface SessionProviderProps {
  children: ReactNode;
}

export const SessionProvider = ({ children }: SessionProviderProps) => {
  const queryClient = useQueryClient();
  const userQuery = useQuery({
    queryKey: ['current-user'],
    queryFn: fetchCurrentUser,
    staleTime: 30_000,
  });

  const loginMutation = useMutation({
    mutationFn: (request: LoginRequest) => loginRequest(request),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['current-user'] });
    },
  });

  const logoutMutation = useMutation({
    mutationFn: () => logoutRequest(),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: ['current-user'] });
    },
  });

  const value = useMemo<SessionContextValue>(() => ({
    user: userQuery.data ?? null,
    isLoading: userQuery.isLoading,
    error: (userQuery.error as Error) ?? null,
    isLoggingIn: loginMutation.isLoading,
    isLoggingOut: logoutMutation.isLoading,
    login: async (request: LoginRequest) => {
      await loginMutation.mutateAsync(request);
    },
    logout: async () => {
      await logoutMutation.mutateAsync();
    },
    refresh: async () => {
      const result = await userQuery.refetch();
      return result.data ?? null;
    },
  }), [
    userQuery.data,
    userQuery.isLoading,
    userQuery.error,
    userQuery.refetch,
    loginMutation.isLoading,
    loginMutation.mutateAsync,
    logoutMutation.isLoading,
    logoutMutation.mutateAsync,
  ]);

  return <SessionContext.Provider value={value}>{children}</SessionContext.Provider>;
};

export const useSession = (): SessionContextValue => {
  const context = useContext(SessionContext);
  if (!context) {
    throw new Error('useSession must be used within a SessionProvider');
  }
  return context;
};
