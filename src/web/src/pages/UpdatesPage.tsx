import { ChangeEvent, FormEvent, useEffect, useState } from 'react';
import { downloadUpdateLog, listUpdateHistory, UpdatePackageResponse, uploadUpdatePackage } from '../api/updates';

type FormState = {
  file?: File;
  version?: string;
  preserveDb: boolean;
  createBackup: boolean;
  backupPath?: string;
  integrityCheck: boolean;
  confirmedBackup: boolean;
  notes?: string;
};

const defaultState: FormState = {
  preserveDb: true,
  createBackup: true,
  integrityCheck: true,
  confirmedBackup: false
};

export function UpdatesPage() {
  const [history, setHistory] = useState<UpdatePackageResponse[]>([]);
  const [form, setForm] = useState<FormState>(defaultState);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | undefined>();
  const [success, setSuccess] = useState<string | undefined>();

  useEffect(() => {
    (async () => {
      try {
        const items = await listUpdateHistory();
        setHistory(items);
      } catch (err) {
        console.error(err);
        setError('Nepodařilo se načíst historii aktualizací.');
      }
    })();
  }, []);

  const handleFileChange = (event: ChangeEvent<HTMLInputElement>) => {
    if (event.target.files && event.target.files.length > 0) {
      setForm((prev) => ({ ...prev, file: event.target.files![0] }));
    }
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(undefined);
    setSuccess(undefined);

    if (!form.file) {
      setError('Vyberte soubor s aktualizací.');
      return;
    }

    if (!form.confirmedBackup && !form.createBackup) {
      setError('Potvrďte existenci zálohy nebo zapněte vytvoření zálohy před aktualizací.');
      return;
    }

    try {
      setLoading(true);
      const result = await uploadUpdatePackage({
        file: form.file,
        version: form.version,
        preserveDatabaseConfiguration: form.preserveDb,
        createBackupBeforeInstall: form.createBackup,
        backupStoragePath: form.backupPath,
        performIntegrityCheck: form.integrityCheck,
        confirmedBackupAvailable: form.confirmedBackup,
        notes: form.notes
      });

      setHistory((prev) => [result, ...prev]);
      setSuccess('Aktualizační balíček byl zařazen do fronty.');
      setForm(defaultState);
    } catch (err: any) {
      console.error(err);
      const detail = err?.response?.data?.detail ?? 'Nahrání selhalo.';
      setError(detail);
    } finally {
      setLoading(false);
    }
  };

  const downloadLog = async (item: UpdatePackageResponse) => {
    try {
      const blob = await downloadUpdateLog(item.id);
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = `update-${item.version || item.id}.log`;
      link.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      console.error(err);
      setError('Stažení logu selhalo.');
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-semibold text-slate-900 dark:text-slate-100">Aktualizace aplikace</h2>
        <p className="text-sm text-slate-600 dark:text-slate-400">
          Nahrajte balíček ZIP/PKG s novou verzí aplikace. Systém jej uloží, ověří hash a připraví staging adresář pro následné nasazení.
        </p>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4 rounded-md border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <div>
          <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">Soubor aktualizace</label>
          <input type="file" accept=".zip,.pkg" onChange={handleFileChange} className="mt-1 block w-full text-sm text-slate-700 dark:text-slate-200" required />
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <div>
            <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">Verze (volitelné)</label>
            <input
              type="text"
              value={form.version ?? ''}
              onChange={(e) => setForm((prev) => ({ ...prev, version: e.target.value }))}
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
              placeholder="v1.2.3"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">Cesta pro zálohu (volitelné)</label>
            <input
              type="text"
              value={form.backupPath ?? ''}
              onChange={(e) => setForm((prev) => ({ ...prev, backupPath: e.target.value }))}
              className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
              placeholder="C:\\Backups\\HWInventory"
            />
          </div>
        </div>
        <div>
          <label className="block text-sm font-medium text-slate-700 dark:text-slate-200">Poznámka</label>
          <textarea
            value={form.notes ?? ''}
            onChange={(e) => setForm((prev) => ({ ...prev, notes: e.target.value }))}
            rows={3}
            className="mt-1 w-full rounded-md border border-slate-300 px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-slate-500 focus:outline-none focus:ring-1 focus:ring-slate-500 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
            placeholder="Poznámky k vydání nebo postup aktualizace"
          />
        </div>
        <div className="grid gap-4 md:grid-cols-2">
          <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
            <input
              type="checkbox"
              checked={form.preserveDb}
              onChange={(e) => setForm((prev) => ({ ...prev, preserveDb: e.target.checked }))}
              className="h-4 w-4"
            />
            Zachovat stávající DB konfiguraci
          </label>
          <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
            <input
              type="checkbox"
              checked={form.integrityCheck}
              onChange={(e) => setForm((prev) => ({ ...prev, integrityCheck: e.target.checked }))}
              className="h-4 w-4"
            />
            Ověřit integritu balíčku (SHA256)
          </label>
          <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
            <input
              type="checkbox"
              checked={form.createBackup}
              onChange={(e) => setForm((prev) => ({ ...prev, createBackup: e.target.checked }))}
              className="h-4 w-4"
            />
            Vytvořit zálohu před nasazením
          </label>
          <label className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
            <input
              type="checkbox"
              checked={form.confirmedBackup}
              onChange={(e) => setForm((prev) => ({ ...prev, confirmedBackup: e.target.checked }))}
              className="h-4 w-4"
            />
            Potvrzuji, že mám aktuální zálohu
          </label>
        </div>
        {error && <div className="rounded-md border border-red-300 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-700 dark:bg-red-900/30 dark:text-red-200">{error}</div>}
        {success && <div className="rounded-md border border-green-300 bg-green-50 px-3 py-2 text-sm text-green-700 dark:border-green-700 dark:bg-green-900/30 dark:text-green-200">{success}</div>}
        <div className="flex justify-end gap-3">
          <button
            type="submit"
            className="rounded-md bg-slate-900 px-4 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-slate-700 disabled:cursor-not-allowed disabled:bg-slate-400 dark:bg-slate-200 dark:text-slate-900 dark:hover:bg-white"
            disabled={loading}
          >
            {loading ? 'Nahrávám…' : 'Nahrát aktualizaci'}
          </button>
        </div>
      </form>

      <section className="rounded-md border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Historie aktualizačních balíčků</h3>
        <p className="mt-1 text-sm text-slate-600 dark:text-slate-400">
          Přehled nahraných balíčků, včetně výsledku ověření a odkazu na log.
        </p>
        <div className="mt-4 overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-700">
            <thead className="bg-slate-50 dark:bg-slate-800">
              <tr>
                <th className="px-4 py-2 text-left font-medium text-slate-700 dark:text-slate-200">Verze</th>
                <th className="px-4 py-2 text-left font-medium text-slate-700 dark:text-slate-200">Soubor</th>
                <th className="px-4 py-2 text-left font-medium text-slate-700 dark:text-slate-200">Stav</th>
                <th className="px-4 py-2 text-left font-medium text-slate-700 dark:text-slate-200">Vytvořil</th>
                <th className="px-4 py-2 text-left font-medium text-slate-700 dark:text-slate-200">Čas</th>
                <th className="px-4 py-2 text-left font-medium text-slate-700 dark:text-slate-200">Akce</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
              {history.length === 0 && (
                <tr>
                  <td colSpan={6} className="px-4 py-6 text-center text-slate-500 dark:text-slate-400">
                    Zatím nebyly nahrány žádné aktualizační balíčky.
                  </td>
                </tr>
              )}
              {history.map((item) => (
                <tr key={item.id}>
                  <td className="px-4 py-2 text-slate-900 dark:text-slate-100">{item.version}</td>
                  <td className="px-4 py-2 text-slate-600 dark:text-slate-300">{item.fileName}</td>
                  <td className="px-4 py-2">
                    <span className="rounded-full bg-slate-100 px-2 py-1 text-xs font-medium uppercase tracking-wide text-slate-700 dark:bg-slate-800 dark:text-slate-200">
                      {item.status}
                    </span>
                    {item.failureReason && (
                      <p className="mt-1 text-xs text-red-500">{item.failureReason}</p>
                    )}
                  </td>
                  <td className="px-4 py-2 text-slate-600 dark:text-slate-300">{item.createdBy}</td>
                  <td className="px-4 py-2 text-slate-600 dark:text-slate-300">
                    {new Date(item.createdAtUtc).toLocaleString()}
                    {item.completedAtUtc && <div className="text-xs text-slate-500 dark:text-slate-400">Dokončeno: {new Date(item.completedAtUtc).toLocaleString()}</div>}
                  </td>
                  <td className="px-4 py-2">
                    <div className="flex gap-2">
                      <button
                        type="button"
                        onClick={() => downloadLog(item)}
                        className="rounded-md border border-slate-300 px-3 py-1 text-xs font-medium text-slate-700 transition hover:bg-slate-100 dark:border-slate-600 dark:text-slate-200 dark:hover:bg-slate-700"
                      >
                        Log
                      </button>
                      {item.manifestJson && (
                        <details className="rounded-md border border-slate-200 px-3 py-1 text-xs dark:border-slate-600">
                          <summary className="cursor-pointer text-slate-700 dark:text-slate-200">Manifest</summary>
                          <pre className="mt-2 max-h-48 overflow-y-auto whitespace-pre-wrap text-[10px] text-slate-600 dark:text-slate-300">
                            {item.manifestJson}
                          </pre>
                        </details>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>
    </div>
  );
}
