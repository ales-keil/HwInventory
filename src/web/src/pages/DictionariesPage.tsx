import { FormEvent, useEffect, useState } from 'react';
import {
  DictionaryEntry,
  DictionaryEntryRequest,
  createDictionaryEntry,
  deleteDictionaryEntry,
  getDictionaryEntries,
  updateDictionaryEntry
} from '../api/dictionaries';

const supportedTypes = [
  'Environment',
  'WsusPriority',
  'OperatingSystem',
  'ServerRole',
  'DeviceType',
  'Location',
  'WorkstationType',
  'Vlan'
];

const createEmptyForm = (dictType: string): DictionaryEntryRequest => ({
  dictType,
  key: '',
  value: '',
  description: '',
  order: 0
});

export const DictionariesPage = () => {
  const [selectedType, setSelectedType] = useState(supportedTypes[0]);
  const [entries, setEntries] = useState<DictionaryEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState(createEmptyForm(selectedType));
  const [editingId, setEditingId] = useState<string | null>(null);

  const loadEntries = async (type: string) => {
    try {
      setLoading(true);
      const items = await getDictionaryEntries(type);
      setEntries(items);
      setError(null);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadEntries(selectedType);
    setForm(createEmptyForm(selectedType));
    setEditingId(null);
  }, [selectedType]);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    try {
      if (editingId) {
        await updateDictionaryEntry(editingId, form);
      } else {
        await createDictionaryEntry(form);
      }
      setForm(createEmptyForm(selectedType));
      setEditingId(null);
      await loadEntries(selectedType);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleEdit = (entry: DictionaryEntry) => {
    setEditingId(entry.id);
    setForm({
      dictType: entry.dictType,
      key: entry.key,
      value: entry.value,
      description: entry.description ?? '',
      order: entry.order
    });
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Opravdu chcete záznam smazat?')) {
      return;
    }
    try {
      await deleteDictionaryEntry(id);
      await loadEntries(selectedType);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-2 md:flex-row md:items-end md:justify-between">
        <div>
          <h2 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">Číselníky</h2>
          <p className="text-sm text-slate-600 dark:text-slate-400">
            Správa referenčních dat používaných v celém systému.
          </p>
        </div>
        <select
          value={selectedType}
          onChange={(event) => setSelectedType(event.target.value)}
          className="rounded border border-slate-300 px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-800"
        >
          {supportedTypes.map((type) => (
            <option key={type} value={type}>
              {type}
            </option>
          ))}
        </select>
      </div>

      {error && <div className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-700">{error}</div>}

      <div className="overflow-x-auto rounded border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <table className="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-700">
          <thead className="bg-slate-100 text-left font-semibold uppercase tracking-wide text-slate-600 dark:bg-slate-800 dark:text-slate-300">
            <tr>
              <th className="px-4 py-3">Pořadí</th>
              <th className="px-4 py-3">Klíč</th>
              <th className="px-4 py-3">Hodnota</th>
              <th className="px-4 py-3">Popis</th>
              <th className="px-4 py-3 text-right">Akce</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
            {entries.map((entry) => (
              <tr key={entry.id} className="hover:bg-slate-50 dark:hover:bg-slate-800">
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{entry.order}</td>
                <td className="px-4 py-3 font-medium text-slate-900 dark:text-slate-100">{entry.key}</td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{entry.value}</td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{entry.description ?? '—'}</td>
                <td className="flex items-center justify-end gap-2 px-4 py-3">
                  <button
                    className="rounded border border-slate-200 px-3 py-1 text-xs font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                    onClick={() => handleEdit(entry)}
                  >
                    Upravit
                  </button>
                  <button
                    className="rounded border border-red-300 px-3 py-1 text-xs font-semibold text-red-600 transition hover:bg-red-50 dark:border-red-700 dark:text-red-400 dark:hover:bg-red-900/40"
                    onClick={() => handleDelete(entry.id)}
                  >
                    Smazat
                  </button>
                </td>
              </tr>
            ))}
            {!loading && entries.length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-sm text-slate-500 dark:text-slate-400">
                  Pro tento typ nejsou k dispozici žádné položky.
                </td>
              </tr>
            )}
            {loading && (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-sm text-slate-500 dark:text-slate-400">
                  Načítám data…
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4 rounded border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">
            {editingId ? 'Upravit položku' : 'Nová položka'} ({selectedType})
          </h3>
          {editingId && (
            <button type="button" onClick={() => { setEditingId(null); setForm(createEmptyForm(selectedType)); }} className="text-sm text-slate-500 underline">
              Zrušit úpravy
            </button>
          )}
        </div>

        <div className="grid gap-4 md:grid-cols-2">
          <label className="flex flex-col gap-1 text-sm">
            <span>Klíč</span>
            <input
              required
              value={form.key}
              onChange={(event) => setForm((state) => ({ ...state, key: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>Hodnota</span>
            <input
              required
              value={form.value}
              onChange={(event) => setForm((state) => ({ ...state, value: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm md:col-span-2">
            <span>Popis</span>
            <textarea
              value={form.description ?? ''}
              onChange={(event) => setForm((state) => ({ ...state, description: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
              rows={3}
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>Pořadí</span>
            <input
              type="number"
              value={form.order}
              onChange={(event) => setForm((state) => ({ ...state, order: Number(event.target.value) }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>
        </div>

        <div className="flex justify-end gap-3">
          <button
            type="button"
            onClick={() => { setEditingId(null); setForm(createEmptyForm(selectedType)); }}
            className="rounded border border-slate-200 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
          >
            Zrušit
          </button>
          <button
            type="submit"
            className="rounded bg-teal-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-teal-500"
          >
            {editingId ? 'Uložit změny' : 'Přidat položku'}
          </button>
        </div>
      </form>
    </div>
  );
};
