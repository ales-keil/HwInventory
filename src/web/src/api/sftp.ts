import { apiClient } from './client';

export interface SftpConnectorModel {
  id: string;
  alias: string;
  enabled: boolean;
  protocol: string;
  host: string;
  port: number;
  remotePath?: string | null;
  username?: string | null;
  useKeyAuthentication: boolean;
  passiveMode: boolean;
  useImplicitFtps?: boolean | null;
  allowUnknownHosts: boolean;
  hasPassword: boolean;
  hasPrivateKey: boolean;
  healthStatus?: string | null;
  lastTestedAtUtc?: string | null;
}

export interface SftpConnectorUpdate {
  alias: string;
  enabled: boolean;
  protocol: string;
  host: string;
  port: number;
  remotePath?: string | null;
  username?: string | null;
  useKeyAuthentication: boolean;
  passiveMode: boolean;
  useImplicitFtps?: boolean | null;
  allowUnknownHosts: boolean;
  rotateSecrets: boolean;
  password?: string | null;
  privateKey?: string | null;
  knownHostsFingerprint?: string | null;
}

export async function getSftpConnector() {
  const response = await apiClient.get<SftpConnectorModel | null>('/api/settings/sftp');
  return response.data;
}

export async function updateSftpConnector(payload: SftpConnectorUpdate) {
  const response = await apiClient.put<SftpConnectorModel>('/api/settings/sftp', payload);
  return response.data;
}

export async function testSftpConnector() {
  const response = await apiClient.post<{ success: boolean; message: string }>(
    '/api/settings/sftp/test',
    {}
  );
  return response.data;
}
