export const DashboardPage = () => {
  return (
    <div className="space-y-6">
      <section>
        <h2 className="text-lg font-semibold text-slate-900 dark:text-white">Quick stats</h2>
        <p className="text-sm text-slate-500 dark:text-slate-400">
          Connect the dashboard to /api/dashboard/summary to surface real-time insights across servers, network devices and workstations.
        </p>
        <div className="mt-4 grid gap-4 sm:grid-cols-3">
          {['Servers', 'Network devices', 'Workstations'].map((label) => (
            <div key={label} className="rounded-lg border border-slate-200 bg-white p-4 shadow-sm dark:border-slate-800 dark:bg-slate-900">
              <p className="text-sm text-slate-500 dark:text-slate-400">{label}</p>
              <p className="mt-2 text-2xl font-semibold text-slate-900 dark:text-white">0</p>
            </div>
          ))}
        </div>
      </section>
      <section>
        <h3 className="text-lg font-semibold text-slate-900 dark:text-white">Getting started checklist</h3>
        <ul className="mt-3 space-y-2 text-sm text-slate-600 dark:text-slate-300">
          <li>1. Configure SQL Server connection and run the onboarding wizard.</li>
          <li>2. Enable authentication providers and test 2FA enrollment.</li>
          <li>3. Sync baseline dictionaries and import inventory data.</li>
          <li>4. Review scheduled jobs, backup policies, and alerting connectors.</li>
        </ul>
      </section>
    </div>
  );
};
