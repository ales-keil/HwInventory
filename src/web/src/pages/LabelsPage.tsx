import { FormEvent, useEffect, useState } from 'react';
import {
  LabelPrintJob,
  LabelTemplate,
  createLabelTemplate,
  deleteLabelTemplate,
  getLabelPrintJobs,
  getLabelTemplates,
  renderLabelPdf,
  renderLabelZpl,
  updateLabelTemplate
} from '../api/labels';

const createEmptyTemplate = (): LabelTemplate => ({
  id: '',
  name: '',
  code: '',
  format: 'A7',
  payload: '{\n  "elements": []\n}',
  description: ''
});

export const LabelsPage = () => {
  const [templates, setTemplates] = useState<LabelTemplate[]>([]);
  const [jobs, setJobs] = useState<LabelPrintJob[]>([]);
  const [form, setForm] = useState(createEmptyTemplate());
  const [editingId, setEditingId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [previewZpl, setPreviewZpl] = useState('');

  const loadTemplates = async () => {
    try {
      setLoading(true);
      const [templateData, jobsData] = await Promise.all([getLabelTemplates(), getLabelPrintJobs()]);
      setTemplates(templateData);
      setJobs(jobsData);
      setError(null);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadTemplates();
  }, []);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    try {
      if (editingId) {
        await updateLabelTemplate(editingId, { ...form, id: editingId });
      } else {
        await createLabelTemplate({ ...form, id: crypto.randomUUID() });
      }
      setForm(createEmptyTemplate());
      setEditingId(null);
      await loadTemplates();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleEdit = (template: LabelTemplate) => {
    setEditingId(template.id);
    setForm(template);
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Opravdu chcete šablonu smazat?')) {
      return;
    }
    try {
      await deleteLabelTemplate(id);
      await loadTemplates();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleRenderZpl = async (id: string) => {
    try {
      const zpl = await renderLabelZpl(id, {});
      setPreviewZpl(zpl);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleRenderPdf = async (id: string) => {
    try {
      const pdfBlob = await renderLabelPdf(id, {});
      const url = URL.createObjectURL(pdfBlob);
      window.open(url, '_blank');
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">Štítky a tisk</h2>
        <p className="text-sm text-slate-600 dark:text-slate-400">
          Správa šablon, generování náhledů a kontrola tiskových úloh.
        </p>
      </div>

      {error && <div className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-700">{error}</div>}

      <div className="grid gap-6 md:grid-cols-2">
        <div className="space-y-4">
          <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Šablony</h3>
          <div className="rounded border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-900">
            <ul className="divide-y divide-slate-200 text-sm dark:divide-slate-800">
              {templates.map((template) => (
                <li key={template.id} className="flex items-center justify-between gap-3 px-4 py-3">
                  <div>
                    <p className="font-medium text-slate-900 dark:text-slate-100">{template.name}</p>
                    <p className="text-xs text-slate-500 dark:text-slate-400">{template.code} · {template.format}</p>
                  </div>
                  <div className="flex gap-2 text-xs">
                    <button
                      className="rounded border border-slate-200 px-3 py-1 font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                      onClick={() => handleEdit(template)}
                    >
                      Upravit
                    </button>
                    <button
                      className="rounded border border-slate-200 px-3 py-1 font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                      onClick={() => handleRenderZpl(template.id)}
                    >
                      Náhled ZPL
                    </button>
                    <button
                      className="rounded border border-slate-200 px-3 py-1 font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                      onClick={() => handleRenderPdf(template.id)}
                    >
                      PDF
                    </button>
                    <button
                      className="rounded border border-red-300 px-3 py-1 font-semibold text-red-600 transition hover:bg-red-50 dark:border-red-700 dark:text-red-400 dark:hover:bg-red-900/40"
                      onClick={() => handleDelete(template.id)}
                    >
                      Smazat
                    </button>
                  </div>
                </li>
              ))}
              {!loading && templates.length === 0 && (
                <li className="px-4 py-6 text-sm text-slate-500 dark:text-slate-400">Nejsou definované žádné šablony.</li>
              )}
              {loading && (
                <li className="px-4 py-6 text-sm text-slate-500 dark:text-slate-400">Načítám data…</li>
              )}
            </ul>
          </div>

          <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Tiskové úlohy</h3>
          <div className="rounded border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-900">
            <ul className="divide-y divide-slate-200 text-sm dark:divide-slate-800">
              {jobs.map((job) => (
                <li key={job.id} className="flex flex-col gap-1 px-4 py-3">
                  <div className="flex items-center justify-between">
                    <span className="font-medium text-slate-900 dark:text-slate-100">{job.target}</span>
                    <span className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">{job.jobStatus}</span>
                  </div>
                  <span className="text-xs text-slate-500 dark:text-slate-400">
                    {new Date(job.createdAtUtc).toLocaleString()} · {job.format}
                  </span>
                </li>
              ))}
              {!loading && jobs.length === 0 && (
                <li className="px-4 py-6 text-sm text-slate-500 dark:text-slate-400">Zatím nebyly vytvořeny žádné tiskové úlohy.</li>
              )}
            </ul>
          </div>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4 rounded border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-700 dark:bg-slate-900">
          <div className="flex items-center justify-between">
            <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">
              {editingId ? 'Upravit šablonu' : 'Nová šablona'}
            </h3>
            {editingId && (
              <button
                type="button"
                onClick={() => { setEditingId(null); setForm(createEmptyTemplate()); }}
                className="text-sm text-slate-500 underline"
              >
                Zrušit úpravy
              </button>
            )}
          </div>

          <label className="flex flex-col gap-1 text-sm">
            <span>Název</span>
            <input
              required
              value={form.name}
              onChange={(event) => setForm((state) => ({ ...state, name: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>Kód</span>
            <input
              required
              value={form.code}
              onChange={(event) => setForm((state) => ({ ...state, code: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>Formát / rozměr</span>
            <input
              value={form.format}
              onChange={(event) => setForm((state) => ({ ...state, format: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>Popis</span>
            <textarea
              value={form.description ?? ''}
              onChange={(event) => setForm((state) => ({ ...state, description: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
              rows={3}
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>Payload (JSON)</span>
            <textarea
              required
              value={form.payload}
              onChange={(event) => setForm((state) => ({ ...state, payload: event.target.value }))}
              className="h-48 rounded border border-slate-300 px-3 py-2 font-mono text-xs dark:border-slate-700 dark:bg-slate-800"
            />
          </label>

          {previewZpl && (
            <div className="rounded border border-slate-200 bg-slate-50 p-3 text-xs font-mono text-slate-700 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200">
              <div className="mb-2 flex items-center justify-between">
                <strong>ZPL náhled</strong>
                <button type="button" className="text-xs text-slate-500 underline" onClick={() => setPreviewZpl('')}>
                  Zavřít
                </button>
              </div>
              <pre className="whitespace-pre-wrap break-all">{previewZpl}</pre>
            </div>
          )}

          <div className="flex justify-end gap-3">
            <button
              type="button"
              onClick={() => { setEditingId(null); setForm(createEmptyTemplate()); }}
              className="rounded border border-slate-200 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
            >
              Zrušit
            </button>
            <button
              type="submit"
              className="rounded bg-purple-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-purple-500"
            >
              {editingId ? 'Uložit změny' : 'Přidat šablonu'}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
};
