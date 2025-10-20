import { useCallback, useEffect, useMemo, useState } from 'react';
import {
  ReportDefinition,
  ReportRun,
  createReport,
  deleteReport,
  listReportRuns,
  listReports,
  triggerReport,
  updateReport,
  buildReportDownloadUrl
} from '../api/reports';
import { ReportFormat, ReportRecurrence, ReportScope } from '../api/reports.types';

interface ReportFormState {
  id?: string;
  name: string;
  description?: string;
  scope: ReportScope;
  format: ReportFormat;
  recurrence: ReportRecurrence;
  filterJson?: string;
  recipients?: string;
  storagePath: string;
  emailSubjectTemplate: string;
  emailBodyTemplate: string;
  notifyOnFailureOnly: boolean;
  includeArtifactInEmail: boolean;
  runAtTime?: string;
  runOnDayOfWeek?: number;
  runOnDayOfMonth?: number;
  enabled: boolean;
}

const defaultStoragePath = 'C:/inetpub/wwwroot/hwinventory/reports';
const defaultSubjectTemplate = 'HW Inventory – report {ReportName}';
const defaultBodyTemplate = "Report '{ReportName}' ({Scope}) byl {StatusText} v {CompletedAt}.\n\nSoubor: {ArtifactPath}\nDetaily: {ReportUrl}";

const defaultForm: ReportFormState = {
  name: '',
  description: '',
  scope: ReportScope.Servers,
  format: ReportFormat.Csv,
  recurrence: ReportRecurrence.Manual,
  filterJson: '',
  recipients: '',
  storagePath: defaultStoragePath,
  emailSubjectTemplate: defaultSubjectTemplate,
  emailBodyTemplate: defaultBodyTemplate,
  notifyOnFailureOnly: false,
  includeArtifactInEmail: false,
  runAtTime: '06:00:00',
  runOnDayOfWeek: 1,
  runOnDayOfMonth: 1,
  enabled: true
};

const formatOptions = Object.values(ReportFormat);
const scopeOptions = Object.values(ReportScope);
const recurrenceOptions = Object.values(ReportRecurrence);

export const ReportsPage = () => {
  const [reports, setReports] = useState<ReportDefinition[]>([]);
  const [runs, setRuns] = useState<ReportRun[]>([]);
  const [form, setForm] = useState<ReportFormState>(defaultForm);
  const [selectedReport, setSelectedReport] = useState<ReportDefinition | null>(null);
  const [loading, setLoading] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  const loadReports = useCallback(async () => {
    const data = await listReports();
    setReports(data);
    if (selectedReport) {
      const refreshed = data.find((r) => r.id === selectedReport.id);
      if (refreshed) {
        setSelectedReport(refreshed);
      }
    }
  }, [selectedReport]);

  const loadRuns = useCallback(
    async (reportId: string) => {
      const data = await listReportRuns(reportId);
      setRuns(data);
    },
    []
  );

  useEffect(() => {
    loadReports().catch((error) => console.error('Failed to load reports', error));
  }, [loadReports]);

  useEffect(() => {
    if (selectedReport) {
      loadRuns(selectedReport.id).catch((error) => console.error('Failed to load runs', error));
    } else {
      setRuns([]);
    }
  }, [selectedReport, loadRuns]);

  const handleInputChange = (event: React.ChangeEvent<HTMLInputElement | HTMLSelectElement | HTMLTextAreaElement>) => {
    const { name, value, type, checked } = event.target;
    setForm((prev) => ({
      ...prev,
      [name]: type === 'checkbox' ? checked : value
    }));
  };

  const resetForm = () => {
    setForm(defaultForm);
    setSelectedReport(null);
    setRuns([]);
  };

  const handleEdit = (report: ReportDefinition) => {
    setSelectedReport(report);
    setForm({
      id: report.id,
      name: report.name,
      description: report.description ?? '',
      scope: report.scope,
      format: report.format,
      recurrence: report.recurrence,
      filterJson: report.filterJson ?? '',
      recipients: report.recipients ?? '',
      storagePath: report.storagePath,
      emailSubjectTemplate: report.emailSubjectTemplate ?? defaultSubjectTemplate,
      emailBodyTemplate: report.emailBodyTemplate ?? defaultBodyTemplate,
      notifyOnFailureOnly: report.notifyOnFailureOnly,
      includeArtifactInEmail: report.includeArtifactInEmail,
      runAtTime: report.runAtTime ?? '06:00:00',
      runOnDayOfWeek: report.runOnDayOfWeek ?? 1,
      runOnDayOfMonth: report.runOnDayOfMonth ?? 1,
      enabled: report.enabled
    });
  };

  const parseTime = (value?: string) => {
    if (!value) {
      return undefined;
    }
    return value;
  };

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setLoading(true);
    setMessage(null);
    try {
      const payload = {
        name: form.name,
        description: form.description,
        scope: form.scope,
        format: form.format,
        recurrence: form.recurrence,
        filterJson: form.filterJson,
        recipients: form.recipients,
        storagePath: form.storagePath,
        emailSubjectTemplate: form.emailSubjectTemplate,
        emailBodyTemplate: form.emailBodyTemplate,
        notifyOnFailureOnly: form.notifyOnFailureOnly,
        includeArtifactInEmail: form.includeArtifactInEmail,
        runAtTime: parseTime(form.runAtTime),
        runOnDayOfWeek: form.runOnDayOfWeek,
        runOnDayOfMonth: form.runOnDayOfMonth,
        enabled: form.enabled
      };

      if (form.id) {
        await updateReport(form.id, payload);
        setMessage('Report byl aktualizován.');
      } else {
        await createReport(payload);
        setMessage('Report byl vytvořen.');
      }
      await loadReports();
      resetForm();
    } catch (error) {
      console.error(error);
      setMessage('Operace selhala – zkontrolujte vstupy a zkuste to znovu.');
    } finally {
      setLoading(false);
    }
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Opravdu chcete report odstranit?')) {
      return;
    }
    setLoading(true);
    try {
      await deleteReport(id);
      setMessage('Report byl smazán.');
      await loadReports();
      if (selectedReport?.id === id) {
        resetForm();
      }
    } catch (error) {
      console.error(error);
      setMessage('Smazání reportu selhalo.');
    } finally {
      setLoading(false);
    }
  };

  const handleRunNow = async (id: string) => {
    setLoading(true);
    try {
      await triggerReport(id);
      setMessage('Report byl přidán do fronty.');
      if (selectedReport?.id === id) {
        await loadRuns(id);
      }
    } catch (error) {
      console.error(error);
      setMessage('Spuštění reportu selhalo.');
    } finally {
      setLoading(false);
    }
  };

  const sortedRuns = useMemo(() => runs.slice().sort((a, b) => b.startedAtUtc.localeCompare(a.startedAtUtc)), [runs]);

  return (
    <div className="space-y-10">
      <header>
        <h1 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">Reporty &amp; Plánování</h1>
        <p className="text-sm text-slate-600 dark:text-slate-400">
          Definujte pravidelné výstupy inventáře, sledujte historii běhů a stahujte generované soubory.
        </p>
      </header>

      {message && <div className="rounded border border-slate-200 bg-slate-50 p-3 text-sm text-slate-700 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200">{message}</div>}

      <section className="grid gap-8 lg:grid-cols-2">
        <form onSubmit={handleSubmit} className="space-y-4 rounded border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
          <h2 className="text-lg font-semibold text-slate-900 dark:text-slate-100">{form.id ? 'Upravit report' : 'Nový report'}</h2>
          <div>
            <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Název</label>
            <input
              name="name"
              value={form.name}
              onChange={handleInputChange}
              className="mt-1 w-full rounded border border-slate-300 p-2 text-sm dark:border-slate-700 dark:bg-slate-800"
              required
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Popis</label>
            <textarea
              name="description"
              value={form.description}
              onChange={handleInputChange}
              rows={3}
              className="mt-1 w-full rounded border border-slate-300 p-2 text-sm dark:border-slate-700 dark:bg-slate-800"
            />
          </div>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-2">
            <div>
              <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Scope</label>
              <select
                name="scope"
                value={form.scope}
                onChange={handleInputChange}
                className="mt-1 w-full rounded border border-slate-300 p-2 text-sm dark:border-slate-700 dark:bg-slate-800"
              >
                {scopeOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Formát</label>
              <select
                name="format"
                value={form.format}
                onChange={handleInputChange}
                className="mt-1 w-full rounded border border-slate-300 p-2 text-sm dark:border-slate-700 dark:bg-slate-800"
              >
                {formatOptions.map((option) => (
                  <option key={option} value={option}>
                    {option}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Recurrence</label>
            <select
              name="recurrence"
              value={form.recurrence}
              onChange={handleInputChange}
              className="mt-1 w-full rounded border border-slate-300 p-2 text-sm dark:border-slate-700 dark:bg-slate-800"
            >
              {recurrenceOptions.map((option) => (
                <option key={option} value={option}>
                  {option}
                </option>
              ))}
            </select>
          </div>
          <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
            <div>
              <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Čas běhu (UTC)</label>
              <input
                type="time"
                name="runAtTime"
                value={form.runAtTime}
                onChange={handleInputChange}
                className="mt-1 w-full rounded border border-slate-300 p-2 text-sm dark:border-slate-700 dark:bg-slate-800"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Den v týdnu</label>
              <input
                type="number"
                min={0}
                max={6}
                name="runOnDayOfWeek"
                value={form.runOnDayOfWeek}
                onChange={handleInputChange}
                className="mt-1 w-full rounded border border-slate-300 p-2 text-sm dark:border-slate-700 dark:bg-slate-800"
              />
            </div>
            <div>
              <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Den v měsíci</label>
              <input
                type="number"
                min={1}
                max={28}
                name="runOnDayOfMonth"
                value={form.runOnDayOfMonth}
                onChange={handleInputChange}
                className="mt-1 w-full rounded border border-slate-300 p-2 text-sm dark:border-slate-700 dark:bg-slate-800"
              />
            </div>
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Cílová složka</label>
            <input
              name="storagePath"
              value={form.storagePath}
              onChange={handleInputChange}
              className="mt-1 w-full rounded border border-slate-300 p-2 text-sm dark:border-slate-700 dark:bg-slate-800"
              required
            />
            <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">Absolutní cesta, kam budou ukládány generované soubory.</p>
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Příjemci e-mailu</label>
            <input
              name="recipients"
              value={form.recipients}
              onChange={handleInputChange}
              placeholder="user@example.com;helpdesk@example.com"
              className="mt-1 w-full rounded border border-slate-300 p-2 text-sm dark:border-slate-700 dark:bg-slate-800"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Předmět e-mailu</label>
            <input
              name="emailSubjectTemplate"
              value={form.emailSubjectTemplate}
              onChange={handleInputChange}
              className="mt-1 w-full rounded border border-slate-300 p-2 text-sm dark:border-slate-700 dark:bg-slate-800"
            />
            <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
              Dostupné proměnné: {'{ReportName}'}, {'{Scope}'}, {'{StatusText}'}, {'{CompletedAt}'}, {'{ArtifactPath}'}, {'{ReportUrl}' }.
            </p>
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Tělo e-mailu</label>
            <textarea
              name="emailBodyTemplate"
              value={form.emailBodyTemplate}
              onChange={handleInputChange}
              rows={5}
              className="mt-1 w-full rounded border border-slate-300 p-2 text-xs font-mono dark:border-slate-700 dark:bg-slate-800"
            />
            <p className="mt-1 text-xs text-slate-500 dark:text-slate-400">
              Nahrazeny budou stejné proměnné jako v předmětu, navíc {'{FailureReason}'} pro selhání.
            </p>
          </div>
          <div className="flex flex-col gap-2">
            <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-300">
              <input
                type="checkbox"
                name="notifyOnFailureOnly"
                checked={form.notifyOnFailureOnly}
                onChange={handleInputChange}
                className="h-4 w-4"
              />
              Posílat notifikace pouze při selhání reportu
            </label>
            <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-300">
              <input
                type="checkbox"
                name="includeArtifactInEmail"
                checked={form.includeArtifactInEmail}
                onChange={handleInputChange}
                className="h-4 w-4"
              />
              Přiložit vygenerovaný soubor k e-mailu (pokud existuje)
            </label>
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 dark:text-slate-300">Filtr (JSON)</label>
            <textarea
              name="filterJson"
              value={form.filterJson}
              onChange={handleInputChange}
              rows={3}
              className="mt-1 w-full rounded border border-slate-300 p-2 text-xs font-mono dark:border-slate-700 dark:bg-slate-800"
              placeholder='{"status":"Active"}'
            />
          </div>
          <div className="flex items-center gap-2">
            <input
              type="checkbox"
              name="enabled"
              checked={form.enabled}
              onChange={handleInputChange}
              className="h-4 w-4"
            />
            <span className="text-sm text-slate-700 dark:text-slate-300">Report aktivní</span>
          </div>
          <div className="flex items-center gap-3">
            <button
              type="submit"
              disabled={loading}
              className="rounded bg-slate-900 px-4 py-2 text-sm font-medium text-white shadow-sm hover:bg-slate-700 disabled:opacity-60 dark:bg-slate-700 dark:hover:bg-slate-600"
            >
              {form.id ? 'Uložit změny' : 'Vytvořit report'}
            </button>
            {form.id && (
              <button
                type="button"
                onClick={resetForm}
                className="rounded border border-slate-300 px-4 py-2 text-sm text-slate-700 hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
              >
                Zrušit úpravy
              </button>
            )}
          </div>
        </form>

        <div className="space-y-6">
          <div className="rounded border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Reporty</h2>
              <span className="text-xs text-slate-500 dark:text-slate-400">{reports.length} definic</span>
            </div>
            <div className="mt-4 overflow-x-auto">
              <table className="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-700">
                <thead className="bg-slate-50 dark:bg-slate-800">
                  <tr>
                    <th className="px-3 py-2 text-left font-medium text-slate-600 dark:text-slate-300">Název</th>
                    <th className="px-3 py-2 text-left font-medium text-slate-600 dark:text-slate-300">Scope</th>
                    <th className="px-3 py-2 text-left font-medium text-slate-600 dark:text-slate-300">Recurrence</th>
                    <th className="px-3 py-2 text-left font-medium text-slate-600 dark:text-slate-300">Další běh</th>
                    <th className="px-3 py-2 text-right font-medium text-slate-600 dark:text-slate-300">Akce</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
                  {reports.map((report) => (
                    <tr key={report.id} className={selectedReport?.id === report.id ? 'bg-slate-100 dark:bg-slate-800' : ''}>
                      <td className="px-3 py-2">
                        <button
                          type="button"
                          onClick={() => handleEdit(report)}
                          className="text-left text-slate-900 hover:underline dark:text-slate-100"
                        >
                          {report.name}
                        </button>
                      </td>
                      <td className="px-3 py-2 text-slate-600 dark:text-slate-300">{report.scope}</td>
                      <td className="px-3 py-2 text-slate-600 dark:text-slate-300">{report.recurrence}</td>
                      <td className="px-3 py-2 text-slate-600 dark:text-slate-300">{report.nextRunAtUtc ?? '—'}</td>
                      <td className="px-3 py-2 text-right text-sm">
                        <div className="flex justify-end gap-2">
                          <button
                            type="button"
                            onClick={() => handleRunNow(report.id)}
                            className="rounded border border-slate-300 px-2 py-1 text-xs text-slate-700 hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                          >
                            Spustit
                          </button>
                          <button
                            type="button"
                            onClick={() => handleDelete(report.id)}
                            className="rounded border border-red-300 px-2 py-1 text-xs text-red-600 hover:bg-red-50 dark:border-red-600 dark:text-red-400 dark:hover:bg-red-900/40"
                          >
                            Smazat
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                  {reports.length === 0 && (
                    <tr>
                      <td colSpan={5} className="px-3 py-6 text-center text-sm text-slate-500 dark:text-slate-400">
                        Zatím nebyly vytvořeny žádné reporty.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>

          <div className="rounded border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
            <div className="flex items-center justify-between">
              <h2 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Historie běhů</h2>
              {selectedReport && <span className="text-xs text-slate-500 dark:text-slate-400">{selectedReport.name}</span>}
            </div>
            <div className="mt-4 overflow-x-auto">
              <table className="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-800">
                <thead className="bg-slate-50 dark:bg-slate-800">
                  <tr>
                    <th className="px-3 py-2 text-left font-medium text-slate-600 dark:text-slate-300">Start</th>
                    <th className="px-3 py-2 text-left font-medium text-slate-600 dark:text-slate-300">Dokončeno</th>
                    <th className="px-3 py-2 text-left font-medium text-slate-600 dark:text-slate-300">Stav</th>
                    <th className="px-3 py-2 text-left font-medium text-slate-600 dark:text-slate-300">Artefakt</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
                  {sortedRuns.map((run) => (
                    <tr key={run.id}>
                      <td className="px-3 py-2 text-slate-600 dark:text-slate-300">{run.startedAtUtc}</td>
                      <td className="px-3 py-2 text-slate-600 dark:text-slate-300">{run.completedAtUtc ?? '—'}</td>
                      <td className="px-3 py-2 text-slate-600 dark:text-slate-300">{run.status}</td>
                      <td className="px-3 py-2 text-slate-600 dark:text-slate-300">
                        {run.status === 'Completed' && (
                          <a
                            href={buildReportDownloadUrl(run.id)}
                            className="text-slate-900 underline hover:text-slate-600 dark:text-slate-100"
                            target="_blank"
                            rel="noreferrer"
                          >
                            Stáhnout
                          </a>
                        )}
                        {run.status !== 'Completed' && <span>—</span>}
                      </td>
                    </tr>
                  ))}
                  {sortedRuns.length === 0 && (
                    <tr>
                      <td colSpan={4} className="px-3 py-6 text-center text-sm text-slate-500 dark:text-slate-400">
                        Vyberte report pro zobrazení historie běhů.
                      </td>
                    </tr>
                  )}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
};
