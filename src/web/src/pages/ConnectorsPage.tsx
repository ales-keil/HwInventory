import { useCallback, useEffect, useState } from 'react';
import {
  ConnectorSummary,
  ConnectorTestRequest,
  ConnectorTestResponse,
  fetchConnectors,
  rotateConnectorSecret,
  testConnector,
  toggleConnector
} from '../api/connectors';

const statusColor = (status?: string | null) => {
  if (!status) {
    return 'bg-slate-200 text-slate-700 dark:bg-slate-800 dark:text-slate-200';
  }

  const lowered = status.toLowerCase();
  if (lowered.includes('healthy')) {
    return 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/50 dark:text-emerald-200';
  }

  if (lowered.includes('fail') || lowered.includes('down')) {
    return 'bg-rose-100 text-rose-700 dark:bg-rose-900/50 dark:text-rose-200';
  }

  if (lowered.includes('degraded') || lowered.includes('warning')) {
    return 'bg-amber-100 text-amber-700 dark:bg-amber-900/50 dark:text-amber-200';
  }

  return 'bg-slate-200 text-slate-700 dark:bg-slate-800 dark:text-slate-200';
};

const formatDate = (value?: string | null) => {
  if (!value) {
    return '—';
  }

  try {
    return new Date(value).toLocaleString();
  } catch (error) {
    return value;
  }
};

const canUse = (connector: ConnectorSummary, capability: 'toggle' | 'test' | 'rotate') => {
  if (!connector.id) {
    return false;
  }

  switch (capability) {
    case 'toggle':
      return connector.supportsToggle;
    case 'test':
      return connector.supportsTest;
    case 'rotate':
      return connector.supportsRotateSecret && connector.hasSecret;
    default:
      return false;
  }
};

const runWithToast = async (
  action: () => Promise<ConnectorSummary | ConnectorTestResponse>,
  onSuccess: (result: ConnectorSummary | ConnectorTestResponse) => void,
  setMessage: (message: string | null) => void
) => {
  try {
    const result = await action();
    onSuccess(result);
  } catch (error) {
    setMessage((error as Error).message);
  }
};

export const ConnectorsPage = () => {
  const [connectors, setConnectors] = useState<ConnectorSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [message, setMessage] = useState<string | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const data = await fetchConnectors();
      setConnectors(data);
    } catch (error) {
      setMessage((error as Error).message);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const updateConnector = (updated?: ConnectorSummary) => {
    if (!updated || !updated.id) {
      return load();
    }

    setConnectors((current) =>
      current.map((connector) => (connector.id === updated.id ? { ...connector, ...updated } : connector))
    );
  };

  const handleToggle = async (connector: ConnectorSummary) => {
    if (!connector.id) {
      return;
    }

    await runWithToast(
      () => toggleConnector(connector.id!, !connector.enabled),
      (result) => {
        setMessage(`Konektor ${result.name} byl ${result.enabled ? 'aktivován' : 'deaktivován'}.`);
        updateConnector(result as ConnectorSummary);
      },
      setMessage
    );
  };

  const handleRotate = async (connector: ConnectorSummary) => {
    if (!connector.id) {
      return;
    }

    const confirmed = window.confirm('Opravdu chcete otočit/odstranit tajemství a vyžadovat jeho nové zadání?');
    if (!confirmed) {
      return;
    }

    await runWithToast(
      () => rotateConnectorSecret(connector.id!),
      (result) => {
        setMessage(`Tajný klíč pro konektor ${result.name} byl vymazán. Nakonfigurujte nový.`);
        updateConnector(result as ConnectorSummary);
      },
      setMessage
    );
  };

  const handleTest = async (connector: ConnectorSummary) => {
    if (!connector.id) {
      return;
    }

    const payload: ConnectorTestRequest = {};
    if (connector.type.toUpperCase() === 'SMTP') {
      const target = window.prompt('Zadejte cílovou e-mailovou adresu pro testovací zprávu:');
      if (!target) {
        return;
      }
      payload.target = target;
    } else if (connector.type.toUpperCase() === 'SMS') {
      const target = window.prompt('Zadejte cílové telefonní číslo pro testovací SMS:');
      if (!target) {
        return;
      }
      payload.target = target;
    }

    await runWithToast(
      () => testConnector(connector.id!, payload),
      (result) => {
        const response = result as ConnectorTestResponse;
        setMessage(response.message);
        if (response.connector) {
          updateConnector(response.connector);
        } else {
          void load();
        }
      },
      setMessage
    );
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">Konektory</h2>
        <p className="mt-1 text-sm text-slate-600 dark:text-slate-400">
          Přehled všech integračních konektorů. Každý záznam zobrazuje aktuální stav, poslední test a možnosti správy.
        </p>
        {message && (
          <div className="mt-3 rounded border border-slate-200 bg-white px-4 py-3 text-sm text-slate-700 shadow dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200">
            {message}
          </div>
        )}
      </div>

      {loading ? (
        <div className="rounded border border-dashed border-slate-300 p-12 text-center text-slate-500 dark:border-slate-700 dark:text-slate-400">
          Načítání konektorů…
        </div>
      ) : (
        <div className="overflow-hidden rounded-lg border border-slate-200 shadow-sm dark:border-slate-800">
          <table className="min-w-full divide-y divide-slate-200 dark:divide-slate-800">
            <thead className="bg-slate-50 dark:bg-slate-900/50">
              <tr>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Alias</th>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Typ</th>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Stav</th>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Poslední test</th>
                <th className="px-4 py-3 text-left text-xs font-semibold uppercase tracking-wide text-slate-500">Popis</th>
                <th className="px-4 py-3 text-right text-xs font-semibold uppercase tracking-wide text-slate-500">Akce</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 bg-white dark:divide-slate-800 dark:bg-slate-950">
              {connectors.map((connector) => (
                <tr key={connector.key}>
                  <td className="px-4 py-4 align-top">
                    <div className="font-medium text-slate-900 dark:text-slate-100">{connector.name}</div>
                    <div className="text-xs text-slate-500 dark:text-slate-400">{connector.key}</div>
                    {connector.requiresConfiguration && (
                      <span className="mt-2 inline-flex rounded bg-amber-100 px-2 py-0.5 text-xs font-medium text-amber-700 dark:bg-amber-900/40 dark:text-amber-200">
                        Vyžaduje konfiguraci
                      </span>
                    )}
                  </td>
                  <td className="px-4 py-4 align-top text-sm text-slate-600 dark:text-slate-300">{connector.type}</td>
                  <td className="px-4 py-4 align-top">
                    <span className={`inline-flex items-center rounded px-2 py-1 text-xs font-medium ${statusColor(connector.healthStatus)}`}>
                      {connector.healthStatus ?? (connector.enabled ? 'Bez testu' : 'Deaktivováno')}
                    </span>
                  </td>
                  <td className="px-4 py-4 align-top text-sm text-slate-600 dark:text-slate-300">{formatDate(connector.lastTestedAtUtc)}</td>
                  <td className="px-4 py-4 align-top text-sm text-slate-600 dark:text-slate-300">
                    {connector.description || '—'}
                  </td>
                  <td className="px-4 py-4 align-top">
                    <div className="flex justify-end gap-2">
                      <button
                        type="button"
                        onClick={() => handleToggle(connector)}
                        disabled={!canUse(connector, 'toggle')}
                        className="rounded border border-slate-200 px-3 py-1 text-xs font-medium text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                      >
                        {connector.enabled ? 'Vypnout' : 'Zapnout'}
                      </button>
                      <button
                        type="button"
                        onClick={() => handleTest(connector)}
                        disabled={!canUse(connector, 'test')}
                        className="rounded border border-slate-200 px-3 py-1 text-xs font-medium text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                      >
                        Test
                      </button>
                      <button
                        type="button"
                        onClick={() => handleRotate(connector)}
                        disabled={!canUse(connector, 'rotate')}
                        className="rounded border border-rose-200 px-3 py-1 text-xs font-medium text-rose-700 transition hover:bg-rose-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-rose-800 dark:text-rose-200 dark:hover:bg-rose-900/40"
                      >
                        Otočit tajemství
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};
