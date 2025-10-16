import { useEffect, useState } from 'react';
import { FeatureModule, getFeatureModules, toggleFeatureModule } from '../api/modules';

export const ModulesPage = () => {
  const [modules, setModules] = useState<FeatureModule[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const loadModules = async () => {
    try {
      setLoading(true);
      const data = await getFeatureModules();
      setModules(data);
      setError(null);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    void loadModules();
  }, []);

  const handleToggle = async (module: FeatureModule) => {
    try {
      await toggleFeatureModule(module.key, !module.enabled);
      await loadModules();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">Moduly</h2>
        <p className="text-sm text-slate-600 dark:text-slate-400">
          Zapnutí a vypnutí jednotlivých funkčních celků platformy.
        </p>
      </div>

      {error && <div className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-700">{error}</div>}

      <div className="rounded border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <table className="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-700">
          <thead className="bg-slate-100 text-left font-semibold uppercase tracking-wide text-slate-600 dark:bg-slate-800 dark:text-slate-300">
            <tr>
              <th className="px-4 py-3">Modul</th>
              <th className="px-4 py-3">Popis</th>
              <th className="px-4 py-3">Stav</th>
              <th className="px-4 py-3 text-right">Akce</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
            {modules.map((module) => (
              <tr key={module.id} className="hover:bg-slate-50 dark:hover:bg-slate-800">
                <td className="px-4 py-3 font-medium text-slate-900 dark:text-slate-100">{module.name}</td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{module.description ?? '—'}</td>
                <td className="px-4 py-3">
                  <span className={`rounded-full px-2 py-1 text-xs font-semibold ${module.enabled ? 'bg-emerald-100 text-emerald-700' : 'bg-slate-200 text-slate-700'}`}>
                    {module.enabled ? 'Zapnuto' : 'Vypnuto'}
                  </span>
                </td>
                <td className="px-4 py-3 text-right">
                  <button
                    className="rounded border border-slate-200 px-3 py-1 text-xs font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                    onClick={() => handleToggle(module)}
                  >
                    {module.enabled ? 'Vypnout' : 'Zapnout'}
                  </button>
                </td>
              </tr>
            ))}
            {!loading && modules.length === 0 && (
              <tr>
                <td colSpan={4} className="px-4 py-6 text-center text-sm text-slate-500 dark:text-slate-400">
                  Nebyly nalezeny žádné moduly.
                </td>
              </tr>
            )}
            {loading && (
              <tr>
                <td colSpan={4} className="px-4 py-6 text-center text-sm text-slate-500 dark:text-slate-400">
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
