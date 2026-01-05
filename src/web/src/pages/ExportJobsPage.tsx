import { FormEvent, useEffect, useState } from 'react';
import {
  ExportJob,
  QueueExportPayload,
  buildExportDownloadUrl,
  listExportJobs,
  queueExportJob
} from '../api/exportJobs';
import {
  ImportJob,
  QueueImportPayload,
  buildImportLogUrl,
  listImportJobs,
  queueImportJob
} from '../api/importJobs';
import { HelpTooltip } from '../components/HelpTooltip';

const scopes = [
  { value: 'Servers', label: 'Servery' },
  { value: 'NetworkDevices', label: 'Síťová zařízení' },
  { value: 'Workstations', label: 'Pracovní stanice' },
  { value: 'Dictionaries', label: 'Číselníky' }
];

const exportFormats = [
  { value: 'Csv', label: 'CSV' },
  { value: 'Xlsx', label: 'Excel (XLSX)' }
];

const importFormats = exportFormats;

const conflictStrategies = [
  { value: 'Skip', label: 'Přeskočit existující' },
  { value: 'Update', label: 'Aktualizovat' },
  { value: 'CreateNew', label: 'Vždy vytvořit nový' }
];

const defaultExportPayload: QueueExportPayload = {
  scope: 'Servers',
  format: 'Csv',
  storagePath: '',
  filterJson: '',
  sendEmail: false,
  emailRecipients: ''
};

const defaultImportForm = {
  scope: 'Servers' as QueueImportPayload['scope'],
  format: 'Csv' as QueueImportPayload['format'],
  conflictStrategy: 'Skip' as QueueImportPayload['conflictStrategy'],
  dryRun: true,
  storagePath: '',
  fileName: '',
  sendEmail: false,
  emailRecipients: '',
  mappingJson: ''
};

const fileToBase64 = (file: File): Promise<string> =>
  new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error('Soubor se nepodařilo načíst.'));
    reader.onload = () => {
      const result = reader.result;
      if (typeof result === 'string') {
        const base64 = result.split(',').pop();
        resolve(base64 ?? '');
      } else {
        reject(new Error('Nepodařilo se převést soubor.'));
      }
    };
    reader.readAsDataURL(file);
  });

export const ExportJobsPage = () => {
  const [exportHistory, setExportHistory] = useState<ExportJob[]>([]);
  const [exportLoading, setExportLoading] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);
  const [exportFeedback, setExportFeedback] = useState<string | null>(null);
  const [exportForm, setExportForm] = useState<QueueExportPayload>({ ...defaultExportPayload });

  const [importHistory, setImportHistory] = useState<ImportJob[]>([]);
  const [importLoading, setImportLoading] = useState(false);
  const [importError, setImportError] = useState<string | null>(null);
  const [importFeedback, setImportFeedback] = useState<string | null>(null);
  const [importForm, setImportForm] = useState({ ...defaultImportForm });
  const [importFile, setImportFile] = useState<File | null>(null);

  const loadExportHistory = async () => {
    setExportLoading(true);
    setExportError(null);
    try {
      const data = await listExportJobs(1, 50);
      setExportHistory(data);
    } catch (err) {
      setExportError((err as Error).message);
    } finally {
      setExportLoading(false);
    }
  };

  const loadImportHistory = async () => {
    setImportLoading(true);
    setImportError(null);
    try {
      const data = await listImportJobs(1, 50);
      setImportHistory(data);
    } catch (err) {
      setImportError((err as Error).message);
    } finally {
      setImportLoading(false);
    }
  };

  useEffect(() => {
    loadExportHistory();
    loadImportHistory();
  }, []);

  const handleExportSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setExportError(null);
    setExportFeedback(null);

    try {
      const payload: QueueExportPayload = {
        ...exportForm,
        filterJson: exportForm.filterJson ? exportForm.filterJson : undefined,
        emailRecipients: exportForm.sendEmail ? exportForm.emailRecipients : undefined
      };
      await queueExportJob(payload);
      setExportFeedback('Export byl zařazen do fronty.');
      await loadExportHistory();
      setExportForm({ ...defaultExportPayload, storagePath: exportForm.storagePath });
    } catch (err) {
      setExportError((err as Error).message);
    }
  };

  const handleImportSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setImportError(null);
    setImportFeedback(null);

    if (!importFile) {
      setImportError('Vyberte soubor pro import.');
      return;
    }

    try {
      const base64 = await fileToBase64(importFile);
      const payload: QueueImportPayload = {
        scope: importForm.scope,
        format: importForm.format,
        conflictStrategy: importForm.conflictStrategy,
        dryRun: importForm.dryRun,
        storagePath: importForm.storagePath,
        fileName: importForm.fileName || importFile.name,
        contentBase64: base64,
        sendEmail: importForm.sendEmail,
        emailRecipients: importForm.sendEmail ? importForm.emailRecipients : undefined,
        mappingJson: importForm.mappingJson || undefined
      };

      await queueImportJob(payload);
      setImportFeedback('Import byl zařazen do fronty.');
      await loadImportHistory();
      setImportForm({ ...defaultImportForm, storagePath: importForm.storagePath });
      setImportFile(null);
    } catch (err) {
      setImportError((err as Error).message);
    }
  };

  return (
    <div className="space-y-10">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div className="space-y-2">
          <h2 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">Import/Export</h2>
          <p className="text-sm text-slate-600 dark:text-slate-400">
            Správa exportů a importů inventáře, včetně historie zpracovaných úloh.
          </p>
        </div>
        <HelpTooltip manualPath="import-export.html" label="Otevřít kapitolu nápovědy k importu a exportu" />
      </header>

      {exportError && (
        <div className="rounded border border-red-200 bg-red-50 p-3 text-sm text-red-800">{exportError}</div>
      )}
      {exportFeedback && (
        <div className="rounded border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">
          {exportFeedback}
        </div>
      )}

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Nový export</h3>
        <form className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-2" onSubmit={handleExportSubmit}>
          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200">
            Rozsah
            <select
              className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
              value={exportForm.scope}
              onChange={(event) => setExportForm((prev) => ({ ...prev, scope: event.target.value }))}
            >
              {scopes.map((scope) => (
                <option key={scope.value} value={scope.value}>
                  {scope.label}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200">
            Formát
            <select
              className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
              value={exportForm.format}
              onChange={(event) => setExportForm((prev) => ({ ...prev, format: event.target.value }))}
            >
              {exportFormats.map((format) => (
                <option key={format.value} value={format.value}>
                  {format.label}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">
            Cílová složka
            <input
              className="rounded border border-slate-300 p-2 dark:border-slate-700 dark:bg-slate-800"
              value={exportForm.storagePath}
              onChange={(event) => setExportForm((prev) => ({ ...prev, storagePath: event.target.value }))}
              required
              placeholder={'např. C\\\Exports'}
            />
          </label>

          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">
            Filtr (JSON)
            <textarea
              className="rounded border border-slate-300 p-2 dark:border-slate-700 dark:bg-slate-800"
              value={exportForm.filterJson ?? ''}
              onChange={(event) => setExportForm((prev) => ({ ...prev, filterJson: event.target.value }))}
              placeholder={'{"field":"status","value":"Active"}'}
              rows={3}
            />
          </label>

          <label className="flex items-center gap-2 text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">
            <input
              type="checkbox"
              checked={exportForm.sendEmail}
              onChange={(event) => setExportForm((prev) => ({ ...prev, sendEmail: event.target.checked }))}
            />
            Odeslat notifikaci e-mailem po dokončení
          </label>

          {exportForm.sendEmail && (
            <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">
              Příjemci (oddělte středníkem)
              <input
                className="rounded border border-slate-300 p-2 dark:border-slate-700 dark:bg-slate-800"
                value={exportForm.emailRecipients ?? ''}
                onChange={(event) => setExportForm((prev) => ({ ...prev, emailRecipients: event.target.value }))}
              />
            </label>
          )}

          <div className="md:col-span-2">
            <button
              type="submit"
              className="rounded bg-indigo-600 px-4 py-2 text-sm font-semibold text-white shadow hover:bg-indigo-700"
              disabled={exportLoading}
            >
              Spustit export
            </button>
          </div>
        </form>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Historie exportů</h3>
          <button
            className="text-sm text-indigo-600 hover:underline dark:text-indigo-300"
            type="button"
            onClick={loadExportHistory}
          >
            Obnovit
          </button>
        </div>

        <div className="mt-4 overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-left text-sm dark:divide-slate-700">
            <thead className="bg-slate-50 dark:bg-slate-800">
              <tr>
                <th className="px-3 py-2 font-medium text-slate-600 dark:text-slate-300">Rozsah</th>
                <th className="px-3 py-2 font-medium text-slate-600 dark:text-slate-300">Formát</th>
                <th className="px-3 py-2 font-medium text-slate-600 dark:text-slate-300">Stav</th>
                <th className="px-3 py-2 font-medium text-slate-600 dark:text-slate-300">Vytvořeno</th>
                <th className="px-3 py-2 font-medium text-slate-600 dark:text-slate-300">Dokončeno</th>
                <th className="px-3 py-2 font-medium text-slate-600 dark:text-slate-300">Platnost do</th>
                <th className="px-3 py-2 font-medium text-slate-600 dark:text-slate-300">Stažení</th>
                <th className="px-3 py-2 font-medium text-slate-600 dark:text-slate-300">Soubor</th>
                <th className="px-3 py-2 font-medium text-slate-600 dark:text-slate-300">Akce</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 dark:divide-slate-700">
              {exportHistory.map((job) => {
                const expiresAt = job.expiresAtUtc ? new Date(job.expiresAtUtc) : null;
                const expired = expiresAt ? expiresAt.getTime() < Date.now() : false;

                return (
                  <tr key={job.id} className="hover:bg-slate-50 dark:hover:bg-slate-800">
                    <td className="px-3 py-2 text-slate-700 dark:text-slate-200">{job.scope}</td>
                    <td className="px-3 py-2 text-slate-700 dark:text-slate-200">{job.format}</td>
                    <td className="px-3 py-2 text-slate-700 dark:text-slate-200">{job.status}</td>
                    <td className="px-3 py-2 text-slate-700 dark:text-slate-200">
                      {new Date(job.createdAtUtc).toLocaleString()}
                    </td>
                    <td className="px-3 py-2 text-slate-700 dark:text-slate-200">
                      {job.completedAtUtc ? new Date(job.completedAtUtc).toLocaleString() : '—'}
                    </td>
                    <td className="px-3 py-2 text-slate-700 dark:text-slate-200">
                      {expiresAt ? expiresAt.toLocaleString() : '—'}
                    </td>
                    <td className="px-3 py-2 text-slate-700 dark:text-slate-200">
                      {job.downloadCount > 0 ? (
                        <span>
                          {job.downloadCount}×
                          {job.lastDownloadedAtUtc && (
                            <span className="text-xs text-slate-500 dark:text-slate-400">
                              {' '}
                              (naposledy {new Date(job.lastDownloadedAtUtc).toLocaleString()})
                            </span>
                          )}
                        </span>
                      ) : (
                        '—'
                      )}
                    </td>
                    <td className="px-3 py-2 text-slate-700 dark:text-slate-200">{job.fileName}</td>
                    <td className="px-3 py-2 text-slate-700 dark:text-slate-200">
                      {job.artifactPath && job.status === 'Completed' && !expired ? (
                        <a
                          href={buildExportDownloadUrl(job.id)}
                          className="text-indigo-600 hover:underline dark:text-indigo-300"
                        >
                          Stáhnout
                        </a>
                      ) : job.status === 'Failed' ? (
                        <span className="text-red-600 dark:text-red-300">Selhalo</span>
                      ) : expired ? (
                        <span className="text-slate-500">Expirováno</span>
                      ) : (
                        <span className="text-slate-500">—</span>
                      )}
                    </td>
                  </tr>
                );
              })}

              {exportHistory.length === 0 && !exportLoading && (
                <tr>
                  <td className="px-3 py-6 text-center text-slate-500 dark:text-slate-400" colSpan={9}>
                    Zatím nejsou dostupné žádné exporty.
                  </td>
                </tr>
              )}

              {exportLoading && (
                <tr>
                  <td className="px-3 py-6 text-center text-slate-500 dark:text-slate-400" colSpan={9}>
                    Načítání…
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Nový import</h3>
        {importError && (
          <div className="mb-3 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-800">{importError}</div>
        )}
        {importFeedback && (
          <div className="mb-3 rounded border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">
            {importFeedback}
          </div>
        )}
        <form className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-2" onSubmit={handleImportSubmit}>
          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200">
            Rozsah
            <select
              className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
              value={importForm.scope}
              onChange={(event) => setImportForm((prev) => ({ ...prev, scope: event.target.value as QueueImportPayload['scope'] }))}
            >
              {scopes.map((scope) => (
                <option key={scope.value} value={scope.value}>
                  {scope.label}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200">
            Formát
            <select
              className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
              value={importForm.format}
              onChange={(event) => setImportForm((prev) => ({ ...prev, format: event.target.value as QueueImportPayload['format'] }))}
            >
              {importFormats.map((format) => (
                <option key={format.value} value={format.value}>
                  {format.label}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200">
            Strategie konfliktů
            <select
              className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
              value={importForm.conflictStrategy}
              onChange={(event) =>
                setImportForm((prev) => ({ ...prev, conflictStrategy: event.target.value as QueueImportPayload['conflictStrategy'] }))
              }
            >
              {conflictStrategies.map((strategy) => (
                <option key={strategy.value} value={strategy.value}>
                  {strategy.label}
                </option>
              ))}
            </select>
          </label>

          <label className="flex items-center gap-2 text-sm font-medium text-slate-700 dark:text-slate-200">
            <input
              type="checkbox"
              checked={importForm.dryRun}
              onChange={(event) => setImportForm((prev) => ({ ...prev, dryRun: event.target.checked }))}
            />
            Pouze dry-run (bez zápisu)
          </label>

          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">
            Cílová složka
            <input
              className="rounded border border-slate-300 p-2 dark:border-slate-700 dark:bg-slate-800"
              value={importForm.storagePath}
              onChange={(event) => setImportForm((prev) => ({ ...prev, storagePath: event.target.value }))}
              required
              placeholder={'např. C\\\Imports'}
            />
          </label>

          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200">
            Název souboru
            <input
              className="rounded border border-slate-300 p-2 dark:border-slate-700 dark:bg-slate-800"
              value={importForm.fileName}
              onChange={(event) => setImportForm((prev) => ({ ...prev, fileName: event.target.value }))}
              placeholder="např. import.csv"
            />
          </label>

          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">
            Soubor
            <input
              type="file"
              accept=".csv,.xlsx"
              onChange={(event) => {
                const file = event.target.files?.[0] ?? null;
                setImportFile(file);
                if (file) {
                  setImportForm((prev) => ({ ...prev, fileName: prev.fileName || file.name }));
                }
              }}
              required
              className="rounded border border-dashed border-slate-300 p-2 dark:border-slate-700"
            />
          </label>

          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">
            Mapování sloupců (JSON)
            <textarea
              className="rounded border border-slate-300 p-2 dark:border-slate-700 dark:bg-slate-800"
              value={importForm.mappingJson}
              onChange={(event) => setImportForm((prev) => ({ ...prev, mappingJson: event.target.value }))}
              placeholder={'{"Name":"column_name"}'}
              rows={3}
            />
          </label>

          <label className="flex items-center gap-2 text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">
            <input
              type="checkbox"
              checked={importForm.sendEmail}
              onChange={(event) => setImportForm((prev) => ({ ...prev, sendEmail: event.target.checked }))}
            />
            Odeslat souhrn e-mailem
          </label>

          {importForm.sendEmail && (
            <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">
              Příjemci (oddělte středníkem)
              <input
                className="rounded border border-slate-300 p-2 dark:border-slate-700 dark:bg-slate-800"
                value={importForm.emailRecipients}
                onChange={(event) => setImportForm((prev) => ({ ...prev, emailRecipients: event.target.value }))}
              />
            </label>
          )}

          <div className="md:col-span-2">
            <button
              type="submit"
              className="rounded bg-emerald-600 px-4 py-2 text-sm font-semibold text-white shadow hover:bg-emerald-700"
              disabled={importLoading}
            >
              Spustit import
            </button>
          </div>
        </form>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Historie importů</h3>
          <button
            className="text-sm text-indigo-600 hover:underline dark:text-indigo-300"
            type="button"
            onClick={loadImportHistory}
          >
            Obnovit
          </button>
        </div>

        <div className="mt-4 overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-left text-sm dark:divide-slate-700">
            <thead className="bg-slate-50 dark:bg-slate-800">
              <tr>
                <th className="px-3 py-2 font-medium text-slate-600 dark:text-slate-300">Rozsah</th>
                <th className="px-3 py-2 font-medium text-slate-600 dark:text-slate-300">Formát</th>
                <th className="px-3 py-2 font-medium text-slate-600 dark:text-slate-300">Stav</th>
                <th className="px-3 py-2 font-medium text-slate-600 dark:text-slate-300">Dry-run</th>
                <th className="px-3 py-2 font-medium text-slate-600 dark:text-slate-300">Zpracováno</th>
                <th className="px-3 py-2 font-medium text-slate-600 dark:text-slate-300">Soubor</th>
                <th className="px-3 py-2 font-medium text-slate-600 dark:text-slate-300">Log</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 dark:divide-slate-700">
              {importHistory.map((job) => (
                <tr key={job.id} className="hover:bg-slate-50 dark:hover:bg-slate-800">
                  <td className="px-3 py-2 text-slate-700 dark:text-slate-200">{job.scope}</td>
                  <td className="px-3 py-2 text-slate-700 dark:text-slate-200">{job.format}</td>
                  <td className="px-3 py-2 text-slate-700 dark:text-slate-200">{job.status}</td>
                  <td className="px-3 py-2 text-slate-700 dark:text-slate-200">{job.dryRun ? 'Ano' : 'Ne'}</td>
                  <td className="px-3 py-2 text-slate-700 dark:text-slate-200">{job.processedRows ?? '—'}</td>
                  <td className="px-3 py-2 text-slate-700 dark:text-slate-200">{job.originalFileName}</td>
                  <td className="px-3 py-2 text-slate-700 dark:text-slate-200">
                    {job.resultLog ? (
                      <a
                        href={buildImportLogUrl(job.id)}
                        className="text-indigo-600 hover:underline dark:text-indigo-300"
                      >
                        Zobrazit log
                      </a>
                    ) : job.status === 'Failed' ? (
                      <span className="text-red-600 dark:text-red-300">Selhalo</span>
                    ) : (
                      <span className="text-slate-500">—</span>
                    )}
                  </td>
                </tr>
              ))}

              {importHistory.length === 0 && !importLoading && (
                <tr>
                  <td className="px-3 py-6 text-center text-slate-500 dark:text-slate-400" colSpan={7}>
                    Zatím nejsou dostupné žádné importy.
                  </td>
                </tr>
              )}

              {importLoading && (
                <tr>
                  <td className="px-3 py-6 text-center text-slate-500 dark:text-slate-400" colSpan={7}>
                    Načítání…
                  </td>
                </tr>
              )}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
};
