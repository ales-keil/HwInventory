import { Link, Navigate, Outlet, Route, Routes } from 'react-router-dom';
import { useCallback } from 'react';
import { useTheme } from './hooks/useTheme';
import { DashboardPage } from './pages/DashboardPage';
import { FirstRunWizardPage } from './pages/FirstRunWizardPage';
import { PlaceholderPage } from './pages/PlaceholderPage';
import { EmailSettingsPage } from './pages/EmailSettingsPage';
import { ConnectorsPage } from './pages/ConnectorsPage';
import { ObservabilitySettingsPage } from './pages/ObservabilitySettingsPage';
import { WebhookSettingsPage } from './pages/WebhookSettingsPage';
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
import { HelpCenterPage } from './pages/HelpCenterPage';
import { LoginPage } from './pages/LoginPage';
import { SessionGuard } from './auth/SessionGuard';
import { useSession } from './auth/SessionProvider';
import { appConfig } from './config';
const navItems = [
  { to: '/', label: 'Dashboard', minimal: true },
  { to: '/servers', label: 'Servers', minimal: true },
  { to: '/network', label: 'Network', minimal: true },
  { to: '/workstations', label: 'Workstations', minimal: true },
  { to: '/audit', label: 'Audit', minimal: true },
  { to: '/labels', label: 'Labels', minimal: false },
  { to: '/settings/security', label: 'Settings', minimal: true },
  { to: '/modules', label: 'Modules', minimal: false },
  { to: '/help', label: 'Help Center', minimal: false }
];

const settingsItems = [
  { to: 'security', label: 'Security & Auth', minimal: true },
  { to: 'email', label: 'Email (SMTP)', minimal: false },
  { to: 'connectors', label: 'Konektory', minimal: false },
  { to: 'webhooks', label: 'Webhooks', minimal: false },
  { to: 'observability', label: 'Observabilita', minimal: false },
  { to: 'backup', label: 'Backup & Restore', minimal: false },
  { to: 'export', label: 'Import/Export', minimal: false },
  { to: 'dictionaries', label: 'Číselníky', minimal: true },
  { to: 'updates', label: 'Aktualizace', minimal: false }
];

const Layout = () => {
  const { theme, toggleTheme } = useTheme();
  const { user, logout, isLoggingOut } = useSession();

  const handleLogout = useCallback(async () => {
    try {
      await logout();
    } catch (error) {
      console.error('Odhlášení se nezdařilo', error);
    }
  }, [logout]);

  return (
    <div className="min-h-screen bg-slate-100 dark:bg-slate-950">
      <header className="border-b border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-6 py-4">
          <div>
            <h1 className="text-xl font-semibold text-slate-900 dark:text-white">HW Inventory</h1>
            <p className="text-sm text-slate-500 dark:text-slate-400">Operations control center</p>
          </div>
          <div className="flex items-center gap-4">
            <div className="hidden text-right sm:block">
              <p className="text-sm font-semibold text-slate-800 dark:text-slate-100">{user?.displayName ?? user?.userName}</p>
              <p className="text-xs text-slate-500 dark:text-slate-400">{user?.email}</p>
            </div>
            <button
              onClick={toggleTheme}
              className="rounded-md border border-slate-200 bg-white px-3 py-2 text-sm font-medium shadow-sm transition hover:bg-slate-100 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:hover:bg-slate-700"
              type="button"
            >
              {theme === 'light' ? '🌙 Dark mode' : '☀️ Light mode'}
            </button>
            <button
              onClick={handleLogout}
              disabled={isLoggingOut}
              className="rounded-md bg-slate-900 px-3 py-2 text-sm font-medium text-white shadow-sm transition hover:bg-slate-700 dark:bg-slate-700 dark:hover:bg-slate-600 disabled:opacity-60"
              type="button"
            >
              {isLoggingOut ? 'Odhlasuji…' : 'Odhlásit'}
            </button>
          </div>
        </div>
        <nav className="bg-slate-50 dark:bg-slate-950">
          <div className="mx-auto flex max-w-6xl flex-wrap gap-3 px-6 py-2 text-sm font-medium">
            {navItems
              .filter((item) => item.minimal || !appConfig.minimalMode)
              .map((item) => (
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
      {settingsItems
        .filter((item) => item.minimal || !appConfig.minimalMode)
        .map((item) => (
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
    <Route path="/login" element={<LoginPage />} />
    <Route element={<SessionGuard />}>
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
          {!appConfig.minimalMode && <Route path="email" element={<EmailSettingsPage />} />}
          {!appConfig.minimalMode && <Route path="connectors" element={<ConnectorsPage />} />}
          {!appConfig.minimalMode && <Route path="webhooks" element={<WebhookSettingsPage />} />}
          {!appConfig.minimalMode && <Route path="observability" element={<ObservabilitySettingsPage />} />}
          {!appConfig.minimalMode && <Route path="backup" element={<BackupRestorePage />} />}
          {!appConfig.minimalMode && <Route path="export" element={<ExportJobsPage />} />}
          {!appConfig.minimalMode && <Route path="updates" element={<UpdatesPage />} />}
          <Route path="dictionaries" element={<DictionariesPage />} />
          <Route path="*" element={<PlaceholderPage title="Settings" description="Administrative configuration center." />} />
        </Route>
        {!appConfig.minimalMode && <Route path="modules" element={<ModulesPage />} />}
        {!appConfig.minimalMode && <Route path="help" element={<HelpCenterPage />} />}
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Route>
  </Routes>
);

export default App;
