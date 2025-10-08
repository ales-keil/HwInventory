import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  NetworkDevice,
  NetworkDeviceRequest,
  createNetworkDevice,
  deleteNetworkDevice,
  getNetworkDevices,
  retireNetworkDevice,
  restoreNetworkDevice,
  updateNetworkDevice
} from '../api/networkDevices';
import { DictionaryEntry, getDictionaryEntries } from '../api/dictionaries';

const dictionaryTypes = ['DeviceType', 'Location', 'Vlan'];

const createEmptyForm = (): NetworkDeviceRequest => ({
  name: '',
  inventoryNumber: '',
  deviceTypeId: '',
  manufacturer: '',
  model: '',
  locationId: '',
  rackPosition: '',
  primaryAdministratorId: null,
  secondaryAdministratorId: null,
  supportUntil: '',
  notes: '',
  networkAssignments: [
    { label: 'VLAN1', vlanId: null, ipAddress: '' },
    { label: 'VLAN2', vlanId: null, ipAddress: '' },
    { label: 'VLAN3', vlanId: null, ipAddress: '' }
  ]
});

export const NetworkDevicesPage = () => {
  const [devices, setDevices] = useState<NetworkDevice[]>([]);
  const [page, setPage] = useState(1);
  const [pageSize] = useState(25);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [form, setForm] = useState<NetworkDeviceRequest>(() => createEmptyForm());
  const [editingId, setEditingId] = useState<string | null>(null);
  const [dictionaries, setDictionaries] = useState<Record<string, DictionaryEntry[]>>({});

  const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / pageSize)), [totalCount, pageSize]);

  const loadDevices = async (pageIndex = page) => {
    try {
      setLoading(true);
      const response = await getNetworkDevices(pageIndex, pageSize);
      setDevices(response.items);
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
    const map: Record<string, DictionaryEntry[]> = {};
    await Promise.all(
      dictionaryTypes.map(async (type) => {
        map[type] = await getDictionaryEntries(type);
      })
    );
    setDictionaries(map);
  };

  useEffect(() => {
    void loadDevices(1);
    void loadDictionaries();
  }, []);

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    const payload: NetworkDeviceRequest = {
      ...form,
      rackPosition: form.rackPosition || null,
      primaryAdministratorId: form.primaryAdministratorId || null,
      secondaryAdministratorId: form.secondaryAdministratorId || null,
      supportUntil: form.supportUntil ? new Date(form.supportUntil).toISOString() : null,
      notes: form.notes || null,
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
        await updateNetworkDevice(editingId, payload);
      } else {
        await createNetworkDevice(payload);
      }
      setForm(createEmptyForm());
      setEditingId(null);
      await loadDevices(page);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleEdit = (device: NetworkDevice) => {
    setEditingId(device.id);
    setForm({
      name: device.name,
      inventoryNumber: device.inventoryNumber,
      deviceTypeId: device.deviceTypeId,
      manufacturer: device.manufacturer,
      model: device.model,
      locationId: device.locationId,
      rackPosition: device.rackPosition ?? '',
      primaryAdministratorId: device.primaryAdministratorId ?? null,
      secondaryAdministratorId: device.secondaryAdministratorId ?? null,
      supportUntil: device.supportUntil ? device.supportUntil.substring(0, 10) : '',
      notes: device.notes ?? '',
      networkAssignments: [
        { label: 'VLAN1', vlanId: device.networkAssignments[0]?.vlanId ?? null, ipAddress: device.networkAssignments[0]?.ipAddress ?? '' },
        { label: 'VLAN2', vlanId: device.networkAssignments[1]?.vlanId ?? null, ipAddress: device.networkAssignments[1]?.ipAddress ?? '' },
        { label: 'VLAN3', vlanId: device.networkAssignments[2]?.vlanId ?? null, ipAddress: device.networkAssignments[2]?.ipAddress ?? '' }
      ]
    });
  };

  const handleDelete = async (id: string) => {
    if (!window.confirm('Opravdu chcete zařízení smazat?')) {
      return;
    }
    try {
      await deleteNetworkDevice(id);
      await loadDevices(page);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleRetireToggle = async (device: NetworkDevice) => {
    try {
      if (device.status === 'Retired') {
        await restoreNetworkDevice(device.id);
      } else {
        await retireNetworkDevice(device.id);
      }
      await loadDevices(page);
    } catch (err) {
      setError((err as Error).message);
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
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">Síťová zařízení</h2>
        <p className="text-sm text-slate-600 dark:text-slate-400">
          Evidence switchů, routerů, firewallů a dalších síťových prvků.
        </p>
      </div>

      {error && <div className="rounded border border-red-300 bg-red-50 p-3 text-sm text-red-700">{error}</div>}

      <div className="overflow-x-auto rounded border border-slate-200 bg-white shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <table className="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-700">
          <thead className="bg-slate-100 text-left font-semibold uppercase tracking-wide text-slate-600 dark:bg-slate-800 dark:text-slate-300">
            <tr>
              <th className="px-4 py-3">Název</th>
              <th className="px-4 py-3">Typ</th>
              <th className="px-4 py-3">Lokalita</th>
              <th className="px-4 py-3">Status</th>
              <th className="px-4 py-3 text-right">Akce</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
            {devices.map((device) => (
              <tr key={device.id} className="hover:bg-slate-50 dark:hover:bg-slate-800">
                <td className="px-4 py-3 font-medium text-slate-900 dark:text-slate-100">{device.name}</td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{device.deviceTypeId}</td>
                <td className="px-4 py-3 text-slate-600 dark:text-slate-300">{device.locationId}</td>
                <td className="px-4 py-3">
                  <span className={`rounded-full px-2 py-1 text-xs font-semibold ${device.status === 'Retired' ? 'bg-amber-100 text-amber-800' : 'bg-emerald-100 text-emerald-700'}`}>
                    {device.status}
                  </span>
                </td>
                <td className="flex items-center justify-end gap-2 px-4 py-3">
                  <button
                    className="rounded border border-slate-200 px-3 py-1 text-xs font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                    onClick={() => handleEdit(device)}
                  >
                    Upravit
                  </button>
                  <button
                    className="rounded border border-slate-200 px-3 py-1 text-xs font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
                    onClick={() => handleRetireToggle(device)}
                  >
                    {device.status === 'Retired' ? 'Obnovit' : 'Vyřadit'}
                  </button>
                  <button
                    className="rounded border border-red-300 px-3 py-1 text-xs font-semibold text-red-600 transition hover:bg-red-50 dark:border-red-700 dark:text-red-400 dark:hover:bg-red-900/40"
                    onClick={() => handleDelete(device.id)}
                  >
                    Smazat
                  </button>
                </td>
              </tr>
            ))}
            {!loading && devices.length === 0 && (
              <tr>
                <td colSpan={5} className="px-4 py-6 text-center text-sm text-slate-500 dark:text-slate-400">
                  Nebyla nalezena žádná síťová zařízení.
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

      <div className="flex items-center justify-between text-sm text-slate-600 dark:text-slate-400">
        <span>
          Stránka {page} / {totalPages} · {totalCount} záznamů
        </span>
        <div className="space-x-2">
          <button
            disabled={page <= 1}
            onClick={() => void loadDevices(page - 1)}
            className="rounded border border-slate-200 px-3 py-1 transition disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700"
          >
            Předchozí
          </button>
          <button
            disabled={page >= totalPages}
            onClick={() => void loadDevices(page + 1)}
            className="rounded border border-slate-200 px-3 py-1 transition disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-700"
          >
            Další
          </button>
        </div>
      </div>

      <form onSubmit={handleSubmit} className="space-y-4 rounded border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <div className="flex items-center justify-between">
          <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">{editingId ? 'Upravit zařízení' : 'Nové zařízení'}</h3>
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
            <span>Typ zařízení</span>
            <select
              required
              value={form.deviceTypeId}
              onChange={(event) => setForm((state) => ({ ...state, deviceTypeId: event.target.value }))}
              className="rounded border border-slate-300 px-3 py-2 dark:border-slate-700 dark:bg-slate-800"
            >
              <option value="">Vyberte…</option>
              {options('DeviceType').map((item) => (
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
              {options('Location').map((item) => (
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
            className="rounded bg-blue-600 px-4 py-2 text-sm font-semibold text-white transition hover:bg-blue-500"
          >
            {editingId ? 'Uložit změny' : 'Přidat zařízení'}
          </button>
        </div>
      </form>
    </div>
  );
};
