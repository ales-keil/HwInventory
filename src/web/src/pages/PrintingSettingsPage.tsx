import { FormEvent, useEffect, useState } from 'react';
import {
  PrintingConnectorModel,
  PrintingConnectorRequest,
  PrintingConnectorTestResponse,
  getPrintingConnector,
  savePrintingConnector,
  testPrintingConnector
} from '../api/printing';

const queueTypes = [
  { value: 'RAW', label: 'RAW 9100 socket' },
  { value: 'Queue', label: 'Fronta / spool (obecná)' }
];

type FormState = {
  alias: string;
  enabled: boolean;
  host: string;
  port: string;
  queueType: string;
  timeoutSeconds: string;
  retryCount: string;
  rotateSecret: boolean;
  sharedSecret: string;
};

const defaultState: FormState = {
  alias: 'Printing',
  enabled: false,
  host: '',
  port: '9100',
  queueType: 'RAW',
  timeoutSeconds: '30',
  retryCount: '0',
  rotateSecret: false,
  sharedSecret: ''
};

export const PrintingSettingsPage = () => {
  const [state, setState] = useState<FormState>(defaultState);
  const [current, setCurrent] = useState<PrintingConnectorModel | null>(null);
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [testMessage, setTestMessage] = useState<string | null>(null);

  useEffect(() => {
    let mounted = true;
    setLoading(true);
    getPrintingConnector()
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
          alias: connector.alias ?? 'Printing',
          enabled: connector.enabled,
          host: connector.host ?? '',
          port: connector.port?.toString() ?? '9100',
          queueType: connector.queueType ?? 'RAW',
          timeoutSeconds: connector.timeoutSeconds?.toString() ?? '30',
          retryCount: connector.retryCount?.toString() ?? '0',
          rotateSecret: false,
          sharedSecret: ''
        });
      })
      .catch((err: any) => {
        if (!mounted) {
          return;
        }

        if (err?.response?.status === 404) {
          setCurrent(null);
          setState(defaultState);
          return;
        }

        const message = err instanceof Error ? err.message : 'Načtení konfigurace se nezdařilo.';
        setError(message);
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

  const sanitize = (): PrintingConnectorRequest => {
    const parsedPort = Number(state.port || 9100);
    const parsedTimeout = state.timeoutSeconds ? Number(state.timeoutSeconds) : undefined;
    const parsedRetry = state.retryCount ? Number(state.retryCount) : undefined;

    const payload: PrintingConnectorRequest = {
      alias: state.alias.trim() || 'Printing',
      enabled: state.enabled,
      host: state.host.trim(),
      port: Number.isNaN(parsedPort) ? 9100 : parsedPort,
      queueType: state.queueType,
      timeoutSeconds: Number.isNaN(parsedTimeout ?? NaN) ? undefined : parsedTimeout,
      retryCount: Number.isNaN(parsedRetry ?? NaN) ? undefined : parsedRetry,
      rotateSecret: state.rotateSecret,
      sharedSecret: state.sharedSecret ? state.sharedSecret : undefined
    };

    return payload;
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setError(null);
    setTestMessage(null);

    try {
      const payload = sanitize();
      const saved = await savePrintingConnector(payload);
      setCurrent(saved);
      setState((prev) => ({ ...prev, rotateSecret: false, sharedSecret: '' }));
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
    setTestMessage(null);

    try {
      const response: PrintingConnectorTestResponse = await testPrintingConnector();
      setTestMessage(response.message);
    } catch (err) {
      const message = err instanceof Error ? err.message : 'Test se nezdařil.';
      setError(message);
    } finally {
      setTesting(false);
    }
  };

  const disabled = loading || saving;

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Tiskové konektory</h2>
        <p className="text-sm text-slate-600 dark:text-slate-400">
          Konfigurace RAW 9100 nebo frontového tisku. Systém uloží přihlašovací údaje šifrovaně a umožní otestovat základní nastavení.
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

            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={state.enabled}
                onChange={(event) => setState((prev) => ({ ...prev, enabled: event.target.checked }))}
              />
              Konektor je aktivní
            </label>
          </div>

          <div className="grid gap-4 sm:grid-cols-2">
            <label className="flex flex-col gap-1 text-sm">
              Host / IP
              <input
                type="text"
                required
                value={state.host}
                onChange={(event) => setState((prev) => ({ ...prev, host: event.target.value }))}
                className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-900"
              />
            </label>

            <label className="flex flex-col gap-1 text-sm">
              Port
              <input
                type="number"
                min={1}
                max={65535}
                value={state.port}
                onChange={(event) => setState((prev) => ({ ...prev, port: event.target.value }))}
                className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-900"
              />
            </label>
          </div>

          <div className="grid gap-4 sm:grid-cols-3">
            <label className="flex flex-col gap-1 text-sm sm:col-span-1">
              Typ fronty
              <select
                value={state.queueType}
                onChange={(event) => setState((prev) => ({ ...prev, queueType: event.target.value }))}
                className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-900"
              >
                {queueTypes.map((type) => (
                  <option key={type.value} value={type.value}>
                    {type.label}
                  </option>
                ))}
              </select>
            </label>

            <label className="flex flex-col gap-1 text-sm">
              Timeout (s)
              <input
                type="number"
                min={1}
                max={600}
                value={state.timeoutSeconds}
                onChange={(event) => setState((prev) => ({ ...prev, timeoutSeconds: event.target.value }))}
                className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-900"
              />
            </label>

            <label className="flex flex-col gap-1 text-sm">
              Počet pokusů
              <input
                type="number"
                min={0}
                max={10}
                value={state.retryCount}
                onChange={(event) => setState((prev) => ({ ...prev, retryCount: event.target.value }))}
                className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-900"
              />
            </label>
          </div>
        </fieldset>

        <fieldset disabled={disabled} className="space-y-4 rounded border border-slate-200 p-4 shadow-sm dark:border-slate-700">
          <legend className="px-1 text-sm font-medium uppercase tracking-wide text-slate-500 dark:text-slate-400">Bezpečnost</legend>

          <div className="grid gap-4 sm:grid-cols-2">
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={state.rotateSecret}
                onChange={(event) => setState((prev) => ({ ...prev, rotateSecret: event.target.checked }))}
              />
              Rotovat sdílené tajemství při uložení
            </label>

            <label className="flex flex-col gap-1 text-sm">
              Sdílené tajemství (volitelné)
              <input
                type="text"
                value={state.sharedSecret}
                onChange={(event) => setState((prev) => ({ ...prev, sharedSecret: event.target.value }))}
                className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-900"
              />
              <span className="text-xs text-slate-500 dark:text-slate-400">
                Uchovává se šifrovaně. Využijte například pro zabezpečení fronty nebo API.
              </span>
            </label>
          </div>

          {current?.hasSecret && !state.rotateSecret && (
            <p className="text-xs text-slate-500 dark:text-slate-400">Tajná hodnota je uložena. Zaškrtněte rotaci pro zadání nové.</p>
          )}
        </fieldset>

        <div className="flex flex-wrap gap-3">
          <button
            type="submit"
            disabled={saving || loading}
            className="rounded bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-slate-700 dark:bg-slate-700 dark:hover:bg-slate-600 disabled:opacity-60"
          >
            {saving ? 'Ukládám…' : 'Uložit nastavení'}
          </button>
          <button
            type="button"
            onClick={handleTest}
            disabled={testing || loading}
            className="rounded border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-600 dark:text-slate-200 dark:hover:bg-slate-800 disabled:opacity-60"
          >
            {testing ? 'Testuji…' : 'Testovat konfiguraci'}
          </button>
        </div>
      </form>
    </div>
  );
};
