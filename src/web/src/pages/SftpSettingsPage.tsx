import { FormEvent, useEffect, useState } from 'react';
import {
  SftpConnectorModel,
  SftpConnectorUpdate,
  getSftpConnector,
  testSftpConnector,
  updateSftpConnector
} from '../api/sftp';

const protocolOptions = [
  { value: 'SFTP', label: 'SFTP (SSH)' },
  { value: 'FTPS', label: 'FTPS (TLS)' }
];

type FormState = {
  alias: string;
  enabled: boolean;
  protocol: string;
  host: string;
  port: string;
  remotePath: string;
  username: string;
  useKeyAuthentication: boolean;
  passiveMode: boolean;
  useImplicitFtps: boolean;
  allowUnknownHosts: boolean;
  rotateSecrets: boolean;
  password: string;
  privateKey: string;
  knownHostsFingerprint: string;
};

const defaultState: FormState = {
  alias: 'SFTP/FTPS',
  enabled: false,
  protocol: 'SFTP',
  host: '',
  port: '22',
  remotePath: '/',
  username: '',
  useKeyAuthentication: false,
  passiveMode: false,
  useImplicitFtps: false,
  allowUnknownHosts: false,
  rotateSecrets: false,
  password: '',
  privateKey: '',
  knownHostsFingerprint: ''
};

export const SftpSettingsPage = () => {
  const [state, setState] = useState<FormState>(defaultState);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [current, setCurrent] = useState<SftpConnectorModel | null>(null);
  const [testMessage, setTestMessage] = useState<string | null>(null);

  useEffect(() => {
    let mounted = true;
    setLoading(true);
    getSftpConnector()
      .then((connector) => {
        if (!mounted) {
          return;
        }

        if (!connector) {
          setCurrent(null);
          setState(defaultState);
          return;
        }

        setCurrent(connector);
        setState({
          alias: connector.alias ?? 'SFTP/FTPS',
          enabled: connector.enabled,
          protocol: connector.protocol ?? 'SFTP',
          host: connector.host ?? '',
          port: connector.port?.toString() ?? (connector.protocol === 'FTPS' ? '21' : '22'),
          remotePath: connector.remotePath ?? '/',
          username: connector.username ?? '',
          useKeyAuthentication: connector.useKeyAuthentication,
          passiveMode: connector.passiveMode,
          useImplicitFtps: connector.useImplicitFtps ?? false,
          allowUnknownHosts: connector.allowUnknownHosts,
          rotateSecrets: false,
          password: '',
          privateKey: '',
          knownHostsFingerprint: ''
        });
      })
      .catch((err: Error) => {
        if (mounted) {
          setError(err.message);
        }
      })
      .finally(() => {
        if (mounted) {
          setLoading(false);
        }
      });

    return () => {
      mounted = false;
    };
  }, []);

  const sanitize = (): SftpConnectorUpdate => {
    const portValue = Number(state.port || (state.protocol === 'FTPS' ? 21 : 22));
    return {
      alias: state.alias.trim() || 'SFTP/FTPS',
      enabled: state.enabled,
      protocol: state.protocol,
      host: state.host.trim(),
      port: Number.isNaN(portValue) ? (state.protocol === 'FTPS' ? 21 : 22) : portValue,
      remotePath: state.remotePath.trim() || '/',
      username: state.username.trim() || undefined,
      useKeyAuthentication: state.useKeyAuthentication,
      passiveMode: state.passiveMode,
      useImplicitFtps: state.protocol === 'FTPS' ? state.useImplicitFtps : undefined,
      allowUnknownHosts: state.allowUnknownHosts,
      rotateSecrets: state.rotateSecrets,
      password: state.password || undefined,
      privateKey: state.privateKey || undefined,
      knownHostsFingerprint: state.knownHostsFingerprint || undefined
    };
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setSaving(true);
    setTestMessage(null);

    try {
      const payload = sanitize();
      const saved = await updateSftpConnector(payload);
      setCurrent(saved);
      setState((prev) => ({ ...prev, rotateSecrets: false, password: '', privateKey: '', knownHostsFingerprint: '' }));
      setTestMessage('Konfigurace byla uložena.');
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Uložení se nezdařilo.';
      setError(message);
    } finally {
      setSaving(false);
    }
  };

  const handleTest = async () => {
    setTesting(true);
    setError(null);
    try {
      const result = await testSftpConnector();
      setTestMessage(result.message);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Test se nezdařil.';
      setError(message);
    } finally {
      setTesting(false);
    }
  };

  const disabled = loading || saving;
  const showImplicitOption = state.protocol === 'FTPS';

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold text-slate-900 dark:text-slate-100">SFTP / FTPS konektor</h2>
        <p className="text-sm text-slate-600 dark:text-slate-400">
          Umožňuje přenos souborů přes zabezpečený protokol SFTP nebo FTPS. Konfigurace je sdílena s importy, exporty a dalším
          zpracováním souborů.
        </p>
      </div>

      {error && <div className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-700">{error}</div>}
      {testMessage && <div className="rounded border border-emerald-300 bg-emerald-50 p-3 text-sm text-emerald-700">{testMessage}</div>}

      <form onSubmit={handleSubmit} className="space-y-6">
        <fieldset disabled={disabled} className="space-y-4 rounded border border-slate-200 p-4 shadow-sm dark:border-slate-700">
          <legend className="px-1 text-sm font-medium uppercase tracking-wide text-slate-500 dark:text-slate-400">Základní údaje</legend>

          <div className="grid gap-4 sm:grid-cols-2">
            <label className="flex flex-col gap-1 text-sm">
              Alias
              <input
                type="text"
                value={state.alias}
                onChange={(event) => setState((prev) => ({ ...prev, alias: event.target.value }))}
                className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-900"
              />
            </label>

            <label className="flex flex-col gap-1 text-sm">
              Protokol
              <select
                value={state.protocol}
                onChange={(event) =>
                  setState((prev) => ({
                    ...prev,
                    protocol: event.target.value,
                    port: event.target.value === 'FTPS' ? '21' : '22'
                  }))
                className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-900"
              >
                {protocolOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>

            <label className="flex flex-col gap-1 text-sm">
              Host
              <input
                type="text"
                value={state.host}
                onChange={(event) => setState((prev) => ({ ...prev, host: event.target.value }))}
                required
                className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-900"
              />
            </label>

            <label className="flex flex-col gap-1 text-sm">
              Port
              <input
                type="number"
                value={state.port}
                onChange={(event) => setState((prev) => ({ ...prev, port: event.target.value }))}
                className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-900"
              />
            </label>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <label className="flex flex-col gap-1 text-sm">
              Uživatelské jméno
              <input
                type="text"
                value={state.username}
                onChange={(event) => setState((prev) => ({ ...prev, username: event.target.value }))}
                className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-900"
              />
            </label>

            <label className="flex flex-col gap-1 text-sm">
              Výchozí vzdálená cesta
              <input
                type="text"
                value={state.remotePath}
                onChange={(event) => setState((prev) => ({ ...prev, remotePath: event.target.value }))}
                className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-900"
              />
            </label>
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={state.enabled}
                onChange={(event) => setState((prev) => ({ ...prev, enabled: event.target.checked }))}
              />
              Konektor je aktivní
            </label>

            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={state.useKeyAuthentication}
                onChange={(event) => setState((prev) => ({ ...prev, useKeyAuthentication: event.target.checked }))}
              />
              Použít SSH klíč
            </label>

            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={state.allowUnknownHosts}
                onChange={(event) => setState((prev) => ({ ...prev, allowUnknownHosts: event.target.checked }))}
              />
              Povolit neznámé hostitele
            </label>

            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={state.passiveMode}
                onChange={(event) => setState((prev) => ({ ...prev, passiveMode: event.target.checked }))}
              />
              Passive mode (FTPS)
            </label>

            {showImplicitOption && (
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={state.useImplicitFtps}
                  onChange={(event) => setState((prev) => ({ ...prev, useImplicitFtps: event.target.checked }))}
                />
                FTPS implicitní režim
              </label>
            )}
          </div>
        </fieldset>

        <fieldset disabled={disabled} className="space-y-4 rounded border border-slate-200 p-4 shadow-sm dark:border-slate-700">
          <legend className="px-1 text-sm font-medium uppercase tracking-wide text-slate-500 dark:text-slate-400">Přístupové údaje</legend>

          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={state.rotateSecrets}
              onChange={(event) => setState((prev) => ({ ...prev, rotateSecrets: event.target.checked }))}
            />
            Resetovat tajemství při uložení
          </label>

          <div className="grid gap-4 sm:grid-cols-2">
            <label className="flex flex-col gap-1 text-sm">
              Heslo
              <input
                type="password"
                value={state.password}
                placeholder={current?.hasPassword ? '••••••••' : ''}
                onChange={(event) => setState((prev) => ({ ...prev, password: event.target.value }))}
                className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-900"
              />
            </label>

            <label className="flex flex-col gap-1 text-sm">
              Soukromý klíč (PEM)
              <textarea
                value={state.privateKey}
                placeholder={current?.hasPrivateKey ? '••••••••' : ''}
                onChange={(event) => setState((prev) => ({ ...prev, privateKey: event.target.value }))}
                rows={4}
                className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-900"
              />
            </label>
          </div>

          <label className="flex flex-col gap-1 text-sm">
            Fingerprint known_hosts
            <input
              type="text"
              value={state.knownHostsFingerprint}
              onChange={(event) => setState((prev) => ({ ...prev, knownHostsFingerprint: event.target.value }))}
              placeholder={current?.allowUnknownHosts ? 'volitelné' : 'SHA256...'}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-900"
            />
          </label>
        </fieldset>

        <div className="flex flex-wrap items-center gap-3">
          <button
            type="submit"
            disabled={saving}
            className="rounded bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow hover:bg-slate-700 dark:bg-slate-700 dark:hover:bg-slate-600 disabled:opacity-60"
          >
            {saving ? 'Ukládám…' : 'Uložit změny'}
          </button>
          <button
            type="button"
            onClick={handleTest}
            disabled={testing || loading}
            className="rounded border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-600 dark:text-slate-200 dark:hover:bg-slate-800 disabled:opacity-60"
          >
            {testing ? 'Testuji…' : 'Spustit test'}
          </button>
          <span className="text-xs text-slate-500 dark:text-slate-400">
            Poslední test: {current?.lastTestedAtUtc ? new Date(current.lastTestedAtUtc).toLocaleString() : 'zatím neproběhl'}
          </span>
          {current?.healthStatus && (
            <span className="text-xs font-medium text-slate-600 dark:text-slate-300">Status: {current.healthStatus}</span>
          )}
        </div>
      </form>
    </div>
  );
};
