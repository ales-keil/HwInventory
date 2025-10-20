import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  EmailSettings,
  EmailSettingsUpdate,
  EmailTestRequest,
  fetchEmailSettings,
  saveEmailSettings,
  sendEmailTest
} from '../api/email';
import { HelpTooltip } from '../components/HelpTooltip';

type EmailSettingsPageProps = {
  variant?: 'default' | 'wizard';
};

const defaultUpdate: EmailSettingsUpdate = {
  alias: 'Primary SMTP',
  enabled: true,
  host: '',
  port: 587,
  useTls: true,
  username: '',
  fromAddress: '',
  replyToAddress: '',
  rotateSecret: false,
  password: ''
};

export const EmailSettingsPage = ({ variant = 'default' }: EmailSettingsPageProps) => {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [state, setState] = useState<EmailSettingsUpdate>(defaultUpdate);
  const [current, setCurrent] = useState<EmailSettings | null>(null);
  const [testRequest, setTestRequest] = useState<EmailTestRequest>({
    recipient: '',
    subject: 'HW Inventory – SMTP test',
    body: 'Toto je testovací e-mail odeslaný z administrace HW Inventory.'
  });
  const [testResult, setTestResult] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [testing, setTesting] = useState(false);

  useEffect(() => {
    let isMounted = true;
    setLoading(true);
    fetchEmailSettings()
      .then((settings) => {
        if (!isMounted) {
          return;
        }
        setCurrent(settings);
        if (settings) {
          setState({
            alias: settings.alias ?? 'Primary SMTP',
            enabled: settings.enabled,
            host: settings.host ?? '',
            port: settings.port ?? 587,
            useTls: settings.useTls ?? true,
            username: settings.username ?? '',
            fromAddress: settings.fromAddress ?? '',
            replyToAddress: settings.replyToAddress ?? '',
            rotateSecret: false,
            password: ''
          });
        }
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

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setSaving(true);
    setError(null);
    try {
      const payload: EmailSettingsUpdate = {
        ...state,
        port: Number(state.port) || 25,
        rotateSecret: state.rotateSecret || Boolean(state.password)
      };
      const result = await saveEmailSettings(payload);
      setCurrent(result);
      setState((previous) => ({
        ...previous,
        rotateSecret: false,
        password: ''
      }));
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setSaving(false);
    }
  };

  const handleTest = async () => {
    setTesting(true);
    setTestResult(null);
    setError(null);
    try {
      const result = await sendEmailTest(testRequest);
      setTestResult(result.message);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setTesting(false);
    }
  };

  const maskMessage = useMemo(() => {
    if (!current?.hasPassword) {
      return 'Heslo není nastaveno.';
    }
    if (state.rotateSecret || state.password) {
      return 'Zadáte nové heslo – bude uloženo po uložení formuláře.';
    }
    return 'Heslo je nastaveno. Chcete-li jej změnit, zadejte nové nebo zvolte "Vymazat".';
  }, [current?.hasPassword, state.rotateSecret, state.password]);

  if (loading) {
    return <div className="text-sm text-slate-600 dark:text-slate-300">Načítám nastavení SMTP…</div>;
  }

  return (
    <div className="space-y-8">
      {variant === 'default' && (
        <header className="flex flex-wrap items-start justify-between gap-4">
          <div className="space-y-2">
            <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">E-mail (SMTP)</h1>
            <p className="max-w-3xl text-sm text-slate-600 dark:text-slate-300">
            Nakonfigurujte připojení k SMTP serveru pro odesílání notifikací, předávacích protokolů a systémových hlášení.
            Zadané hodnoty se ukládají okamžitě po uložení formuláře a změny se zaznamenávají do auditu.
            </p>
          </div>
          <HelpTooltip manualPath="updates.html" label="Otevřít kapitolu nápovědy k e-mailovým konektorům" />
        </header>
      )}

      {error && (
        <div className="rounded border border-red-200 bg-red-50 p-4 text-sm text-red-800 dark:border-red-800 dark:bg-red-950 dark:text-red-200">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-6">
        <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Připojení</h2>
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            <label className="flex flex-col text-sm">
              Alias
              <input
                type="text"
                value={state.alias}
                onChange={(event) => setState((previous) => ({ ...previous, alias: event.target.value }))}
                className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </label>
            <label className="flex flex-col text-sm">
              Host
              <input
                type="text"
                required
                value={state.host}
                onChange={(event) => setState((previous) => ({ ...previous, host: event.target.value }))}
                className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </label>
            <label className="flex flex-col text-sm">
              Port
              <input
                type="number"
                min={1}
                max={65535}
                value={state.port}
                onChange={(event) => setState((previous) => ({ ...previous, port: Number(event.target.value) }))}
                className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={state.useTls}
                onChange={(event) => setState((previous) => ({ ...previous, useTls: event.target.checked }))}
              />
              Použít TLS/SSL
            </label>
            <label className="flex items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={state.enabled}
                onChange={(event) => setState((previous) => ({ ...previous, enabled: event.target.checked }))}
              />
              Aktivní konektor
            </label>
          </div>
        </section>

        <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Autentizace</h2>
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            <label className="flex flex-col text-sm">
              Uživatelské jméno
              <input
                type="text"
                value={state.username ?? ''}
                onChange={(event) => setState((previous) => ({ ...previous, username: event.target.value }))}
                className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </label>
            <label className="flex flex-col text-sm">
              Heslo
              <input
                type="password"
                placeholder={current?.hasPassword ? '••••••••' : 'Není nastaveno'}
                value={state.password ?? ''}
                onChange={(event) =>
                  setState((previous) => ({ ...previous, password: event.target.value, rotateSecret: true }))
                }
                className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </label>
          </div>
          <p className="mt-3 text-xs text-slate-500 dark:text-slate-400">{maskMessage}</p>
          {current?.hasPassword && !state.password && (
            <button
              type="button"
              onClick={() => setState((previous) => ({ ...previous, rotateSecret: true }))}
              className="mt-3 text-sm text-sky-600 hover:underline"
            >
              Vymazat uložené heslo při uložení
            </button>
          )}
        </section>

        <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Adresy</h2>
          <div className="mt-4 grid gap-4 md:grid-cols-2">
            <label className="flex flex-col text-sm">
              From adresa
              <input
                type="email"
                required
                value={state.fromAddress ?? ''}
                onChange={(event) => setState((previous) => ({ ...previous, fromAddress: event.target.value }))}
                className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </label>
            <label className="flex flex-col text-sm">
              Reply-To adresa (volitelně)
              <input
                type="email"
                value={state.replyToAddress ?? ''}
                onChange={(event) => setState((previous) => ({ ...previous, replyToAddress: event.target.value }))}
                className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
              />
            </label>
          </div>
        </section>

        <div className="flex justify-end gap-3">
          <button
            type="submit"
            disabled={saving}
            className="rounded bg-sky-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-sky-700 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {saving ? 'Ukládám…' : 'Uložit změny'}
          </button>
        </div>
      </form>

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Testovací e-mail</h2>
        <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
          Ověřte funkčnost SMTP připojení odesláním testovací zprávy. Výsledek testu se uloží do auditu a zdravotního stavu konektoru.
        </p>
        <div className="mt-4 grid gap-4 md:grid-cols-2">
          <label className="flex flex-col text-sm">
            Příjemce
            <input
              type="email"
              required
              value={testRequest.recipient}
              onChange={(event) => setTestRequest((previous) => ({ ...previous, recipient: event.target.value }))}
              className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
            />
          </label>
          <label className="flex flex-col text-sm">
            Předmět
            <input
              type="text"
              value={testRequest.subject}
              onChange={(event) => setTestRequest((previous) => ({ ...previous, subject: event.target.value }))}
              className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
            />
          </label>
        </div>
        <label className="mt-4 flex flex-col text-sm">
          Text zprávy
          <textarea
            rows={4}
            value={testRequest.body}
            onChange={(event) => setTestRequest((previous) => ({ ...previous, body: event.target.value }))}
            className="mt-1 rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-white"
          />
        </label>
        <div className="mt-4 flex items-center gap-3">
          <button
            type="button"
            onClick={handleTest}
            disabled={testing}
            className="rounded bg-emerald-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-emerald-700 disabled:cursor-not-allowed disabled:opacity-50"
          >
            {testing ? 'Odesílám…' : 'Odeslat test'}
          </button>
          {testResult && <span className="text-sm text-emerald-600 dark:text-emerald-400">{testResult}</span>}
        </div>

        {current && (
          <dl className="mt-6 grid gap-4 text-xs text-slate-500 dark:text-slate-400 md:grid-cols-3">
            <div>
              <dt>Zdravotní stav</dt>
              <dd className="font-medium text-slate-700 dark:text-slate-200">{current.healthStatus ?? 'neznámý'}</dd>
            </div>
            <div>
              <dt>Poslední test</dt>
              <dd className="font-medium text-slate-700 dark:text-slate-200">
                {current.lastTestedAtUtc ? new Date(current.lastTestedAtUtc).toLocaleString() : '—'}
              </dd>
            </div>
            <div>
              <dt>Alias konektoru</dt>
              <dd className="font-medium text-slate-700 dark:text-slate-200">{current.alias}</dd>
            </div>
          </dl>
        )}
      </section>
    </div>
  );
};
