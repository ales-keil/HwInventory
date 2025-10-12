import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  Server,
  ServerRequest,
  createServer,
  deleteServer,
  getServers,
  retireServer,
  restoreServer,
  updateServer
} from '../api/servers';
import { DictionaryEntry, getDictionaryEntries } from '../api/dictionaries';

interface ServerFormState extends Omit<ServerRequest, 'networkAssignments'> {
  networkAssignments: {
    label: string;
    vlanId?: string | null;
    ipAddress?: string | null;
  }[];
}

const createEmptyForm = (): ServerFormState => ({
  name: '',
  inventoryNumber: '',
  manufacturer: '',
  model: '',
  environmentId: '',
  wsusPriorityId: '',
  operatingSystemId: '',
  serverRoleId: '',
  primaryAdministratorId: null,
  secondaryAdministratorId: null,
  locationId: '',
  rackPosition: '',
  purchasedAt: '',
  supportUntil: '',
  notes: '',
  networkAssignments: [
    { label: 'VLAN1', vlanId: null, ipAddress: '' },
    { label: 'VLAN2', vlanId: null, ipAddress: '' },
    { label: 'VLAN3', vlanId: null, ipAddress: '' }
  ]
});

interface OptionGroup {
  type: string;
  entries: DictionaryEntry[];
}

const dictionaryTypes = [
  'Environment',
  'WsusPriority',
  'OperatingSystem',
  'ServerRole',
  'Location',
  'Vlan'
];

const formatDateValue = (value?: string | null) => (value ? value.substring(0, 10) : '');

export const ServersPage = () => {
  const [servers, setServers] = useState<Server[]>([]);
  const [page, setPage] = useState(1);
  const [totalCount, setTotalCount] = useState(0);
  const [pageSize] = useState(25);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState<ServerFormState>(() => createEmptyForm());
  const [editingId, setEditingId] = useState<string | null>(null);
  const [dicts, setDicts] = useState<OptionGroup[]>([]);

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / pageSize)), [totalCount, pageSize]);

  const loadServers = async (pageIndex = page) => {
    try {
      setLoading(true);
      const response = await getServers(pageIndex, pageSize);
      setServers(response.items);
      setTotalCount(response.totalCount);
      setPage(response.page);
      setError(null);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  const loadDictionaries = async () => {
    const results = await Promise.all(dictionaryTypes.map((type) => getDictionaryEntries(type)));
    const groups = results.map((entries, index) => ({ type: dictionaryTypes[index], entries }));
    setDicts(groups);
  };

  useEffect(() => {
    void loadServers(1);
    void loadDictionaries();
  }, []);

  const dictionary = (type: string) => dicts.find((x) => x.type.toLowerCase() === type.toLowerCase())?.entries ?? [];

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    const payload: ServerRequest = {
      ...form,
      primaryAdministratorId: form.primaryAdministratorId || null,
      secondaryAdministratorId: form.secondaryAdministratorId || null,
      rackPosition: form.rackPosition || null,
      notes: form.notes || null,
      purchasedAt: form.purchasedAt ? new Date(form.purchasedAt).toISOString() : null,
      supportUntil: form.supportUntil ? new Date(form.supportUntil).toISOString() : null,
      networkAssignments: form.networkAssignments
        .filter((assignment) => assignment.vlanId || assignment.ipAddress)
        .map((assignment) => ({
          label: assignment.label,
          vlanId: assignment.vlanId || null,
          ipAddress: assignment.ipAddress || null
        }))
    };

    try {
      if (editingId) {
        await updateServer(editingId, payload);
      } else {
        await createServer(payload);
      }
      setForm(createEmptyForm());
      setEditingId(null);
      await loadServers(page);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleEdit = (server: Server) => {
    setEditingId(server.id);
    setForm({
      name: server.name,
      inventoryNumber: server.inventoryNumber,
      manufacturer: server.manufacturer,
      model: server.model,
      environmentId: server.environmentId,
      wsusPriorityId: server.wsusPriorityId,
      operatingSystemId: server.operatingSystemId,
      serverRoleId: server.serverRoleId,
      primaryAdministratorId: server.primaryAdministratorId ?? null,
      secondaryAdministratorId: server.secondaryAdministratorId ?? null,
      locationId: server.locationId,
      rackPosition: server.rackPosition ?? '',
      purchasedAt: formatDateValue(server.purchasedAt),
      supportUntil: formatDateValue(server.supportUntil),
      notes: server.notes ?? '',
      networkAssignments: [
        { label: 'VLAN1', vlanId: server.networkAssignments[0]?.vlanId ?? null, ipAddress: server.networkAssignments[0]?.ipAddress ?? '' },
        { label: 'VLAN2', vlanId: server.networkAssignments[1]?.vlanId ?? null, ipAddress: server.networkAssignments[1]?.ipAddress ?? '' },
        { label: 'VLAN3', vlanId: server.networkAssignments[2]?.vlanId ?? null, ipAddress: server.networkAssignments[2]?.ipAddress ?? '' }
      ]
    });
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Opravdu chcete server smazat?')) {
      return;
    }
    try {
      await deleteServer(id);
      await loadServers(page);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleRetireToggle = async (server: Server) => {
    try {
      if (server.status === 'Retired') {
        await restoreServer(server.id);
      } else {
        await retireServer(server.id);
      }
      await loadServers(page);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleResetForm = () => {
    setForm(createEmptyForm());
    setEditingId(null);
  };

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">Servery</h2>
        <p className="text-sm text-slate-600 dark:text-slate-400">
          Kompletní evidence serverů včetně vazeb na číselníky a VLAN konfiguraci.
        </p>
      </div>

      {error && <div className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-700">{error}</div>}

      <div className="overflow-x-auto rounded border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <table className="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-700">
          <thead className="bg-slate-100 text-left font-semibold uppercase tracking-wide text-slate-600 dark:bg-slate-800 dark:text-slate-300">
            <tr>
              <th className="px-4 py-3">Název</th>
              <th className="px-4 py-3">Inventární číslo</th>
              <th className="px-4 py-3">Prostředí</th>
              <th className="px-4 py-3">Lokalita</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3 text-right">Akce</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
            {servers.map((server) => (
              <tr key={server.id} className="hover:bg-slate-50 dark:hover:bg-slate-800">
                <td className="px-4 py-3 font-medium text-slate-900 dark:text-slate-100">{server.name}</td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{server.inventoryNumber}</td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{server.environmentId}</td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{server.locationId}</td>
                <td className="px-4 py-3">
                  <span className={`rounded-full px-2 py-1 text-xs font-semibold ${server.status === 'Retired' ? 'bg-amber-100 text-amber-800' : 'bg-emerald-100 text-emerald-700'}`}>
                    {server.status}
                  </span>
                </td>
                <td className="flex items-center justify-end gap-2 px-4 py-3">
                  <button
                    className="rounded border border-slate-200 px-3 py-1 text-xs font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                    onClick={() => handleEdit(server)}
                  >
                    Upravit
                  </button>
                  <button
                    className="rounded border border-slate-200 px-3 py-1 text-xs font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                    onClick={() => handleRetireToggle(server)}
                  >
                    {server.status === 'Retired' ? 'Obnovit' : 'Vyřadit'}
                  </button>
                  <button
                    className="rounded border border-red-300 px-3 py-1 text-xs font-semibold text-red-600 transition hover:bg-red-50 dark:border-red-700 dark:text-red-400 dark:hover:bg-red-900/40"
                    onClick={() => handleDelete(server.id)}
                  >
                    Smazat
                  </button>
                </td>
              </tr>
            ))}
            {!loading && servers.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-sm text-slate-500 dark:text-slate-400">
                  Nebyly nalezeny žádné servery.
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

      <div className="flex items-center justify-between text-sm text-slate-600 dark:text-slate-400">
        <span>
          Stránka {page} / {totalPages} · {totalCount} záznamů
        </span>
        <div className="space-x-2">
          <button
            disabled={page <= 1}
            onClick={() => void loadServers(page - 1)}
            className="rounded border border-slate-200 px-3 py-1 transition disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700"
          >
            Předchozí
          </button>
          <button
            disabled={page >= totalPages}
            onClick={() => void loadServers(page + 1)}
            className="rounded border border-slate-200 px-3 py-1 transition disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700"
          >
            Další
          </button>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4 rounded border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">
            {editingId ? 'Upravit server' : 'Nový server'}
          </h3>
          {editingId && (
            <button type="button" onClick={handleResetForm} className="text-sm text-slate-500 underline">
              Zrušit úpravy
            </button>
          )}
        </div>

        <div className="grid gap-4 md:grid-cols-2">
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
            <span>Inventární číslo</span>
            <input
              required
              value={form.inventoryNumber}
              onChange={(event) => setForm((state) => ({ ...state, inventoryNumber: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>Výrobce</span>
            <input
              required
              value={form.manufacturer}
              onChange={(event) => setForm((state) => ({ ...state, manufacturer: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>Model</span>
            <input
              required
              value={form.model}
              onChange={(event) => setForm((state) => ({ ...state, model: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>

          <label className="flex flex-col gap-1 text-sm">
            <span>Prostředí</span>
            <select
              required
              value={form.environmentId}
              onChange={(event) => setForm((state) => ({ ...state, environmentId: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            >
              <option value="">Vyberte…</option>
              {dictionary('Environment').map((item) => (
                <option key={item.id} value={item.id}>
                  {item.value}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1 text-sm">
            <span>WSUS priorita</span>
            <select
              required
              value={form.wsusPriorityId}
              onChange={(event) => setForm((state) => ({ ...state, wsusPriorityId: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            >
              <option value="">Vyberte…</option>
              {dictionary('WsusPriority').map((item) => (
                <option key={item.id} value={item.id}>
                  {item.value}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1 text-sm">
            <span>Operační systém</span>
            <select
              required
              value={form.operatingSystemId}
              onChange={(event) => setForm((state) => ({ ...state, operatingSystemId: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            >
              <option value="">Vyberte…</option>
              {dictionary('OperatingSystem').map((item) => (
                <option key={item.id} value={item.id}>
                  {item.value}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1 text-sm">
            <span>Role/typ serveru</span>
            <select
              required
              value={form.serverRoleId}
              onChange={(event) => setForm((state) => ({ ...state, serverRoleId: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            >
              <option value="">Vyberte…</option>
              {dictionary('ServerRole').map((item) => (
                <option key={item.id} value={item.id}>
                  {item.value}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1 text-sm">
            <span>Lokalita</span>
            <select
              required
              value={form.locationId}
              onChange={(event) => setForm((state) => ({ ...state, locationId: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            >
              <option value="">Vyberte…</option>
              {dictionary('Location').map((item) => (
                <option key={item.id} value={item.id}>
                  {item.value}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1 text-sm">
            <span>Rack pozice</span>
            <input
              value={form.rackPosition ?? ''}
              onChange={(event) => setForm((state) => ({ ...state, rackPosition: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>

          <label className="flex flex-col gap-1 text-sm">
            <span>Datum zakoupení</span>
            <input
              type="date"
              value={form.purchasedAt ?? ''}
              onChange={(event) => setForm((state) => ({ ...state, purchasedAt: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>

          <label className="flex flex-col gap-1 text-sm">
            <span>Podpora do</span>
            <input
              type="date"
              value={form.supportUntil ?? ''}
              onChange={(event) => setForm((state) => ({ ...state, supportUntil: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>
        </div>

        <div className="grid gap-3 md:grid-cols-3">
          {form.networkAssignments.map((assignment, index) => (
            <div key={assignment.label} className="rounded border border-slate-200 p-3 dark:border-slate-700">
              <h4 className="mb-2 text-sm font-semibold text-slate-700 dark:text-slate-200">{assignment.label}</h4>
              <label className="mb-2 flex flex-col gap-1 text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">
                VLAN
                <select
                  value={assignment.vlanId ?? ''}
                  onChange={(event) => {
                    const value = event.target.value || null;
                    setForm((state) => {
                      const updated = [...state.networkAssignments];
                      updated[index] = { ...updated[index], vlanId: value };
                      return { ...state, networkAssignments: updated };
                    });
                  }}
                  className="rounded border border-slate-300 px-2 py-1 text-sm dark:border-slate-700 dark:bg-slate-800"
                >
                  <option value="">—</option>
                  {dictionary('Vlan').map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.value}
                    </option>
                  ))}
                </select>
              </label>
              <label className="flex flex-col gap-1 text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">
                IP adresa
                <input
                  value={assignment.ipAddress ?? ''}
                  onChange={(event) => {
                    const value = event.target.value;
                    setForm((state) => {
                      const updated = [...state.networkAssignments];
                      updated[index] = { ...updated[index], ipAddress: value };
                      return { ...state, networkAssignments: updated };
                    });
                  }}
                  className="rounded border border-slate-300 px-2 py-1 text-sm dark:border-slate-700 dark:bg-slate-800"
                />
              </label>
            </div>
          ))}
        </div>

        <label className="flex flex-col gap-1 text-sm">
          <span>Poznámka</span>
          <textarea
            value={form.notes ?? ''}
            onChange={(event) => setForm((state) => ({ ...state, notes: event.target.value }))}
            className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            rows={3}
          />
        </label>

        <div className="flex justify-end gap-3">
          <button
            type="button"
            onClick={handleResetForm}
            className="rounded border border-slate-200 px-4 py-2 text-sm font-medium text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
          >
            Zrušit
          </button>
          <button
            type="submit"
            className="rounded bg-emerald-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-emerald-500"
          >
            {editingId ? 'Uložit změny' : 'Přidat server'}
          </button>
        </div>
      </form>
    </div>
  );
};
