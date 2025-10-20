import { ChangeEvent, FormEvent, useEffect, useState } from 'react';
import {
  HandoverConfiguration,
  HandoverConfigurationUpdate,
  fetchHandoverConfiguration,
  saveHandoverConfiguration
} from '../api/handover';
import { HelpTooltip } from '../components/HelpTooltip';

const emptyRecipients = ['', '', ''];

type RecipientType = 'to' | 'cc' | 'bcc';

type RecipientErrors = Record<RecipientType, string[]>;

const createEmptyErrors = (): RecipientErrors => ({
  to: ['', '', ''],
  cc: ['', '', ''],
  bcc: ['', '', '']
});

const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/i;

const normalizeSlots = (values: string[] | undefined) => {
  const slots = [...emptyRecipients];
  if (!values) {
    return slots;
  }
  values.slice(0, 3).forEach((value, index) => {
    slots[index] = value ?? '';
  });
  return slots;
};

export const HandoverSettingsPage = () => {
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [saving, setSaving] = useState(false);
  const [config, setConfig] = useState<HandoverConfiguration | null>(null);
  const [state, setState] = useState({
    to: [...emptyRecipients],
    cc: [...emptyRecipients],
    bcc: [...emptyRecipients],
    subject: '',
    body: '',
    pdfLogoBase64: '' as string,
    useMinimalPdf: false,
    pdfFooterNote: ''
  });
  const [recipientErrors, setRecipientErrors] = useState<RecipientErrors>(() => createEmptyErrors());
  const [logoPreview, setLogoPreview] = useState<string | null>(null);

  useEffect(() => {
    let isMounted = true;
    setLoading(true);
    fetchHandoverConfiguration()
      .then((result) => {
        if (!isMounted) {
          return;
        }
        setConfig(result);
        setState({
          to: normalizeSlots(result.defaultTo),
          cc: normalizeSlots(result.defaultCc),
          bcc: normalizeSlots(result.defaultBcc),
          subject: result.defaultSubject ?? '',
          body: result.defaultBody ?? '',
          pdfLogoBase64: result.pdfLogoBase64 ?? '',
          useMinimalPdf: result.useMinimalPdf,
          pdfFooterNote: result.pdfFooterNote ?? ''
        });
        setRecipientErrors(createEmptyErrors());
        setLogoPreview(result.pdfLogoBase64 ? `data:image/png;base64,${result.pdfLogoBase64}` : null);
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

  const handleRecipientChange = (type: 'to' | 'cc' | 'bcc', index: number, value: string) => {
    setState((previous) => {
      const updated = [...previous[type]];
      updated[index] = value;
      return { ...previous, [type]: updated };
    });
    setRecipientErrors((previous) => {
      const next: RecipientErrors = {
        to: [...previous.to],
        cc: [...previous.cc],
        bcc: [...previous.bcc]
      };
      next[type][index] = '';
      return next;
    });
  };

  const handleLogoUpload = (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0];
    if (!file) {
      return;
    }

    const reader = new FileReader();
    reader.onload = () => {
      const result = reader.result as string;
      const base64 = result.includes(',') ? result.split(',')[1] : result;
      setState((previous) => ({ ...previous, pdfLogoBase64: base64 }));
      setLogoPreview(result);
    };
    reader.readAsDataURL(file);
  };

  const clearLogo = () => {
    setState((previous) => ({ ...previous, pdfLogoBase64: '' }));
    setLogoPreview(null);
  };

  const validateRecipients = () => {
    const next = createEmptyErrors();
    let hasError = false;

    (['to', 'cc', 'bcc'] as RecipientType[]).forEach((type) => {
      const filled = state[type].filter((value) => value.trim().length > 0);
      if (filled.length > 3) {
        next[type][0] = 'Lze zadat maximálně tři adresy.';
        hasError = true;
      }

      state[type].forEach((value, index) => {
        const trimmed = value.trim();
        if (!trimmed) {
          return;
        }

        if (!emailPattern.test(trimmed)) {
          next[type][index] = 'Zadejte platnou e-mailovou adresu.';
          hasError = true;
        }
      });
    });

    setRecipientErrors(next);

    if (hasError) {
      setError('Zkontrolujte prosím vyplněné e-mailové adresy.');
      return false;
    }

    return true;
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);

    if (!validateRecipients()) {
      return;
    }

    setSaving(true);

    const payload: HandoverConfigurationUpdate = {
      defaultTo: state.to.filter((value) => value.trim().length > 0),
      defaultCc: state.cc.filter((value) => value.trim().length > 0),
      defaultBcc: state.bcc.filter((value) => value.trim().length > 0),
      defaultSubject: state.subject.trim() || null,
      defaultBody: state.body.trim() || null,
      pdfLogoBase64: state.pdfLogoBase64.trim() || null,
      useMinimalPdf: state.useMinimalPdf,
      pdfFooterNote: state.pdfFooterNote.trim() || null
    };

    try {
      const saved = await saveHandoverConfiguration(payload);
      setConfig(saved);
      setState((previous) => ({
        ...previous,
        to: normalizeSlots(saved.defaultTo),
        cc: normalizeSlots(saved.defaultCc),
        bcc: normalizeSlots(saved.defaultBcc),
        subject: saved.defaultSubject ?? '',
        body: saved.defaultBody ?? '',
        pdfLogoBase64: saved.pdfLogoBase64 ?? '',
        useMinimalPdf: saved.useMinimalPdf,
        pdfFooterNote: saved.pdfFooterNote ?? ''
      }));
      setLogoPreview(saved.pdfLogoBase64 ? `data:image/png;base64,${saved.pdfLogoBase64}` : null);
      setRecipientErrors(createEmptyErrors());
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setSaving(false);
    }
  };

  if (loading) {
    return <div className="text-sm text-slate-600 dark:text-slate-300">Načítám nastavení předávacích protokolů…</div>;
  }

  return (
    <div className="space-y-8">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="space-y-2">
          <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">Předávací protokoly</h1>
          <p className="max-w-3xl text-sm text-slate-600 dark:text-slate-300">
            Nastavte výchozí příjemce, šablony e-mailů a vzhled PDF dokumentů, které jsou generovány při předávání pracovních
            stanic. Změny se ukládají okamžitě po uložení formuláře a každá úprava je zaznamenána do auditu.
          </p>
        </div>
        <HelpTooltip manualPath="handover.html" label="Zobrazit kapitolu nápovědy k předávacím protokolům" />
      </header>

      {error && (
        <div className="rounded border border-red-200 bg-red-50 p-4 text-sm text-red-800 dark:border-red-800 dark:bg-red-950 dark:text-red-200">
          {error}
        </div>
      )}

      <form onSubmit={handleSubmit} className="space-y-6">
        <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <div className="mb-4 flex items-center justify-between">
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Výchozí příjemci</h2>
            <span className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">Max. 3 adresy na sekci</span>
          </div>
          <div className="grid gap-6 md:grid-cols-3">
            {(['to', 'cc', 'bcc'] as const).map((type) => (
              <div key={type} className="space-y-2">
                <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200">
                  {type === 'to' ? 'Primární příjemci (To)' : type === 'cc' ? 'Kopie (CC)' : 'Skrytá kopie (BCC)'}
                </h3>
                {state[type].map((value, index) => {
                  const fieldError = recipientErrors[type][index];
                  const baseClasses = fieldError
                    ? 'border-red-500 focus:border-red-500 focus:ring-red-500 dark:border-red-400'
                    : 'border-slate-300 focus:border-indigo-500 focus:ring-indigo-500 dark:border-slate-700';

                  return (
                    <div key={index} className="space-y-1">
                      <input
                        type="email"
                        value={value}
                        placeholder={`adresa ${index + 1}`}
                        onChange={(event) => handleRecipientChange(type, index, event.target.value)}
                        className={`w-full rounded px-3 py-2 text-sm dark:bg-slate-800 dark:text-white border ${baseClasses}`}
                        aria-invalid={fieldError ? 'true' : 'false'}
                        aria-describedby={fieldError ? `${type}-${index}-error` : undefined}
                      />
                      {fieldError && (
                        <p id={`${type}-${index}-error`} className="text-xs text-red-600 dark:text-red-400">
                          {fieldError}
                        </p>
                      )}
                    </div>
                  );
                })}
              </div>
            ))}
          </div>
        </section>

        <section className="space-y-4 rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Šablona e-mailu</h2>
          <p className="text-sm text-slate-600 dark:text-slate-300">
            Podporované proměnné: <code className="rounded bg-slate-100 px-1 dark:bg-slate-800">{'{AssetTag}'}</code>,{' '}
            <code className="rounded bg-slate-100 px-1 dark:bg-slate-800">{'{Name}'}</code>,{' '}
            <code className="rounded bg-slate-100 px-1 dark:bg-slate-800">{'{OldLocation}'}</code>,{' '}
            <code className="rounded bg-slate-100 px-1 dark:bg-slate-800">{'{NewLocation}'}</code>,{' '}
            <code className="rounded bg-slate-100 px-1 dark:bg-slate-800">{'{OldOwner}'}</code>,{' '}
            <code className="rounded bg-slate-100 px-1 dark:bg-slate-800">{'{NewOwner}'}</code>,{' '}
            <code className="rounded bg-slate-100 px-1 dark:bg-slate-800">{'{Date}'}</code>.
          </p>
          <label className="flex flex-col gap-2 text-sm">
            Předmět
            <input
              type="text"
              value={state.subject}
              onChange={(event) => setState((previous) => ({ ...previous, subject: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800 dark:text-white"
            />
          </label>
          <label className="flex flex-col gap-2 text-sm">
            Text zprávy
            <textarea
              value={state.body}
              rows={6}
              onChange={(event) => setState((previous) => ({ ...previous, body: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800 dark:text-white"
            />
          </label>
        </section>

        <section className="space-y-4 rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-white">PDF protokol</h2>
          <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
            <input
              type="checkbox"
              checked={state.useMinimalPdf}
              onChange={(event) => setState((previous) => ({ ...previous, useMinimalPdf: event.target.checked }))}
            />
            Použít minimalistickou verzi PDF (základní souhrn místo detailní tabulky)
          </label>
          <label className="flex flex-col gap-2 text-sm">
            Vlastní poznámka v patičce
            <textarea
              value={state.pdfFooterNote}
              rows={3}
              onChange={(event) => setState((previous) => ({ ...previous, pdfFooterNote: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800 dark:text-white"
            />
          </label>
          <div className="space-y-3">
            <span className="block text-sm font-semibold text-slate-700 dark:text-slate-200">Logo v PDF (PNG/JPG)</span>
            <div className="flex items-center gap-3">
              <input type="file" accept="image/png,image/jpeg" onChange={handleLogoUpload} />
              {logoPreview && (
                <button
                  type="button"
                  onClick={clearLogo}
                  className="rounded border border-slate-300 px-3 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-100 dark:border-slate-600 dark:text-slate-100 dark:hover:bg-slate-800"
                >
                  Odebrat logo
                </button>
              )}
            </div>
            {logoPreview ? (
              <img src={logoPreview} alt="Náhled loga" className="max-h-24 rounded border border-slate-200 dark:border-slate-700" />
            ) : (
              <p className="text-xs text-slate-500 dark:text-slate-400">Logo zatím není nastaveno.</p>
            )}
          </div>
        </section>

        <div className="flex items-center justify-between">
          <div className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">
            Poslední uložení: {config?.defaultSubject ? 'konfigurováno' : 'výchozí hodnoty'}
          </div>
          <button
            type="submit"
            disabled={saving}
            className="rounded bg-slate-900 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-slate-700 disabled:opacity-60 dark:bg-slate-700 dark:hover:bg-slate-600"
          >
            {saving ? 'Ukládám…' : 'Uložit změny'}
          </button>
        </div>
      </form>
    </div>
  );
};
