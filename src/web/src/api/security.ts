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

export interface CaptchaConfiguration {
  enabled: boolean;
  siteKey: string;
  hasSecret: boolean;
  verificationEndpoint: string;
  bypassToken?: string | null;
}

export interface CaptchaConfigurationRequest {
  enabled: boolean;
  siteKey: string;
  verificationEndpoint: string;
  secret?: string | null;
  rotateSecret: boolean;
  bypassToken?: string | null;
}

export interface SmsConnectorConfiguration {
  id?: string | null;
  alias: string;
  enabled: boolean;
  endpoint: string;
  sender?: string | null;
  region?: string | null;
  hasSecret: boolean;
  healthStatus?: string | null;
  lastTestedAtUtc?: string | null;
}

export interface SmsConnectorRequest {
  alias: string;
  enabled: boolean;
  endpoint: string;
  sender?: string | null;
  region?: string | null;
  secret?: string | null;
  rotateSecret: boolean;
}

export interface LdapRoleMapping {
  id: string;
  groupName: string;
  roleName: string;
}

export interface PasswordPolicyConfiguration {
  enabled: boolean;
  minimumLength: number;
  requireUppercase: boolean;
  requireLowercase: boolean;
  requireDigit: boolean;
  requireNonAlphanumeric: boolean;
  expirationDays: number;
  historyCount: number;
  lockoutAttempts: number;
  lockoutDurationMinutes: number;
}

export interface UserSummary {
  id: string;
  displayName: string;
  email: string;
  userName: string;
}

export interface UserSession {
  id: string;
  sessionIdentifier: string;
  issuedAtUtc: string;
  lastSeenAtUtc: string;
  ipAddress?: string | null;
  userAgent?: string | null;
  isRevoked: boolean;
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

export const getCaptchaConfiguration = async () => {
  const { data } = await apiClient.get<CaptchaConfiguration>('/security/captcha/config');
  return data;
};

export const updateCaptchaConfiguration = async (payload: CaptchaConfigurationRequest) => {
  await apiClient.post('/security/captcha/config', payload);
};

export const getSmsConfiguration = async () => {
  const { data } = await apiClient.get<SmsConnectorConfiguration | null>('/security/sms/config');
  return data;
};

export const updateSmsConfiguration = async (payload: SmsConnectorRequest) => {
  const { data } = await apiClient.post<SmsConnectorConfiguration>('/security/sms/config', payload);
  return data;
};

export const getPasswordPolicyConfiguration = async () => {
  const { data } = await apiClient.get<PasswordPolicyConfiguration>('/security/password-policy');
  return data;
};

export const updatePasswordPolicyConfiguration = async (payload: PasswordPolicyConfiguration) => {
  await apiClient.post('/security/password-policy', payload);
};

export const getLdapRoleMappings = async () => {
  const { data } = await apiClient.get<LdapRoleMapping[]>('/security/ldap/role-mappings');
  return data;
};

export const createLdapRoleMapping = async (payload: { groupName: string; roleName: string }) => {
  const { data } = await apiClient.post<LdapRoleMapping>('/security/ldap/role-mappings', payload);
  return data;
};

export const updateLdapRoleMapping = async (id: string, payload: { groupName: string; roleName: string }) => {
  const { data } = await apiClient.put<LdapRoleMapping>(`/security/ldap/role-mappings/${id}`, payload);
  return data;
};

export const deleteLdapRoleMapping = async (id: string) => {
  await apiClient.delete(`/security/ldap/role-mappings/${id}`);
};

export const searchSecurityUsers = async (query: string) => {
  const params = query.trim().length > 0 ? { params: { query } } : {};
  const { data } = await apiClient.get<UserSummary[]>('/security/users', params);
  return data;
};

export const getUserSessions = async (userId: string) => {
  const { data } = await apiClient.get<UserSession[]>(`/security/users/${userId}/sessions`);
  return data;
};

export const revokeSession = async (sessionId: string, reason?: string) => {
  await apiClient.post(`/security/sessions/${sessionId}/revoke`, reason ? { reason } : {});
};

export const revokeAllSessions = async (userId: string) => {
  await apiClient.post(`/security/users/${userId}/sessions/revoke-all`);
};
