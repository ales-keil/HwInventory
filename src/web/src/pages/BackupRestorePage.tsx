import { FormEvent, useEffect, useMemo, useState } from 'react';
import {
  BackupJob,
  BackupSchedule,
  BackupScheduleUpdatePayload,
  IntegrityTestPayload,
  QueueBackupPayload,
  RestoreBackupPayload,
  fetchBackupSchedule,
  listBackups,
  queueBackup,
  restoreBackup,
  testBackupIntegrity,
  updateBackupSchedule
} from '../api/maintenance';

const scopes = [
  { value: 'Full', label: 'Celá databáze' },
  { value: 'Servers', label: 'Servery' },
  { value: 'NetworkDevices', label: 'Síťová zařízení' },
  { value: 'Workstations', label: 'Pracovní stanice' },
  { value: 'Dictionaries', label: 'Číselníky' }
];

const frequencies = [
  { value: 'Daily', label: 'Denně' },
  { value: 'Weekly', label: 'Týdně' },
  { value: 'Monthly', label: 'Měsíčně' }
];

const weekDays = [
  { value: 1, label: 'Pondělí' },
  { value: 2, label: 'Úterý' },
  { value: 3, label: 'Středa' },
  { value: 4, label: 'Čtvrtek' },
  { value: 5, label: 'Pátek' },
  { value: 6, label: 'Sobota' },
  { value: 0, label: 'Neděle' }
];

const defaultQueuePayload: QueueBackupPayload = {
  scope: 'Full',
  storagePath: '',
  encryptionEnabled: false,
  password: '',
  passwordConfirmation: '',
  sendEmail: false,
  emailRecipients: '',
  integrityCheckEnabled: true
};

const buildSchedulePayload = (schedule: BackupSchedule | null): BackupScheduleUpdatePayload => ({
  enabled: schedule?.enabled ?? false,
  frequency: schedule?.frequency ?? 'Daily',
  dayOfWeek: schedule?.dayOfWeek ?? null,
  dayOfMonth: schedule?.dayOfMonth ?? null,
  executionTimeUtc: schedule?.executionTimeUtc ?? '01:00',
  scope: schedule?.scope ?? 'Full',
  storagePath: schedule?.storagePath ?? '',
  encryptionEnabled: schedule?.encryptionEnabled ?? false,
  password: '',
  passwordConfirmation: '',
  rotatePassword: false,
  sendEmail: schedule?.sendEmail ?? false,
  emailRecipients: schedule?.emailRecipients ?? '',
  integrityCheckEnabled: schedule?.integrityCheckEnabled ?? true
});

export const BackupRestorePage = () => {
  const [history, setHistory] = useState<BackupJob[]>([]);
  const [loading, setLoading] = useState(false);
  const [queueForm, setQueueForm] = useState<QueueBackupPayload>(defaultQueuePayload);
  const [schedule, setSchedule] = useState<BackupSchedule | null>(null);
  const [scheduleForm, setScheduleForm] = useState<BackupScheduleUpdatePayload>(buildSchedulePayload(null));
  const [feedback, setFeedback] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);

  const dayOptions = useMemo(() => Array.from({ length: 31 }, (_, i) => i + 1), []);

  useEffect(() => {
    const load = async () => {
      try {
        setLoading(true);
        const [historyData, scheduleData] = await Promise.all([listBackups(1, 50), fetchBackupSchedule()]);
        setHistory(historyData);
        setSchedule(scheduleData);
        setScheduleForm(buildSchedulePayload(scheduleData));
      } catch (err) {
        setError((err as Error).message);
      } finally {
        setLoading(false);
      }
    };

    load();
  }, []);

  const refreshHistory = async () => {
    const data = await listBackups(1, 50);
    setHistory(data);
  };

  const handleQueueBackup = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setFeedback(null);

    try {
      const payload: QueueBackupPayload = {
        ...queueForm,
        password: queueForm.encryptionEnabled ? queueForm.password : undefined,
        passwordConfirmation: queueForm.encryptionEnabled ? queueForm.passwordConfirmation : undefined,
        emailRecipients: queueForm.sendEmail ? queueForm.emailRecipients : undefined
      };

      await queueBackup(payload);
      setFeedback('Záloha byla zařazena do fronty.');
      await refreshHistory();
      setQueueForm({ ...defaultQueuePayload, storagePath: queueForm.storagePath });
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleRestore = async (job: BackupJob) => {
    const password = job.encryptionEnabled ? window.prompt('Zadejte heslo pro obnovení (pokud je potřeba):', '') ?? undefined : undefined;
    const confirmIntegrity = window.confirm('Chcete po obnovení provést kontrolu integrity?');

    const payload: RestoreBackupPayload = {
      password: password || undefined,
      performIntegrityTest: confirmIntegrity
    };

    try {
      const response = await restoreBackup(job.id, payload);
      setFeedback(response.message);
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleTestIntegrity = async (job: BackupJob) => {
    const password = job.encryptionEnabled ? window.prompt('Zadejte heslo pro ověření:', '') ?? undefined : undefined;
    const payload: IntegrityTestPayload = { password: password || undefined };

    try {
      const response = await testBackupIntegrity(job.id, payload);
      setFeedback(response.message + (response.passed === false ? ' (Test neprošel)' : ''));
      await refreshHistory();
    } catch (err) {
      setError((err as Error).message);
    }
  };

  const handleScheduleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setError(null);
    setFeedback(null);

    try {
      const payload: BackupScheduleUpdatePayload = {
        ...scheduleForm,
        password: scheduleForm.encryptionEnabled ? scheduleForm.password : undefined,
        passwordConfirmation: scheduleForm.encryptionEnabled ? scheduleForm.passwordConfirmation : undefined,
        emailRecipients: scheduleForm.sendEmail ? scheduleForm.emailRecipients : undefined
      };

      const updated = await updateBackupSchedule(payload);
      setSchedule(updated);
      setScheduleForm(buildSchedulePayload(updated));
      setFeedback('Plán záloh byl uložen.');
    } catch (err) {
      setError((err as Error).message);
    }
  };

  return (
    <div className="space-y-10">
      <header className="space-y-2">
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-slate-100">Zálohování a obnova</h2>
        <p className="text-sm text-slate-600 dark:text-slate-400">
          Spouštění ručních záloh, správa historie a konfigurace automatického plánu záloh.
        </p>
      </header>

      {error && <div className="rounded border border-red-200 bg-red-50 p-3 text-sm text-red-800">{error}</div>}
      {feedback && <div className="rounded border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800">{feedback}</div>}

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Ruční záloha</h3>
        <form className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-2" onSubmit={handleQueueBackup}>
          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200">
            Rozsah
            <select
              className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
              value={queueForm.scope}
              onChange={(event) => setQueueForm((prev) => ({ ...prev, scope: event.target.value }))}
            >
              {scopes.map((scope) => (
                <option key={scope.value} value={scope.value}>
                  {scope.label}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">
            Cesta k úložišti
            <input
              className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
              value={queueForm.storagePath}
              onChange={(event) => setQueueForm((prev) => ({ ...prev, storagePath: event.target.value }))}
              placeholder="např. C:\\Backups"
              required
            />
          </label>

          <div className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
            <input
              id="queue-encryption"
              type="checkbox"
              checked={queueForm.encryptionEnabled}
              onChange={(event) => setQueueForm((prev) => ({ ...prev, encryptionEnabled: event.target.checked }))}
            />
            <label htmlFor="queue-encryption">Zapnout šifrování (AES-256)</label>
          </div>

          <div className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
            <input
              id="queue-email"
              type="checkbox"
              checked={queueForm.sendEmail}
              onChange={(event) => setQueueForm((prev) => ({ ...prev, sendEmail: event.target.checked }))}
            />
            <label htmlFor="queue-email">Odeslat e-mail s výsledkem</label>
          </div>

          {queueForm.encryptionEnabled && (
            <>
              <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200">
                Heslo
                <input
                  type="password"
                  className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
                  value={queueForm.password ?? ''}
                  onChange={(event) => setQueueForm((prev) => ({ ...prev, password: event.target.value }))}
                  required
                />
              </label>
              <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200">
                Potvrzení hesla
                <input
                  type="password"
                  className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
                  value={queueForm.passwordConfirmation ?? ''}
                  onChange={(event) => setQueueForm((prev) => ({ ...prev, passwordConfirmation: event.target.value }))}
                  required
                />
              </label>
            </>
          )}

          {queueForm.sendEmail && (
            <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">
              Příjemci e-mailu (oddělit středníkem)
              <input
                className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
                value={queueForm.emailRecipients ?? ''}
                onChange={(event) => setQueueForm((prev) => ({ ...prev, emailRecipients: event.target.value }))}
              />
            </label>
          )}

          <div className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
            <input
              id="queue-integrity"
              type="checkbox"
              checked={queueForm.integrityCheckEnabled}
              onChange={(event) => setQueueForm((prev) => ({ ...prev, integrityCheckEnabled: event.target.checked }))}
            />
            <label htmlFor="queue-integrity">Otestovat integritu po dokončení</label>
          </div>

          <div className="md:col-span-2">
            <button
              type="submit"
              className="rounded bg-blue-600 px-4 py-2 text-sm font-semibold text-white shadow hover:bg-blue-700"
              disabled={loading}
            >
              Spustit zálohu
            </button>
          </div>
        </form>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Historie záloh</h3>
        <div className="mt-4 overflow-x-auto">
          <table className="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-700">
            <thead className="bg-slate-100 dark:bg-slate-800">
              <tr>
                <th className="px-3 py-2 text-left font-medium text-slate-600 dark:text-slate-300">Název</th>
                <th className="px-3 py-2 text-left font-medium text-slate-600 dark:text-slate-300">Rozsah</th>
                <th className="px-3 py-2 text-left font-medium text-slate-600 dark:text-slate-300">Stav</th>
                <th className="px-3 py-2 text-left font-medium text-slate-600 dark:text-slate-300">Dokončeno</th>
                <th className="px-3 py-2 text-left font-medium text-slate-600 dark:text-slate-300">Velikost</th>
                <th className="px-3 py-2 text-left font-medium text-slate-600 dark:text-slate-300">Akce</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
              {history.map((job) => (
                <tr key={job.id} className="hover:bg-slate-50 dark:hover:bg-slate-800/60">
                  <td className="px-3 py-2 font-medium text-slate-800 dark:text-slate-100">{job.fileName}</td>
                  <td className="px-3 py-2 text-slate-600 dark:text-slate-300">{job.scope}</td>
                  <td className="px-3 py-2 text-slate-600 dark:text-slate-300">
                    {job.status}
                    {job.failureReason && <span className="block text-xs text-red-500">{job.failureReason}</span>}
                  </td>
                  <td className="px-3 py-2 text-slate-600 dark:text-slate-300">
                    {job.completedAtUtc ? new Date(job.completedAtUtc).toLocaleString() : 'Probíhá'}
                  </td>
                  <td className="px-3 py-2 text-slate-600 dark:text-slate-300">
                    {job.fileSizeBytes ? `${(job.fileSizeBytes / 1024 / 1024).toFixed(1)} MB` : '—'}
                  </td>
                  <td className="px-3 py-2 space-x-2 whitespace-nowrap">
                    <button
                      type="button"
                      className="rounded border border-blue-200 px-3 py-1 text-xs font-semibold text-blue-700 hover:bg-blue-50 dark:border-blue-400 dark:text-blue-200 dark:hover:bg-blue-900/40"
                      onClick={() => handleRestore(job)}
                    >
                      Obnovit
                    </button>
                    <button
                      type="button"
                      className="rounded border border-slate-200 px-3 py-1 text-xs font-semibold text-slate-700 hover:bg-slate-50 dark:border-slate-500 dark:text-slate-200 dark:hover:bg-slate-800"
                      onClick={() => handleTestIntegrity(job)}
                    >
                      Test integrity
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-700 dark:bg-slate-900">
        <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Plán automatických záloh</h3>
        <form className="mt-4 grid grid-cols-1 gap-4 md:grid-cols-2" onSubmit={handleScheduleSubmit}>
          <div className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
            <input
              id="schedule-enabled"
              type="checkbox"
              checked={scheduleForm.enabled}
              onChange={(event) => setScheduleForm((prev) => ({ ...prev, enabled: event.target.checked }))}
            />
            <label htmlFor="schedule-enabled">Automatické zálohy povoleny</label>
          </div>

          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200">
            Frekvence
            <select
              className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
              value={scheduleForm.frequency}
              onChange={(event) => setScheduleForm((prev) => ({ ...prev, frequency: event.target.value }))}
            >
              {frequencies.map((frequency) => (
                <option key={frequency.value} value={frequency.value}>
                  {frequency.label}
                </option>
              ))}
            </select>
          </label>

          {scheduleForm.frequency === 'Weekly' && (
            <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200">
              Den v týdnu
              <select
                className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
                value={scheduleForm.dayOfWeek ?? ''}
                onChange={(event) =>
                  setScheduleForm((prev) => ({ ...prev, dayOfWeek: event.target.value === '' ? null : Number(event.target.value) }))
                }
              >
                <option value="">--</option>
                {weekDays.map((day) => (
                  <option key={day.value} value={day.value}>
                    {day.label}
                  </option>
                ))}
              </select>
            </label>
          )}

          {scheduleForm.frequency === 'Monthly' && (
            <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200">
              Den v měsíci
              <select
                className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
                value={scheduleForm.dayOfMonth ?? ''}
                onChange={(event) =>
                  setScheduleForm((prev) => ({ ...prev, dayOfMonth: event.target.value === '' ? null : Number(event.target.value) }))
                }
              >
                <option value="">--</option>
                {dayOptions.map((day) => (
                  <option key={day} value={day}>
                    {day}
                  </option>
                ))}
              </select>
            </label>
          )}

          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200">
            Čas spuštění (UTC)
            <input
              type="time"
              className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
              value={scheduleForm.executionTimeUtc}
              onChange={(event) => setScheduleForm((prev) => ({ ...prev, executionTimeUtc: event.target.value }))}
              required
            />
          </label>

          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200">
            Rozsah
            <select
              className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
              value={scheduleForm.scope}
              onChange={(event) => setScheduleForm((prev) => ({ ...prev, scope: event.target.value }))}
            >
              {scopes.map((scope) => (
                <option key={scope.value} value={scope.value}>
                  {scope.label}
                </option>
              ))}
            </select>
          </label>

          <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">
            Cesta k úložišti
            <input
              className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
              value={scheduleForm.storagePath}
              onChange={(event) => setScheduleForm((prev) => ({ ...prev, storagePath: event.target.value }))}
              required
            />
          </label>

          <div className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
            <input
              id="schedule-encryption"
              type="checkbox"
              checked={scheduleForm.encryptionEnabled}
              onChange={(event) => setScheduleForm((prev) => ({ ...prev, encryptionEnabled: event.target.checked }))}
            />
            <label htmlFor="schedule-encryption">Šifrovat zálohy</label>
          </div>

          {scheduleForm.encryptionEnabled && (
            <div className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
              <input
                id="schedule-rotate"
                type="checkbox"
                checked={scheduleForm.rotatePassword}
                onChange={(event) => setScheduleForm((prev) => ({ ...prev, rotatePassword: event.target.checked }))}
              />
              <label htmlFor="schedule-rotate">Zadat nové heslo / resetovat uložené heslo</label>
            </div>
          )}

          {scheduleForm.encryptionEnabled && scheduleForm.rotatePassword && (
            <>
              <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200">
                Heslo
                <input
                  type="password"
                  className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
                  value={scheduleForm.password ?? ''}
                  onChange={(event) => setScheduleForm((prev) => ({ ...prev, password: event.target.value }))}
                  required
                />
              </label>
              <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200">
                Potvrzení hesla
                <input
                  type="password"
                  className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
                  value={scheduleForm.passwordConfirmation ?? ''}
                  onChange={(event) => setScheduleForm((prev) => ({ ...prev, passwordConfirmation: event.target.value }))}
                  required
                />
              </label>
            </>
          )}

          <div className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
            <input
              id="schedule-email"
              type="checkbox"
              checked={scheduleForm.sendEmail}
              onChange={(event) => setScheduleForm((prev) => ({ ...prev, sendEmail: event.target.checked }))}
            />
            <label htmlFor="schedule-email">Odeslat e-mail po dokončení</label>
          </div>

          {scheduleForm.sendEmail && (
            <label className="flex flex-col gap-1 text-sm font-medium text-slate-700 dark:text-slate-200 md:col-span-2">
              Příjemci e-mailu
              <input
                className="rounded border border-slate-300 bg-white p-2 dark:border-slate-700 dark:bg-slate-800"
                value={scheduleForm.emailRecipients ?? ''}
                onChange={(event) => setScheduleForm((prev) => ({ ...prev, emailRecipients: event.target.value }))}
              />
            </label>
          )}

          <div className="flex items-center gap-2 text-sm text-slate-700 dark:text-slate-200">
            <input
              id="schedule-integrity"
              type="checkbox"
              checked={scheduleForm.integrityCheckEnabled}
              onChange={(event) => setScheduleForm((prev) => ({ ...prev, integrityCheckEnabled: event.target.checked }))}
            />
            <label htmlFor="schedule-integrity">Po záloze provést kontrolu integrity</label>
          </div>

          {schedule && (
            <div className="md:col-span-2 text-sm text-slate-600 dark:text-slate-300">
              <p>
                Poslední běh: {schedule.lastRunAtUtc ? new Date(schedule.lastRunAtUtc).toLocaleString() : '—'}
              </p>
              <p>
                Další běh: {schedule.nextRunAtUtc ? new Date(schedule.nextRunAtUtc).toLocaleString() : '—'}
              </p>
              {schedule.encryptionEnabled && schedule.hasStoredPassword && !scheduleForm.rotatePassword && (
                <p className="text-xs text-slate-500 dark:text-slate-400">Heslo je uloženo (skryté). Zaškrtněte "resetovat", pokud chcete zadat nové.</p>
              )}
            </div>
          )}

          <div className="md:col-span-2">
            <button
              type="submit"
              className="rounded bg-emerald-600 px-4 py-2 text-sm font-semibold text-white shadow hover:bg-emerald-700"
              disabled={loading}
            >
              Uložit plán
            </button>
          </div>
        </form>
      </section>
    </div>
  );
};
