import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  ObservabilityConfiguration,
  ObservabilityConfigurationUpdate,
  fetchMetricsPreview,
  fetchObservabilityConfiguration,
  saveObservabilityConfiguration
} from '../api/observability';

const logLevels = ['Trace', 'Debug', 'Information', 'Warning', 'Error', 'Critical', 'None'];

const defaultState: ObservabilityConfigurationUpdate = {
  logLevel: 'Information',
  healthEndpointEnabled: true,
  metricsEndpointEnabled: false,
  correlationIdsEnabled: true,
  includeTraceIdentifier: true,
  otelExporterEnabled: false,
  otelEndpoint: '',
  otelAuthToken: '',
  rotateOtelAuthToken: false,
  resourceAttributes: ''
};

export const ObservabilitySettingsPage = () => {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);
  const [state, setState] = useState<ObservabilityConfigurationUpdate>(defaultState);
  const [current, setCurrent] = useState<ObservabilityConfiguration | null>(null);
  const [metricsPreview, setMetricsPreview] = useState<string>('');
  const [previewTimestamp, setPreviewTimestamp] = useState<string>('');
  const [previewLoading, setPreviewLoading] = useState(false);
  const [saving, setSaving] = useState(false);

  useEffect(() => {
    let mounted = true;

    const refreshMetrics = async () => {
      try {
        setPreviewLoading(true);
        const snapshot = await fetchMetricsPreview();
        if (!mounted) {
          return;
        }
        setMetricsPreview(snapshot);
        setPreviewTimestamp(new Date().toLocaleString());
      } catch (err) {
        if (mounted) {
          setMetricsPreview('Náhled metrik se nepodařilo načíst.');
        }
      } finally {
        if (mounted) {
          setPreviewLoading(false);
        }
      }
    };

    const load = async () => {
      try {
        const configuration = await fetchObservabilityConfiguration();
        if (!mounted) {
          return;
        }
        setCurrent(configuration);
        setState({
          logLevel: configuration.logLevel,
          healthEndpointEnabled: configuration.healthEndpointEnabled,
          metricsEndpointEnabled: configuration.metricsEndpointEnabled,
          correlationIdsEnabled: configuration.correlationIdsEnabled,
          includeTraceIdentifier: configuration.includeTraceIdentifier,
          otelExporterEnabled: configuration.otelExporterEnabled,
          otelEndpoint: configuration.otelEndpoint ?? '',
          otelAuthToken: '',
          rotateOtelAuthToken: false,
          resourceAttributes: configuration.resourceAttributes ?? ''
        });
        await refreshMetrics();
      } catch (err) {
        if (mounted) {
          setError((err as Error).message);
        }
      } finally {
        if (mounted) {
          setLoading(false);
        }
      }
    };

    load();
    return () => {
      mounted = false;
    };
  }, []);

  const handleRefreshMetrics = async () => {
    setError(null);
    setPreviewLoading(true);
    try {
      const snapshot = await fetchMetricsPreview();
      setMetricsPreview(snapshot);
      setPreviewTimestamp(new Date().toLocaleString());
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setPreviewLoading(false);
    }
  };

  const tokenMessage = useMemo(() => {
    if (!current?.hasOtelAuthToken) {
      return 'Token není nastaven.';
    }
    if (state.rotateOtelAuthToken || state.otelAuthToken) {
      return 'Zadáte nový token – bude uložen po uložení formuláře.';
    }
    return 'Token je nastaven. Chcete-li jej odstranit, zaškrtněte „Vymazat token“ nebo zadejte nový.';
  }, [current?.hasOtelAuthToken, state.rotateOtelAuthToken, state.otelAuthToken]);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setError(null);
    setSuccess(null);
    try {
      const payload: ObservabilityConfigurationUpdate = {
        ...state,
        otelEndpoint: state.otelEndpoint?.trim() || undefined,
        otelAuthToken: state.otelAuthToken?.trim() || undefined,
        rotateOtelAuthToken: state.rotateOtelAuthToken || Boolean(state.otelAuthToken && state.otelAuthToken.trim()),
        resourceAttributes: state.resourceAttributes?.trim() || undefined
      };

      const updated = await saveObservabilityConfiguration(payload);
      setCurrent(updated);
      setState((previous) => ({
        ...previous,
        rotateOtelAuthToken: false,
        otelAuthToken: ''
      }));
      setSuccess('Nastavení bylo uloženo.');
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return <div className="text-sm text-slate-600 dark:text-slate-300">Načítám observability nastavení…</div>;
  }

  return (
    <div className="space-y-8">
      <header className="space-y-2">
        <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">Observabilita</h1>
        <p className="max-w-3xl text-sm text-slate-600 dark:text-slate-300">
          Spravujte logovací úroveň, dostupnost koncových bodů /health a /metrics a konfiguraci OpenTelemetry exporteru. Změny
          se ukládají okamžitě a zapisují do auditu.
        </p>
      </header>

      {error && (
        <div className="rounded border border-red-200 bg-red-50 p-4 text-sm text-red-800 dark:border-red-800 dark:bg-red-950 dark:text-red-200">
          {error}
        </div>
      )}

      {success && (
        <div className="rounded border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800 dark:border-emerald-800 dark:bg-emerald-950 dark:text-emerald-200">
          {success}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-6">
        <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Logování a telemetry</h2>
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            <label className="flex flex-col text-sm">
              Log level
              <select
                value={state.logLevel}
                onChange={(event) => setState((previous) => ({ ...previous, logLevel: event.target.value }))}
                className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              >
                {logLevels.map((level) => (
                  <option key={level} value={level}>
                    {level}
                  </option>
                ))}
              </select>
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={state.healthEndpointEnabled}
                onChange={(event) => setState((previous) => ({ ...previous, healthEndpointEnabled: event.target.checked }))}
              />
              Povolit /health
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={state.metricsEndpointEnabled}
                onChange={(event) => setState((previous) => ({ ...previous, metricsEndpointEnabled: event.target.checked }))}
              />
              Povolit /metrics
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={state.correlationIdsEnabled}
                onChange={(event) => setState((previous) => ({ ...previous, correlationIdsEnabled: event.target.checked }))}
              />
              Přidávat X-Correlation-Id
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={state.includeTraceIdentifier}
                onChange={(event) => setState((previous) => ({ ...previous, includeTraceIdentifier: event.target.checked }))}
              />
              Přidat trace-id do odpovědi
            </label>
          </div>
        </section>

        <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">OpenTelemetry Exporter</h2>
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            <label className="flex items-center gap-2 text-sm md:col-span-2">
              <input
                type="checkbox"
                checked={state.otelExporterEnabled}
                onChange={(event) => setState((previous) => ({ ...previous, otelExporterEnabled: event.target.checked }))}
              />
              Povolit odesílání do OTLP endpointu
            </label>
            <label className="flex flex-col text-sm md:col-span-2">
              Endpoint
              <input
                type="url"
                placeholder="https://otel.example.com:4318/v1/traces"
                value={state.otelEndpoint ?? ''}
                onChange={(event) => setState((previous) => ({ ...previous, otelEndpoint: event.target.value }))}
                className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </label>
            <label className="flex flex-col text-sm md:col-span-2">
              Token
              <input
                type="password"
                value={state.otelAuthToken ?? ''}
                onChange={(event) => setState((previous) => ({ ...previous, otelAuthToken: event.target.value }))}
                placeholder="••••••"
                className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
              <span className="mt-1 text-xs text-slate-500 dark:text-slate-400">{tokenMessage}</span>
              <div className="mt-2 flex items-center gap-2">
                <label className="flex items-center gap-2 text-xs text-slate-600 dark:text-slate-300">
                  <input
                    type="checkbox"
                    checked={state.rotateOtelAuthToken}
                    onChange={(event) =>
                      setState((previous) => ({ ...previous, rotateOtelAuthToken: event.target.checked }))
                    }
                  />
                  Vymazat aktuální token
                </label>
                <button
                  type="button"
                  onClick={() =>
                    setState((previous) => ({
                      ...previous,
                      otelAuthToken: '',
                      rotateOtelAuthToken: true
                    }))
                  }
                  className="text-xs font-semibold text-red-600 hover:underline"
                >
                  Vymazat
                </button>
              </div>
            </label>
            <label className="flex flex-col text-sm md:col-span-2">
              Resource attributes (např. <code>service.name=hw-inventory,environment=prod</code>)
              <textarea
                rows={3}
                value={state.resourceAttributes ?? ''}
                onChange={(event) => setState((previous) => ({ ...previous, resourceAttributes: event.target.value }))}
                className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </label>
          </div>
        </section>

        <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Náhled metrik</h2>
          <div className="mt-4 flex items-center justify-between text-xs text-slate-500 dark:text-slate-400">
            <span>{previewTimestamp ? `Poslední aktualizace: ${previewTimestamp}` : 'Náhled zatím nebyl načten.'}</span>
            <button
              type="button"
              onClick={handleRefreshMetrics}
              className="rounded border border-slate-300 px-3 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-100 dark:border-slate-600 dark:text-slate-100 dark:hover:bg-slate-800"
            >
              Obnovit náhled
            </button>
          </div>
          <pre className="mt-3 max-h-64 overflow-auto rounded bg-slate-900 p-4 text-xs text-slate-100">
            {previewLoading ? 'Načítám…' : metricsPreview || 'Žádná data.'}
          </pre>
        </section>

        <div className="flex justify-end gap-3">
          <button
            type="button"
            onClick={() => {
              if (!current) {
                return;
              }
              setState({
                logLevel: current.logLevel,
                healthEndpointEnabled: current.healthEndpointEnabled,
                metricsEndpointEnabled: current.metricsEndpointEnabled,
                correlationIdsEnabled: current.correlationIdsEnabled,
                includeTraceIdentifier: current.includeTraceIdentifier,
                otelExporterEnabled: current.otelExporterEnabled,
                otelEndpoint: current.otelEndpoint ?? '',
                otelAuthToken: '',
                rotateOtelAuthToken: false,
                resourceAttributes: current.resourceAttributes ?? ''
              });
              setError(null);
              setSuccess(null);
            }}
            className="rounded border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-100 dark:border-slate-600 dark:text-slate-100 dark:hover:bg-slate-800"
          >
            Zrušit změny
          </button>
          <button
            type="submit"
            disabled={saving}
            className="rounded bg-sky-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-sky-700 disabled:opacity-60"
          >
            {saving ? 'Ukládám…' : 'Uložit' }
          </button>
        </div>
      </form>
    </div>
  );
};
