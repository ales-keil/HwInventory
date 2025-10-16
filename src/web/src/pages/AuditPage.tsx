import { useEffect, useState } from 'react';
import { AuditLogEntry, getAuditLogs } from '../api/audit';

export const AuditPage = () => {
  const [logs, setLogs] = useState<AuditLogEntry[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [entityFilter, setEntityFilter] = useState('');

  const loadLogs = async () => {
    try {
      setLoading(true);
      const items = await getAuditLogs(entityFilter || undefined);
      setLogs(items);
      setError(null);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadLogs();
  }, []);

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-2 md:flex-row md:items-end md:justify-between">
        <div>
          <h2 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">Auditní log</h2>
          <p className="text-sm text-slate-600 dark:text-slate-400">
            Nejnovější změny v systému včetně diffů a identity uživatele.
          </p>
        </div>
        <div className="flex items-end gap-3">
          <label className="flex flex-col gap-1 text-sm">
            <span>Filtr podle entity</span>
            <input
              value={entityFilter}
              onChange={(event) => setEntityFilter(event.target.value)}
              placeholder="Servers / Workstations / Settings…"
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>
          <button
            onClick={() => void loadLogs()}
            className="rounded bg-slate-900 px-4 py-2 text-sm font-semibold text-white transition hover:bg-slate-700"
          >
            Načíst
          </button>
        </div>
      </div>

      {error && <div className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-700">{error}</div>}

      <div className="overflow-x-auto rounded border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <table className="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-700">
          <thead className="bg-slate-100 text-left font-semibold uppercase tracking-wide text-slate-600 dark:bg-slate-800 dark:text-slate-300">
            <tr>
              <th className="px-4 py-3">Čas</th>
              <th className="px-4 py-3">Uživatel</th>
              <th className="px-4 py-3">Role</th>
              <th className="px-4 py-3">Entita</th>
              <th className="px-4 py-3">Akce</th>
              <th className="px-4 py-3">Detaily</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
            {logs.map((log) => (
              <tr key={log.id} className="align-top hover:bg-slate-50 dark:hover:bg-slate-800">
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">
                  {new Date(log.performedAtUtc).toLocaleString()}
                </td>
                <td className="px-4 py-3 font-medium text-slate-900 dark:text-slate-100">{log.performedBy}</td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{log.roles ?? '—'}</td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{log.entityType}</td>
                <td className="px-4 py-3">
                  <span className="rounded-full bg-slate-100 px-2 py-1 text-xs font-semibold uppercase tracking-wide text-slate-700 dark:bg-slate-800 dark:text-slate-200">
                    {log.action}
                  </span>
                </td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">
                  <pre className="whitespace-pre-wrap break-words text-xs leading-relaxed">
                    {log.changedFieldsJson ?? log.changeSummary ?? '—'}
                  </pre>
                </td>
              </tr>
            ))}
            {!loading && logs.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-sm text-slate-500 dark:text-slate-400">
                  Zatím nejsou dostupné žádné auditní záznamy.
                </td>
              </tr>
            )}
            {loading && (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-sm text-slate-500 dark:text-slate-400">
                  Načítám data…
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
};
