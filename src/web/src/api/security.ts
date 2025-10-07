import { apiClient } from './client';

export interface OidcConfiguration {
  enabled: boolean;
  authority: string;
  clientId: string;
  clientSecret?: string | null;
  responseType?: string | null;
  scopes: string[];
  claimMappings: Record<string, string>;
  usePkce: boolean;
}

export interface LdapConfiguration {
  enabled: boolean;
  host: string;
  port: number;
  useSsl: boolean;
  bindDn: string;
  hasPassword: boolean;
  ignoreCertificateErrors: boolean;
  usersBaseDn: string;
  usersFilter?: string | null;
  attributeMap: string[];
}

export interface LdapConfigurationRequest {
  enabled: boolean;
  host: string;
  port: number;
  useSsl: boolean;
  bindDn: string;
  password?: string | null;
  resetPassword: boolean;
  ignoreCertificateErrors: boolean;
  usersBaseDn: string;
  usersFilter?: string | null;
  attributeMap: string[];
}

export interface LdapConnectionOptions {
  host: string;
  port: number;
  useSsl: boolean;
  bindDn: string;
  password?: string | null;
  ignoreCertificateErrors: boolean;
}

export interface LdapDryRunRequest {
  connection: LdapConnectionOptions & { password?: string | null };
  usersBaseDn: string;
  usersFilter?: string | null;
  attributeMap: string[];
  resultLimit: number;
}

export interface LdapConnectionTestResult {
  success: boolean;
  message: string;
  diagnostics?: Record<string, string> | null;
}

export interface LdapDryRunResult {
  users: Array<{ distinguishedName: string; attributes: Record<string, string | null> }>;
  resultCount: number;
  truncated: boolean;
  notes?: string | null;
}

export interface SsprConfiguration {
  enabled: boolean;
  requireTwoFactor: boolean;
  requireCaptcha: boolean;
  requireSmsOtp: boolean;
  tokenExpiryMinutes: number;
  throttleWindowMinutes: number;
  maxRequestsPerWindow: number;
  smsConnectorKey?: string | null;
}

export const getOidcConfiguration = async () => {
  const { data } = await apiClient.get<OidcConfiguration>('/security/oidc');
  return data;
};

export const updateOidcConfiguration = async (payload: OidcConfiguration) => {
  await apiClient.post('/security/oidc', payload);
};

export const getLdapConfiguration = async () => {
  const { data } = await apiClient.get<LdapConfiguration>('/security/ldap/config');
  return data;
};

export const updateLdapConfiguration = async (payload: LdapConfigurationRequest) => {
  await apiClient.post('/security/ldap/config', payload);
};

export const testLdapConnection = async (options: LdapConnectionOptions & { password?: string | null }) => {
  const { data } = await apiClient.post<LdapConnectionTestResult>('/security/ldap/test', options);
  return data;
};

export const dryRunLdap = async (request: LdapDryRunRequest) => {
  const { data } = await apiClient.post<LdapDryRunResult>('/security/ldap/dry-run', request);
  return data;
};

export const getSsprConfiguration = async () => {
  const { data } = await apiClient.get<SsprConfiguration>('/security/sspr/config');
  return data;
};

export const updateSsprConfiguration = async (payload: SsprConfiguration) => {
  await apiClient.post('/security/sspr/config', payload);
};
