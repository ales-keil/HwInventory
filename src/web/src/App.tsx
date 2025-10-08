import { Link, Navigate, Outlet, Route, Routes } from 'react-router-dom';
import { useTheme } from './hooks/useTheme';
import { DashboardPage } from './pages/DashboardPage';
import { FirstRunWizardPage } from './pages/FirstRunWizardPage';
import { PlaceholderPage } from './pages/PlaceholderPage';
import { EmailSettingsPage } from './pages/EmailSettingsPage';
import { ConnectorsPage } from './pages/ConnectorsPage';
import { ObservabilitySettingsPage } from './pages/ObservabilitySettingsPage';
import { BackupRestorePage } from './pages/BackupRestorePage';
import { SecuritySettingsPage } from './pages/SecuritySettingsPage';
import { ExportJobsPage } from './pages/ExportJobsPage';
import { UpdatesPage } from './pages/UpdatesPage';
import { ServersPage } from './pages/ServersPage';
import { NetworkDevicesPage } from './pages/NetworkDevicesPage';
import { WorkstationsPage } from './pages/WorkstationsPage';
import { AuditPage } from './pages/AuditPage';
import { LabelsPage } from './pages/LabelsPage';
import { DictionariesPage } from './pages/DictionariesPage';
import { ModulesPage } from './pages/ModulesPage';

const navItems = [
  { to: '/', label: 'Dashboard' },
  { to: '/servers', label: 'Servers' },
  { to: '/network', label: 'Network' },
  { to: '/workstations', label: 'Workstations' },
  { to: '/audit', label: 'Audit' },
  { to: '/labels', label: 'Labels' },
  { to: '/settings/security', label: 'Settings' },
  { to: '/modules', label: 'Modules' }
];

const settingsItems = [
  { to: 'security', label: 'Security & Auth' },
  { to: 'email', label: 'Email (SMTP)' },
  { to: 'connectors', label: 'Konektory' },
  { to: 'observability', label: 'Observabilita' },
  { to: 'backup', label: 'Backup & Restore' },
  { to: 'export', label: 'Import/Export' },
  { to: 'dictionaries', label: 'Číselníky' },
  { to: 'updates', label: 'Aktualizace' }
];

const Layout = () => {
  const { theme, toggleTheme } = useTheme();

  return (
    <div className="min-h-screen bg-slate-100 dark:bg-slate-950">
      <header className="border-b border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <div>
            <h1 className="text-xl font-semibold text-slate-900 dark:text-white">HW Inventory</h1>
            <p className="text-sm text-slate-500 dark:text-slate-400">Operations control center</p>
          </div>
          <button
            onClick={toggleTheme}
            className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm font-medium shadow-sm transition hover:bg-slate-100 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:hover:bg-slate-700"
            type="button"
          >
            {theme === 'light' ? '🌙 Dark mode' : '☀️ Light mode'}
          </button>
        </div>
        <nav className="bg-slate-50 dark:bg-slate-950">
          <div className="mx-auto flex max-w-6xl flex-wrap gap-3 px-6 py-2 text-sm font-medium">
            {navItems.map((item) => (
              <Link
                key={item.to}
                to={item.to}
                className="rounded px-3 py-1 text-slate-700 transition hover:bg-slate-200 dark:text-slate-200 dark:hover:bg-slate-800"
              >
                {item.label}
              </Link>
            ))}
          </div>
        </nav>
      </header>
      <main className="mx-auto max-w-6xl px-6 py-8">
        <Outlet />
      </main>
    </div>
  );
};

const SettingsLayout = () => (
  <div className="space-y-6">
    <nav className="flex flex-wrap gap-3">
      {settingsItems.map((item) => (
        <Link
          key={item.to}
          to={item.to}
          className="rounded border border-slate-200 px-3 py-1 text-sm text-slate-700 transition hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800"
        >
          {item.label}
        </Link>
      ))}
    </nav>
    <Outlet />
  </div>
);

const App = () => (
  <Routes>
    <Route path="/" element={<Layout />}>
      <Route index element={<DashboardPage />} />
      <Route path="first-run" element={<FirstRunWizardPage />} />
      <Route path="servers" element={<ServersPage />} />
      <Route path="network" element={<NetworkDevicesPage />} />
      <Route path="workstations" element={<WorkstationsPage />} />
      <Route path="audit" element={<AuditPage />} />
      <Route path="labels" element={<LabelsPage />} />
      <Route path="settings" element={<SettingsLayout />}>
        <Route index element={<Navigate to="security" replace />} />
        <Route path="security" element={<SecuritySettingsPage />} />
        <Route path="email" element={<EmailSettingsPage />} />
        <Route path="connectors" element={<ConnectorsPage />} />
        <Route path="observability" element={<ObservabilitySettingsPage />} />
        <Route path="backup" element={<BackupRestorePage />} />
        <Route path="export" element={<ExportJobsPage />} />
        <Route path="updates" element={<UpdatesPage />} />
        <Route path="dictionaries" element={<DictionariesPage />} />
        <Route path="*" element={<PlaceholderPage title="Settings" description="Administrative configuration center." />} />
      </Route>
      <Route path="modules" element={<ModulesPage />} />
    </Route>
  </Routes>
);

export default App;
