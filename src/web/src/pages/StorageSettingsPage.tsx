import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  StorageConnectorModel,
  StorageConnectorTestResult,
  StorageConnectorUpdate,
  fetchStorageConnector,
  saveStorageConnector,
  testStorageConnector
} from '../api/storage';

const typeOptions = [
  { value: 'LOCAL', label: 'Lokální disk' },
  { value: 'SMB', label: 'SMB / UNC sdílení' },
  { value: 'S3', label: 'S3 kompatibilní úložiště' }
];

type StorageFormState = {
  alias: string;
  enabled: boolean;
  type: string;
  path: string;
  endpoint: string;
  bucket: string;
  folder: string;
  region: string;
  username: string;
  domain: string;
  publicUrlBase: string;
  retentionDays: string;
  useSsl: boolean;
  rotateSecret: boolean;
  password: string;
  accessKey: string;
  secretKey: string;
};

const defaultState: StorageFormState = {
  alias: 'Artefact Storage',
  enabled: true,
  type: 'LOCAL',
  path: '',
  endpoint: '',
  bucket: '',
  folder: '',
  region: '',
  username: '',
  domain: '',
  publicUrlBase: '',
  retentionDays: '30',
  useSsl: false,
  rotateSecret: false,
  password: '',
  accessKey: '',
  secretKey: ''
};

export const StorageSettingsPage = () => {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [state, setState] = useState<StorageFormState>(defaultState);
  const [current, setCurrent] = useState<StorageConnectorModel | null>(null);
  const [testResult, setTestResult] = useState<StorageConnectorTestResult | null>(null);

  useEffect(() => {
    let isMounted = true;
    setLoading(true);
    fetchStorageConnector()
      .then((connector) => {
        if (!isMounted || !connector) {
          setCurrent(connector ?? null);
          return;
        }

        setCurrent(connector);
        setState({
          alias: connector.alias ?? 'Artefact Storage',
          enabled: connector.enabled,
          type: connector.type?.toUpperCase() ?? 'LOCAL',
          path: connector.path ?? '',
          endpoint: connector.endpoint ?? '',
          bucket: connector.bucket ?? '',
          folder: connector.folder ?? '',
          region: connector.region ?? '',
          username: connector.username ?? '',
          domain: connector.domain ?? '',
          publicUrlBase: connector.publicUrlBase ?? '',
          retentionDays: connector.retentionDays?.toString() ?? '',
          useSsl: connector.useSsl ?? false,
          rotateSecret: false,
          password: '',
          accessKey: '',
          secretKey: ''
        });
      })
      .catch((err: Error) => {
        if (!isMounted) {
          return;
        }
        setError(err.message);
      })
      .finally(() => {
        if (isMounted) {
          setLoading(false);
        }
      });

    return () => {
      isMounted = false;
    };
  }, []);

  const sanitize = (): StorageConnectorUpdate => {
    const retention = state.retentionDays.trim();
    const retentionNumber = retention ? Number(retention) : undefined;

    const payload: StorageConnectorUpdate = {
      alias: state.alias.trim() || 'Artefact Storage',
      enabled: state.enabled,
      type: state.type.toUpperCase(),
      path: state.path.trim() || null,
      endpoint: state.endpoint.trim() || null,
      bucket: state.bucket.trim() || null,
      folder: state.folder.trim() || null,
      region: state.region.trim() || null,
      username: state.username.trim() || null,
      domain: state.domain.trim() || null,
      publicUrlBase: state.publicUrlBase.trim() || null,
      retentionDays: Number.isFinite(retentionNumber) ? (retentionNumber as number) : null,
      useSsl: state.useSsl,
      rotateSecret: state.rotateSecret || Boolean(state.password || state.accessKey || state.secretKey),
      password: state.password.trim() || null,
      accessKey: state.accessKey.trim() || null,
      secretKey: state.secretKey.trim() || null
    };

    if (payload.type === 'LOCAL') {
      payload.endpoint = null;
      payload.bucket = null;
      payload.folder = payload.folder;
      payload.region = null;
      payload.username = null;
      payload.domain = null;
      payload.accessKey = null;
      payload.secretKey = null;
      payload.useSsl = null;
    }

    if (payload.type === 'SMB') {
      payload.bucket = null;
      payload.folder = payload.folder;
      payload.region = null;
      payload.accessKey = null;
      payload.secretKey = null;
    }

    if (payload.type === 'S3') {
      payload.path = null;
      payload.username = null;
      payload.domain = null;
      payload.password = null;
    }

    return payload;
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setError(null);
    setTestResult(null);

    try {
      const payload = sanitize();
      const result = await saveStorageConnector(payload);
      setCurrent(result);
      setState((previous) => ({
        ...previous,
        rotateSecret: false,
        password: '',
        accessKey: '',
        secretKey: ''
      }));
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const handleTest = async () => {
    setTesting(true);
    setError(null);
    setTestResult(null);
    try {
      const result = await testStorageConnector();
      setTestResult(result);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setTesting(false);
    }
  };

  const passwordHelp = useMemo(() => {
    if (state.type === 'LOCAL') {
      return 'Lokální disk nevyžaduje přihlašovací údaje.';
    }

    if (state.type === 'SMB') {
      if (state.rotateSecret || state.password) {
        return 'Zadáte nové heslo – bude uloženo po uložení formuláře.';
      }
      return current?.hasPassword ? 'Heslo je nastaveno. Pokud jej chcete změnit, zadejte nové nebo zvolte „Vymazat“.' : 'Zadejte heslo pro přístup k UNC sdílení.';
    }

    if (state.type === 'S3') {
      if (state.rotateSecret || state.accessKey || state.secretKey) {
        return 'Po uložení se uloží nové access/secret klíče.';
      }
      return current?.hasAccessKeys ? 'Klíče jsou nastaveny. Zadejte nové hodnoty pro aktualizaci.' : 'Zadejte access a secret key pro přístup k úložišti.';
    }

    return '';
  }, [current?.hasAccessKeys, current?.hasPassword, state.accessKey, state.password, state.rotateSecret, state.secretKey, state.type]);

  if (loading) {
    return <div className="text-sm text-slate-600 dark:text-slate-300">Načítám nastavení úložiště…</div>;
  }

  return (
    <div className="space-y-8">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">Úložiště artefaktů</h1>
        <p className="max-w-3xl text-sm text-slate-600 dark:text-slate-300">
          Nakonfigurujte úložiště pro exporty, generované PDF/ZPL a další artefakty. Konfigurace je sdílena mezi import/export,
          tiskovou frontou i zálohováním a všechny změny jsou auditovány.
        </p>
        {current && (
          <p className="text-xs text-slate-500 dark:text-slate-400">
            Stav: {current.healthStatus ?? 'N/A'} • Poslední test: {current.lastTestedAtUtc ? new Date(current.lastTestedAtUtc).toLocaleString() : 'nikdy'}
          </p>
        )}
      </header>

      {error && (
        <div className="rounded border border-rose-200 bg-rose-50 p-4 text-sm text-rose-800 dark:border-rose-900 dark:bg-rose-950 dark:text-rose-200">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-6">
        <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Základní nastavení</h2>
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            <label className="flex flex-col text-sm">
              Alias
              <input
                type="text"
                value={state.alias}
                onChange={(event) => setState((prev) => ({ ...prev, alias: event.target.value }))}
                className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </label>
            <label className="flex flex-col text-sm">
              Typ úložiště
              <select
                value={state.type}
                onChange={(event) =>
                  setState((prev) => ({
                    ...prev,
                    type: event.target.value,
                    rotateSecret: false,
                    password: '',
                    accessKey: '',
                    secretKey: ''
                  }))
                }
                className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              >
                {typeOptions.map((option) => (
                  <option key={option.value} value={option.value}>
                    {option.label}
                  </option>
                ))}
              </select>
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={state.enabled}
                onChange={(event) => setState((prev) => ({ ...prev, enabled: event.target.checked }))}
              />
              Konektor aktivní
            </label>
            <label className="flex flex-col text-sm">
              Retence (dny)
              <input
                type="number"
                min={0}
                value={state.retentionDays}
                onChange={(event) => setState((prev) => ({ ...prev, retentionDays: event.target.value }))}
                className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </label>
          </div>
        </section>

        {state.type === 'LOCAL' && (
          <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Lokální disk</h2>
            <div className="mt-4 grid gap-4 md:grid-cols-2">
              <label className="flex flex-col text-sm">
                Cesta k adresáři
                <input
                  type="text"
                  required
                  value={state.path}
                  onChange={(event) => setState((prev) => ({ ...prev, path: event.target.value }))}
                  placeholder="C:\\HWInventory\\artefacts"
                  className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                />
              </label>
              <label className="flex flex-col text-sm">
                Veřejná URL (volitelné)
                <input
                  type="text"
                  value={state.publicUrlBase}
                  onChange={(event) => setState((prev) => ({ ...prev, publicUrlBase: event.target.value }))}
                  placeholder="https://inventory.example.com/downloads"
                  className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                />
              </label>
              <label className="flex flex-col text-sm md:col-span-2">
                Podsložka pro artefakty (volitelné)
                <input
                  type="text"
                  value={state.folder}
                  onChange={(event) => setState((prev) => ({ ...prev, folder: event.target.value }))}
                  placeholder="např. exports"
                  className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                />
              </label>
            </div>
          </section>
        )}

        {state.type === 'SMB' && (
          <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">SMB / UNC připojení</h2>
            <div className="mt-4 grid gap-4 md:grid-cols-2">
              <label className="flex flex-col text-sm">
                UNC cesta (\\\\server\\sdílení)
                <input
                  type="text"
                  required
                  value={state.path}
                  onChange={(event) => setState((prev) => ({ ...prev, path: event.target.value }))}
                  placeholder="\\\\fileserver\\hw-inventory"
                  className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                />
              </label>
              <label className="flex flex-col text-sm">
                Přihlašovací jméno
                <input
                  type="text"
                  value={state.username}
                  onChange={(event) => setState((prev) => ({ ...prev, username: event.target.value }))}
                  className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                />
              </label>
              <label className="flex flex-col text-sm">
                Doména (volitelné)
                <input
                  type="text"
                  value={state.domain}
                  onChange={(event) => setState((prev) => ({ ...prev, domain: event.target.value }))}
                  className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                />
              </label>
              <label className="flex flex-col text-sm">
                Heslo
                <input
                  type="password"
                  value={state.password}
                  onChange={(event) => setState((prev) => ({ ...prev, password: event.target.value }))}
                  placeholder={current?.hasPassword ? '••••••••' : 'Zadejte heslo'}
                  className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                />
              </label>
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={state.rotateSecret}
                  onChange={(event) => setState((prev) => ({ ...prev, rotateSecret: event.target.checked }))}
                />
                Vymazat uložené heslo při uložení
              </label>
              <label className="flex flex-col text-sm md:col-span-2">
                Podsložka (volitelné)
                <input
                  type="text"
                  value={state.folder}
                  onChange={(event) => setState((prev) => ({ ...prev, folder: event.target.value }))}
                  placeholder="např. exports"
                  className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                />
              </label>
            </div>
          </section>
        )}

        {state.type === 'S3' && (
          <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">S3 kompatibilní úložiště</h2>
            <div className="mt-4 grid gap-4 md:grid-cols-2">
              <label className="flex flex-col text-sm">
                Endpoint URL
                <input
                  type="text"
                  required
                  value={state.endpoint}
                  onChange={(event) => setState((prev) => ({ ...prev, endpoint: event.target.value }))}
                  placeholder="https://s3.example.com"
                  className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                />
              </label>
              <label className="flex flex-col text-sm">
                Bucket
                <input
                  type="text"
                  required
                  value={state.bucket}
                  onChange={(event) => setState((prev) => ({ ...prev, bucket: event.target.value }))}
                  className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                />
              </label>
              <label className="flex flex-col text-sm">
                Prefix / složka (volitelné)
                <input
                  type="text"
                  value={state.folder}
                  onChange={(event) => setState((prev) => ({ ...prev, folder: event.target.value }))}
                  placeholder="např. exports/"
                  className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                />
              </label>
              <label className="flex flex-col text-sm">
                Region (volitelné)
                <input
                  type="text"
                  value={state.region}
                  onChange={(event) => setState((prev) => ({ ...prev, region: event.target.value }))}
                  placeholder="eu-central-1"
                  className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                />
              </label>
              <label className="flex flex-col text-sm">
                Access key
                <input
                  type="text"
                  value={state.accessKey}
                  onChange={(event) => setState((prev) => ({ ...prev, accessKey: event.target.value }))}
                  placeholder={current?.hasAccessKeys ? '••••••••' : 'Zadejte access key'}
                  className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                />
              </label>
              <label className="flex flex-col text-sm">
                Secret key
                <input
                  type="password"
                  value={state.secretKey}
                  onChange={(event) => setState((prev) => ({ ...prev, secretKey: event.target.value }))}
                  placeholder={current?.hasAccessKeys ? '••••••••' : 'Zadejte secret key'}
                  className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
                />
              </label>
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={state.useSsl}
                  onChange={(event) => setState((prev) => ({ ...prev, useSsl: event.target.checked }))}
                />
                Použít SSL/TLS
              </label>
              <label className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={state.rotateSecret}
                  onChange={(event) => setState((prev) => ({ ...prev, rotateSecret: event.target.checked }))}
                />
                Vymazat uložené klíče při uložení
              </label>
            </div>
          </section>
        )}

        <section className="rounded-lg border border-dashed border-slate-300 bg-slate-50 p-6 text-sm text-slate-600 dark:border-slate-700 dark:bg-slate-900/40 dark:text-slate-300">
          <p>{passwordHelp}</p>
          {current?.publicUrlBase && (
            <p className="mt-2">
              Veřejná URL základna: <span className="font-mono">{current.publicUrlBase}</span>
            </p>
          )}
        </section>

        <div className="flex flex-wrap gap-3">
          <button
            type="submit"
            disabled={saving}
            className="rounded bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-slate-700 dark:bg-slate-700 dark:hover:bg-slate-600 disabled:opacity-60"
          >
            {saving ? 'Ukládám…' : 'Uložit nastavení'}
          </button>
          <button
            type="button"
            onClick={handleTest}
            disabled={testing}
            className="rounded border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-200 dark:border-slate-600 dark:text-slate-200 dark:hover:bg-slate-700 disabled:opacity-60"
          >
            {testing ? 'Testuji…' : 'Otestovat konektor'}
          </button>
          {testResult && <span className="text-sm text-slate-600 dark:text-slate-300">{testResult.message}</span>}
        </div>
      </form>
    </div>
  );
};
