import { FormEvent, useCallback, useEffect, useMemo, useState } from 'react';
import {
  WebhookConnector,
  WebhookConnectorUpdate,
  WebhookTestRequest,
  fetchWebhookConnector,
  saveWebhookConnector,
  testWebhookConnector
} from '../api/webhooks';

interface HeaderRow {
  id: string;
  key: string;
  value: string;
}

const createId = () =>
  typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? (crypto.randomUUID as () => string)()
    : Math.random().toString(36).slice(2);

const createHeaderRows = (headers: Record<string, string>): HeaderRow[] => {
  const entries = Object.entries(headers ?? {});
  if (entries.length === 0) {
    return [{ id: createId(), key: '', value: '' }];
  }

  return entries.map(([key, value]) => ({ id: createId(), key, value }));
};

const emptyFormState = {
  alias: '',
  enabled: false,
  url: '',
  method: 'POST',
  contentType: 'application/json',
  useSignature: false,
  signingSecret: '',
  rotateSecret: false,
  headers: [{ id: createId(), key: '', value: '' }],
  hasSecret: false
};

export const WebhookSettingsPage = () => {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);
  const [message, setMessage] = useState<string | null>(null);
  const [testMessage, setTestMessage] = useState<string | null>(null);
  const [responseSnippet, setResponseSnippet] = useState<string | null>(null);
  const [form, setForm] = useState({ ...emptyFormState });
  const [connector, setConnector] = useState<WebhookConnector | null>(null);
  const [testEventType, setTestEventType] = useState('hwinventory.webhook.test');
  const [testPayload, setTestPayload] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await fetchWebhookConnector();
      setConnector(data);
      if (data) {
        setForm({
          alias: data.alias ?? '',
          enabled: data.enabled,
          url: data.url ?? '',
          method: data.method ?? 'POST',
          contentType: data.contentType ?? 'application/json',
          useSignature: data.useSignature,
          signingSecret: '',
          rotateSecret: false,
          headers: createHeaderRows(data.headers ?? {}),
          hasSecret: data.hasSecret
        });
      } else {
        setForm({ ...emptyFormState, headers: [{ id: createId(), key: '', value: '' }] });
      }
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const headerRows = useMemo(() => form.headers, [form.headers]);

  const setField = <T extends keyof typeof form>(key: T, value: (typeof form)[T]) => {
    setForm((current) => ({ ...current, [key]: value }));
  };

  const updateHeaderRow = (id: string, key: 'key' | 'value', value: string) => {
    setForm((current) => ({
      ...current,
      headers: current.headers.map((row) => (row.id === id ? { ...row, [key]: value } : row))
    }));
  };

  const addHeaderRow = () => {
    setForm((current) => ({
      ...current,
      headers: [...current.headers, { id: createId(), key: '', value: '' }]
    }));
  };

  const removeHeaderRow = (id: string) => {
    setForm((current) => ({
      ...current,
      headers: current.headers.filter((row) => row.id !== id)
    }));
  };

  const buildHeaders = () => {
    const result: Record<string, string> = {};
    for (const row of headerRows) {
      if (!row.key.trim()) {
        continue;
      }

      result[row.key.trim()] = row.value.trim();
    }

    return result;
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setMessage(null);

    const payload: WebhookConnectorUpdate = {
      alias: form.alias,
      enabled: form.enabled,
      url: form.url,
      method: form.method,
      contentType: form.contentType,
      headers: buildHeaders(),
      useSignature: form.useSignature,
      signingSecret: form.signingSecret ? form.signingSecret : undefined,
      rotateSecret: form.rotateSecret
    };

    try {
      const updated = await saveWebhookConnector(payload);
      setConnector(updated);
      setForm((current) => ({
        ...current,
        alias: updated.alias ?? '',
        enabled: updated.enabled,
        url: updated.url ?? '',
        method: updated.method ?? 'POST',
        contentType: updated.contentType ?? 'application/json',
        headers: createHeaderRows(updated.headers ?? {}),
        useSignature: updated.useSignature,
        hasSecret: updated.hasSecret,
        signingSecret: '',
        rotateSecret: false
      }));
      setMessage('Webhook konfigurace byla uložena.');
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const handleTest = async () => {
    setTesting(true);
    setTestMessage(null);
    setResponseSnippet(null);

    const payload: WebhookTestRequest = {
      eventType: testEventType,
      payloadJson: testPayload?.trim() ? testPayload : undefined
    };

    try {
      const result = await testWebhookConnector(payload);
      setTestMessage(result.message);
      setResponseSnippet(result.responseSnippet ?? null);
      await load();
    } catch (error) {
      setTestMessage((error as Error).message);
    } finally {
      setTesting(false);
    }
  };

  const lastTested = connector?.lastTestedAtUtc
    ? new Date(connector.lastTestedAtUtc).toLocaleString()
    : '—';

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">Webhooks</h2>
        <p className="mt-1 text-sm text-slate-600 dark:text-slate-400">
          Konfigurace odchozího webhook konektoru. Definujte URL, HTTP metodu, hlavičky a případný podpis HMAC-SHA256.
        </p>
      </div>

      {loading ? (
        <div className="rounded border border-dashed border-slate-300 p-12 text-center text-slate-500 dark:border-slate-700 dark:text-slate-300">
          Načítám konfiguraci…
        </div>
      ) : (
        <form className="space-y-8" onSubmit={handleSubmit}>
          <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Základní informace</h3>
            <div className="mt-4 grid gap-4 md:grid-cols-2">
              <label className="flex flex-col gap-1 text-sm">
                <span className="text-slate-600 dark:text-slate-400">Alias</span>
                <input
                  type="text"
                  value={form.alias}
                  onChange={(event) => setField('alias', event.target.value)}
                  className="rounded border border-slate-300 px-3 py-2 text-slate-900 shadow-sm focus:border-slate-500 focus:outline-none focus:ring dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                />
              </label>
              <label className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-400">
                <input
                  type="checkbox"
                  checked={form.enabled}
                  onChange={(event) => setField('enabled', event.target.checked)}
                  className="h-4 w-4"
                />
                Konektor je aktivní
              </label>
            </div>
            <div className="mt-4 grid gap-4 md:grid-cols-2">
              <label className="flex flex-col gap-1 text-sm">
                <span className="text-slate-600 dark:text-slate-400">Webhook URL *</span>
                <input
                  type="url"
                  required
                  value={form.url}
                  onChange={(event) => setField('url', event.target.value)}
                  className="rounded border border-slate-300 px-3 py-2 text-slate-900 shadow-sm focus:border-slate-500 focus:outline-none focus:ring dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                />
              </label>
              <label className="flex flex-col gap-1 text-sm">
                <span className="text-slate-600 dark:text-slate-400">HTTP metoda</span>
                <select
                  value={form.method}
                  onChange={(event) => setField('method', event.target.value)}
                  className="rounded border border-slate-300 px-3 py-2 text-slate-900 shadow-sm focus:border-slate-500 focus:outline-none focus:ring dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                >
                  {['POST', 'PUT', 'PATCH'].map((method) => (
                    <option key={method} value={method}>
                      {method}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            <div className="mt-4 grid gap-4 md:grid-cols-2">
              <label className="flex flex-col gap-1 text-sm">
                <span className="text-slate-600 dark:text-slate-400">Content-Type</span>
                <input
                  type="text"
                  value={form.contentType}
                  onChange={(event) => setField('contentType', event.target.value)}
                  className="rounded border border-slate-300 px-3 py-2 text-slate-900 shadow-sm focus:border-slate-500 focus:outline-none focus:ring dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                />
              </label>
              <div className="text-sm text-slate-600 dark:text-slate-400">
                <span className="font-medium">Poslední test:</span> {lastTested}
                {connector?.healthStatus && (
                  <span className="ml-2 rounded bg-slate-200 px-2 py-0.5 text-xs text-slate-700 dark:bg-slate-800 dark:text-slate-200">
                    {connector.healthStatus}
                  </span>
                )}
              </div>
            </div>
          </section>

          <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">HTTP hlavičky</h3>
            <p className="mt-1 text-sm text-slate-600 dark:text-slate-400">
              Přidejte vlastní hlavičky, které se odešlou spolu s webhookem. Klíč ponechte prázdný pro odstranění řádku.
            </p>
            <div className="mt-4 space-y-3">
              {headerRows.map((row, index) => (
                <div key={row.id} className="flex flex-col gap-2 md:flex-row md:items-center">
                  <input
                    type="text"
                    placeholder="X-Custom-Header"
                    value={row.key}
                    onChange={(event) => updateHeaderRow(row.id, 'key', event.target.value)}
                    className="w-full rounded border border-slate-300 px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-slate-500 focus:outline-none focus:ring dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 md:w-1/3"
                  />
                  <input
                    type="text"
                    placeholder="Hodnota"
                    value={row.value}
                    onChange={(event) => updateHeaderRow(row.id, 'value', event.target.value)}
                    className="w-full rounded border border-slate-300 px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-slate-500 focus:outline-none focus:ring dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 md:flex-1"
                  />
                  <button
                    type="button"
                    onClick={() => removeHeaderRow(row.id)}
                    className="w-full rounded border border-slate-300 px-3 py-2 text-sm text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800 md:w-auto"
                    disabled={headerRows.length === 1 && index === 0}
                  >
                    Odebrat
                  </button>
                </div>
              ))}
              <button
                type="button"
                onClick={addHeaderRow}
                className="rounded border border-dashed border-slate-300 px-3 py-2 text-sm text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
              >
                Přidat hlavičku
              </button>
            </div>
          </section>

          <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Podpis HMAC</h3>
            <div className="mt-4 space-y-4">
              <label className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-400">
                <input
                  type="checkbox"
                  checked={form.useSignature}
                  onChange={(event) => setField('useSignature', event.target.checked)}
                  className="h-4 w-4"
                />
                Přidat podpis HMAC-SHA256 ({' '}
                <code className="text-xs">X-HWINV-Signature</code>)
              </label>
              {form.useSignature && (
                <div className="grid gap-4 md:grid-cols-2">
                  <label className="flex flex-col gap-1 text-sm">
                    <span className="text-slate-600 dark:text-slate-400">
                      Nové tajemství (ponechte prázdné pro zachování)
                    </span>
                    <input
                      type="text"
                      value={form.signingSecret}
                      onChange={(event) => setField('signingSecret', event.target.value)}
                      className="rounded border border-slate-300 px-3 py-2 text-slate-900 shadow-sm focus:border-slate-500 focus:outline-none focus:ring dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                    />
                  </label>
                  <label className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-400">
                    <input
                      type="checkbox"
                      checked={form.rotateSecret}
                      onChange={(event) => setField('rotateSecret', event.target.checked)}
                      className="h-4 w-4"
                    />
                    Vymazat stávající tajemství (nutné zadat nové před dalším testem)
                  </label>
                </div>
              )}
              {form.hasSecret && !form.useSignature && (
                <p className="text-sm text-slate-500 dark:text-slate-400">
                  Tajemství je nastaveno, ale podpis je aktuálně vypnutý.
                </p>
              )}
            </div>
          </section>

          <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Testovací událost</h3>
            <div className="mt-4 grid gap-4 md:grid-cols-2">
              <label className="flex flex-col gap-1 text-sm">
                <span className="text-slate-600 dark:text-slate-400">Typ události</span>
                <input
                  type="text"
                  value={testEventType}
                  onChange={(event) => setTestEventType(event.target.value)}
                  className="rounded border border-slate-300 px-3 py-2 text-slate-900 shadow-sm focus:border-slate-500 focus:outline-none focus:ring dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                />
              </label>
              <div className="text-sm text-slate-600 dark:text-slate-400">
                Odeslání vytvoří JSON payload s ID, typem, timestampem a zprávou. Můžete přepsat payload vlastním JSONem.
              </div>
            </div>
            <label className="mt-4 block text-sm text-slate-600 dark:text-slate-400">
              Vlastní payload (volitelné)
              <textarea
                value={testPayload}
                onChange={(event) => setTestPayload(event.target.value)}
                rows={4}
                placeholder='{"custom":"value"}'
                className="mt-1 w-full rounded border border-slate-300 px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-slate-500 focus:outline-none focus:ring dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
              />
            </label>
            <div className="mt-4 flex flex-wrap gap-3">
              <button
                type="submit"
                disabled={saving}
                className="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-slate-700 disabled:opacity-60 dark:bg-slate-700 dark:hover:bg-slate-600"
              >
                {saving ? 'Ukládám…' : 'Uložit změny'}
              </button>
              <button
                type="button"
                onClick={handleTest}
                disabled={testing}
                className="rounded border border-slate-300 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-100 disabled:opacity-60 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
              >
                {testing ? 'Testuji…' : 'Odeslat test'}
              </button>
            </div>
            {(message || testMessage) && (
              <div className="mt-4 space-y-2">
                {message && (
                  <div className="rounded border border-slate-200 bg-white px-4 py-3 text-sm text-slate-700 shadow dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200">
                    {message}
                  </div>
                )}
                {testMessage && (
                  <div className="rounded border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-700 shadow dark:border-emerald-900 dark:bg-emerald-950/60 dark:text-emerald-200">
                    {testMessage}
                    {responseSnippet && (
                      <pre className="mt-2 max-h-40 overflow-y-auto rounded bg-emerald-900/20 p-3 text-xs text-emerald-200">
                        {responseSnippet}
                      </pre>
                    )}
                  </div>
                )}
              </div>
            )}
          </section>
        </form>
      )}
    </div>
  );
};
