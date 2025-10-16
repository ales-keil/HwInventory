import { useQuery } from '@tanstack/react-query';
import { formatDistanceToNow } from 'date-fns';
import { getDashboardSummary } from '../api/dashboard';

export const DashboardPage = () => {
  const { data, isLoading, isError, error, refetch, isFetching } = useQuery({
    queryKey: ['dashboard-summary'],
    queryFn: getDashboardSummary,
    refetchInterval: 60_000
  });

  return (
    <div className="space-y-6">
      <section>
        <div className="flex items-center justify-between">
          <div>
            <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Inventární přehled</h2>
            <p className="text-sm text-slate-500 dark:text-slate-400">Souhrn zařízení spravovaných v systému.</p>
          </div>
          <button
            onClick={() => refetch()}
            disabled={isFetching}
            className="rounded-md border border-slate-200 px-3 py-1 text-sm text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800 disabled:opacity-60"
            type="button"
          >
            {isFetching ? 'Obnovuji…' : 'Obnovit'}
          </button>
        </div>
        {isLoading && <p className="mt-4 text-sm text-slate-500 dark:text-slate-400">Načítám přehled…</p>}
        {isError && (
          <p className="mt-4 text-sm text-red-600 dark:text-red-400">Chyba při načítání: {error instanceof Error ? error.message : 'Neznámá chyba'}</p>
        )}
        {data && (
          <div className="mt-4 grid gap-4 sm:grid-cols-3">
            {[{ label: 'Servery', value: data.servers }, { label: 'Síťová zařízení', value: data.networkDevices }, { label: 'Pracovní stanice', value: data.workstations }].map((item) => (
              <div key={item.label} className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
                <p className="text-sm text-slate-500 dark:text-slate-400">{item.label}</p>
                <p className="mt-2 text-2xl font-semibold text-slate-900 dark:text-white">{item.value.toLocaleString()}</p>
              </div>
            ))}
          </div>
        )}
      </section>

      {data && (
        <section>
          <h3 className="text-lg font-semibold text-slate-900 dark:text-white">Bezpečnostní ukazatele</h3>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Poslední aktualizace {formatDistanceToNow(new Date(data.security.generatedAtUtc), { addSuffix: true })}.
          </p>
          <div className="mt-4 grid gap-4 sm:grid-cols-3">
            {[{ label: 'Celkem uživatelů', value: data.security.totalUsers }, { label: 'Aktivní uživatelé', value: data.security.activeUsers }, { label: '2FA zapnuto', value: data.security.totpEnabled }, { label: '2FA vyžadováno', value: data.security.totpRequired }, { label: 'Uzamčené účty', value: data.security.lockedOut }, { label: 'Aktivní relace', value: data.security.activeSessions }].map((item) => (
              <div key={item.label} className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
                <p className="text-sm text-slate-500 dark:text-slate-400">{item.label}</p>
                <p className="mt-2 text-xl font-semibold text-slate-900 dark:text-white">{item.value.toLocaleString()}</p>
              </div>
            ))}
          </div>
          <p className="mt-4 text-sm text-slate-500 dark:text-slate-400">
            Čekající reset hesla: <span className="font-semibold text-slate-900 dark:text-white">{data.security.pendingPasswordResets}</span>
          </p>
        </section>
      )}

      <section>
        <h3 className="text-lg font-semibold text-slate-900 dark:text-white">Posledních 10 změn</h3>
        {data ? (
          <div className="mt-3 divide-y divide-slate-200 overflow-hidden rounded-lg border border-slate-200 bg-white shadow-sm dark:divide-slate-800 dark:border-slate-800 dark:bg-slate-900">
            {data.latestChanges.length === 0 && (
              <p className="p-4 text-sm text-slate-500 dark:text-slate-400">Žádné změny nejsou k dispozici.</p>
            )}
            {data.latestChanges.map((entry) => (
              <div key={`${entry.entityType}-${entry.performedAtUtc}-${entry.action}`} className="grid gap-2 p-4 sm:grid-cols-4">
                <p className="text-sm font-medium text-slate-900 dark:text-white">{entry.entityType}</p>
                <p className="text-sm text-slate-600 dark:text-slate-300">{entry.action}</p>
                <p className="text-sm text-slate-600 dark:text-slate-300">{entry.performedBy}</p>
                <p className="text-sm text-slate-500 dark:text-slate-400">{new Date(entry.performedAtUtc).toLocaleString()}</p>
              </div>
            ))}
          </div>
        ) : (
          <p className="mt-3 text-sm text-slate-500 dark:text-slate-400">Načítám data auditu…</p>
        )}
      </section>
    </div>
  );
};
