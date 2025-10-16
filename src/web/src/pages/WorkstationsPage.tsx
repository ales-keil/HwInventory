import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  Workstation,
  WorkstationRequest,
  createWorkstation,
  deleteWorkstation,
  deleteWorkstations,
  getWorkstations,
  retireWorkstation,
  retireWorkstations,
  restoreWorkstation,
  restoreWorkstations,
  updateWorkstation
} from '../api/workstations';
import { DictionaryEntry, getDictionaryEntries } from '../api/dictionaries';

const dictionaryTypes = ['OperatingSystem', 'WorkstationType', 'Location', 'Vlan'];

const createEmptyForm = (): WorkstationRequest => ({
  name: '',
  inventoryNumber: '',
  operatingSystemId: '',
  workstationTypeId: '',
  ownerId: null,
  ownerDisplayName: '',
  ownerDepartment: '',
  locationId: '',
  locationNote: '',
  cpu: '',
  ram: '',
  storage: '',
  macAddress: '',
  purchasedAt: '',
  supportUntil: '',
  primaryAdministratorId: null,
  secondaryAdministratorId: null,
  notes: '',
  networkAssignments: [
    { label: 'VLAN1', vlanId: null, ipAddress: '' },
    { label: 'VLAN2', vlanId: null, ipAddress: '' },
    { label: 'VLAN3', vlanId: null, ipAddress: '' }
  ]
});

export const WorkstationsPage = () => {
  const [workstations, setWorkstations] = useState<Workstation[]>([]);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(25);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState<WorkstationRequest>(() => createEmptyForm());
  const [editingId, setEditingId] = useState<string | null>(null);
  const [dictionaries, setDictionaries] = useState<Record<string, DictionaryEntry[]>>({});
  const [selected, setSelected] = useState<string[]>([]);

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / pageSize)), [totalCount, pageSize]);

  const loadWorkstations = async (pageIndex = page) => {
    try {
      setLoading(true);
      const response = await getWorkstations(pageIndex, pageSize);
      setWorkstations(response.items);
      setTotalCount(response.totalCount);
      setPage(response.page);
      setSelected((current) => current.filter((id) => response.items.some((item) => item.id === id)));
      setError(null);
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setLoading(false);
    }
  };

  const loadDictionaries = async () => {
    const map: Record<string, DictionaryEntry[]> = {};
    await Promise.all(
      dictionaryTypes.map(async (type) => {
        map[type] = await getDictionaryEntries(type);
      })
    );
    setDictionaries(map);
  };

  useEffect(() => {
    void loadWorkstations(1);
    void loadDictionaries();
  }, []);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    const payload: WorkstationRequest = {
      ...form,
      ownerId: form.ownerId || null,
      ownerDisplayName: form.ownerDisplayName || null,
      ownerDepartment: form.ownerDepartment || null,
      locationNote: form.locationNote || null,
      primaryAdministratorId: form.primaryAdministratorId || null,
      secondaryAdministratorId: form.secondaryAdministratorId || null,
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
        await updateWorkstation(editingId, payload);
      } else {
        await createWorkstation(payload);
      }
      setForm(createEmptyForm());
      setEditingId(null);
      await loadWorkstations(page);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleEdit = (workstation: Workstation) => {
    setEditingId(workstation.id);
    setForm({
      name: workstation.name,
      inventoryNumber: workstation.inventoryNumber,
      operatingSystemId: workstation.operatingSystemId,
      workstationTypeId: workstation.workstationTypeId,
      ownerId: workstation.ownerId ?? null,
      ownerDisplayName: workstation.ownerDisplayName ?? '',
      ownerDepartment: workstation.ownerDepartment ?? '',
      locationId: workstation.locationId,
      locationNote: workstation.locationNote ?? '',
      cpu: workstation.cpu,
      ram: workstation.ram,
      storage: workstation.storage,
      macAddress: workstation.macAddress,
      purchasedAt: workstation.purchasedAt ? workstation.purchasedAt.substring(0, 10) : '',
      supportUntil: workstation.supportUntil ? workstation.supportUntil.substring(0, 10) : '',
      primaryAdministratorId: workstation.primaryAdministratorId ?? null,
      secondaryAdministratorId: workstation.secondaryAdministratorId ?? null,
      notes: workstation.notes ?? '',
      networkAssignments: [
        { label: 'VLAN1', vlanId: workstation.networkAssignments[0]?.vlanId ?? null, ipAddress: workstation.networkAssignments[0]?.ipAddress ?? '' },
        { label: 'VLAN2', vlanId: workstation.networkAssignments[1]?.vlanId ?? null, ipAddress: workstation.networkAssignments[1]?.ipAddress ?? '' },
        { label: 'VLAN3', vlanId: workstation.networkAssignments[2]?.vlanId ?? null, ipAddress: workstation.networkAssignments[2]?.ipAddress ?? '' }
      ]
    });
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Opravdu chcete pracovní stanici smazat?')) {
      return;
    }
    try {
      await deleteWorkstation(id);
      await loadWorkstations(page);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleRetireToggle = async (workstation: Workstation) => {
    try {
      if (workstation.status === 'Retired') {
        await restoreWorkstation(workstation.id);
      } else {
        await retireWorkstation(workstation.id);
      }
      await loadWorkstations(page);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleBulkAction = async (action: 'retire' | 'restore' | 'delete') => {
    if (selected.length === 0) {
      return;
    }

    if (action === 'delete' && !window.confirm(`Opravdu chcete smazat ${selected.length} vybraných pracovních stanic?`)) {
      return;
    }

    try {
      if (action === 'retire') {
        await retireWorkstations(selected);
      } else if (action === 'restore') {
        await restoreWorkstations(selected);
      } else {
        await deleteWorkstations(selected);
      }

      setSelected([]);
      await loadWorkstations(page);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const toggleSelect = (id: string) => {
    setSelected((current) => (current.includes(id) ? current.filter((item) => item !== id) : [...current, id]));
  };

  const allVisibleSelected = workstations.length > 0 && workstations.every((item) => selected.includes(item.id));

  const toggleSelectAllVisible = () => {
    if (allVisibleSelected) {
      setSelected((current) => current.filter((id) => !workstations.some((item) => item.id === id)));
    } else {
      setSelected((current) => {
        const additions = workstations.map((item) => item.id).filter((id) => !current.includes(id));
        return [...current, ...additions];
      });
    }
  };

  const handleResetForm = () => {
    setForm(createEmptyForm());
    setEditingId(null);
  };

  const options = (type: string) => dictionaries[type] ?? [];

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">Pracovní stanice</h2>
        <p className="text-sm text-slate-600 dark:text-slate-400">
          Evidence pracovních stanic, notebooků a thin klientů včetně handover agendy.
        </p>
      </div>

      {error && <div className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-700">{error}</div>}

      {selected.length > 0 && (
        <div className="flex items-center justify-between rounded border border-amber-300 bg-amber-50 px-4 py-3 text-sm text-amber-900 dark:border-amber-500 dark:bg-amber-900/40 dark:text-amber-100">
          <span>Vybráno {selected.length} stanic.</span>
          <div className="flex flex-wrap items-center gap-2">
            <button
              type="button"
              className="rounded border border-slate-300 px-3 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-100 dark:border-slate-600 dark:text-slate-100 dark:hover:bg-slate-800"
              onClick={() => void handleBulkAction('retire')}
            >
              Hromadně vyřadit
            </button>
            <button
              type="button"
              className="rounded border border-slate-300 px-3 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-100 dark:border-slate-600 dark:text-slate-100 dark:hover:bg-slate-800"
              onClick={() => void handleBulkAction('restore')}
            >
              Hromadně obnovit
            </button>
            <button
              type="button"
              className="rounded border border-red-400 px-3 py-1 text-xs font-semibold text-red-700 hover:bg-red-50 dark:border-red-500 dark:text-red-200 dark:hover:bg-red-900/40"
              onClick={() => void handleBulkAction('delete')}
            >
              Hromadně smazat
            </button>
            <button
              type="button"
              className="rounded border border-slate-300 px-3 py-1 text-xs font-semibold text-slate-600 hover:bg-slate-100 dark:border-slate-600 dark:text-slate-100 dark:hover:bg-slate-800"
              onClick={() => setSelected([])}
            >
              Zrušit výběr
            </button>
          </div>
        </div>
      )}

      <div className="overflow-x-auto rounded border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <table className="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-700">
          <thead className="bg-slate-100 text-left font-semibold uppercase tracking-wide text-slate-600 dark:bg-slate-800 dark:text-slate-300">
            <tr>
              <th className="px-4 py-3">
                <input
                  type="checkbox"
                  aria-label="Vybrat vše"
                  checked={workstations.length > 0 && allVisibleSelected}
                  onChange={toggleSelectAllVisible}
                />
              </th>
              <th className="px-4 py-3">Název</th>
              <th className="px-4 py-3">Inventární číslo</th>
              <th className="px-4 py-3">Uživatel</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3 text-right">Akce</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
            {workstations.map((workstation) => (
              <tr key={workstation.id} className="hover:bg-slate-50 dark:hover:bg-slate-800">
                <td className="px-4 py-3">
                  <input
                    type="checkbox"
                    aria-label={`Vybrat ${workstation.name}`}
                    checked={selected.includes(workstation.id)}
                    onChange={() => toggleSelect(workstation.id)}
                  />
                </td>
                <td className="px-4 py-3 font-medium text-slate-900 dark:text-slate-100">{workstation.name}</td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{workstation.inventoryNumber}</td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{workstation.ownerDisplayName ?? '—'}</td>
                <td className="px-4 py-3">
                  <span className={`rounded-full px-2 py-1 text-xs font-semibold ${workstation.status === 'Retired' ? 'bg-amber-100 text-amber-800' : 'bg-emerald-100 text-emerald-700'}`}>
                    {workstation.status}
                  </span>
                </td>
                <td className="flex items-center justify-end gap-2 px-4 py-3">
                  <button
                    className="rounded border border-slate-200 px-3 py-1 text-xs font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                    onClick={() => handleEdit(workstation)}
                  >
                    Upravit
                  </button>
                  <button
                    className="rounded border border-slate-200 px-3 py-1 text-xs font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                    onClick={() => handleRetireToggle(workstation)}
                  >
                    {workstation.status === 'Retired' ? 'Obnovit' : 'Vyřadit'}
                  </button>
                  <button
                    className="rounded border border-red-300 px-3 py-1 text-xs font-semibold text-red-600 transition hover:bg-red-50 dark:border-red-700 dark:text-red-400 dark:hover:bg-red-900/40"
                    onClick={() => handleDelete(workstation.id)}
                  >
                    Smazat
                  </button>
                </td>
              </tr>
            ))}
            {!loading && workstations.length === 0 && (
              <tr>
                <td colSpan={6} className="px-4 py-6 text-center text-sm text-slate-500 dark:text-slate-400">
                  Nebyly nalezeny žádné pracovní stanice.
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
            onClick={() => void loadWorkstations(page - 1)}
            className="rounded border border-slate-200 px-3 py-1 transition disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700"
          >
            Předchozí
          </button>
          <button
            disabled={page >= totalPages}
            onClick={() => void loadWorkstations(page + 1)}
            className="rounded border border-slate-200 px-3 py-1 transition disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700"
          >
            Další
          </button>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4 rounded border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">{editingId ? 'Upravit stanici' : 'Nová stanice'}</h3>
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
            <span>Operační systém</span>
            <select
              required
              value={form.operatingSystemId}
              onChange={(event) => setForm((state) => ({ ...state, operatingSystemId: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            >
              <option value="">Vyberte…</option>
              {options('OperatingSystem').map((item) => (
                <option key={item.id} value={item.id}>
                  {item.value}
                </option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>Typ stanice</span>
            <select
              required
              value={form.workstationTypeId}
              onChange={(event) => setForm((state) => ({ ...state, workstationTypeId: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            >
              <option value="">Vyberte…</option>
              {options('WorkstationType').map((item) => (
                <option key={item.id} value={item.id}>
                  {item.value}
                </option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>Uživatel / vlastník</span>
            <input
              value={form.ownerDisplayName ?? ''}
              onChange={(event) => setForm((state) => ({ ...state, ownerDisplayName: event.target.value }))}
              placeholder="Jméno uživatele"
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>Oddělení</span>
            <input
              value={form.ownerDepartment ?? ''}
              onChange={(event) => setForm((state) => ({ ...state, ownerDepartment: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
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
              {options('Location').map((item) => (
                <option key={item.id} value={item.id}>
                  {item.value}
                </option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>Poznámka k lokaci</span>
            <input
              value={form.locationNote ?? ''}
              onChange={(event) => setForm((state) => ({ ...state, locationNote: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>CPU</span>
            <input
              required
              value={form.cpu}
              onChange={(event) => setForm((state) => ({ ...state, cpu: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>RAM</span>
            <input
              required
              value={form.ram}
              onChange={(event) => setForm((state) => ({ ...state, ram: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>Úložiště</span>
            <input
              required
              value={form.storage}
              onChange={(event) => setForm((state) => ({ ...state, storage: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            />
          </label>
          <label className="flex flex-col gap-1 text-sm">
            <span>MAC adresa</span>
            <input
              required
              value={form.macAddress}
              onChange={(event) => setForm((state) => ({ ...state, macAddress: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 uppercase tracking-wider dark:border-slate-700 dark:bg-slate-800"
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
                  {options('Vlan').map((item) => (
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
            className="rounded bg-indigo-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-indigo-500"
          >
            {editingId ? 'Uložit změny' : 'Přidat stanici'}
          </button>
        </div>
      </form>
    </div>
  );
};
