import { Link, Navigate, Outlet, Route, Routes } from 'react-router-dom';
import { useCallback, useState } from 'react';
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
import { StorageSettingsPage } from './pages/StorageSettingsPage';
import { SftpSettingsPage } from './pages/SftpSettingsPage';
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
import { PrintingSettingsPage } from './pages/PrintingSettingsPage';
import { ReportsPage } from './pages/ReportsPage';
import { HandoverSettingsPage } from './pages/HandoverSettingsPage';
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
  { to: 'handover', label: 'Předávací protokoly', minimal: true },
  { to: 'connectors', label: 'Konektory', minimal: false },
  { to: 'storage', label: 'Úložiště', minimal: false },
  { to: 'sftp', label: 'SFTP / FTPS', minimal: false },
  { to: 'webhooks', label: 'Webhooks', minimal: false },
  { to: 'printing', label: 'Tisk', minimal: false },
  { to: 'observability', label: 'Observabilita', minimal: false },
  { to: 'backup', label: 'Backup & Restore', minimal: false },
  { to: 'export', label: 'Import/Export', minimal: false },
  { to: 'reports', label: 'Reporty & Schedules', minimal: false },
  { to: 'dictionaries', label: 'Číselníky', minimal: true },
  { to: 'updates', label: 'Aktualizace', minimal: false }
];

const Layout = () => {
  const { theme, toggleTheme } = useTheme();
  const { user, logout, isLoggingOut } = useSession();
  const [mobileNavOpen, setMobileNavOpen] = useState(false);

  const handleLogout = useCallback(async () => {
    try {
      await logout();
    } catch (error) {
      console.error('Odhlášení se nezdařilo', error);
    }
  }, [logout]);

  const closeMobileNav = () => setMobileNavOpen(false);

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-950 via-black to-slate-900 text-slate-100">
      <header className="border-b border-slate-800 bg-slate-950/80 backdrop-blur">
        <div className="mx-auto flex max-w-7xl flex-col gap-4 px-4 py-4 sm:flex-row sm:items-center sm:justify-between sm:px-6 lg:px-8">
          <div className="flex items-center justify-between gap-4">
            <div>
              <h1 className="text-2xl font-semibold text-white">HW Inventory</h1>
              <p className="text-sm text-slate-400">Operations control center</p>
            </div>
            <button
              type="button"
              className="inline-flex items-center justify-center rounded-md border border-slate-700 p-2 text-slate-100 transition hover:bg-slate-800 sm:hidden"
              onClick={() => setMobileNavOpen((prev) => !prev)}
              aria-expanded={mobileNavOpen}
              aria-controls="primary-navigation"
            >
              <span className="sr-only">Přepnout navigaci</span>
              {mobileNavOpen ? '✕' : '☰'}
            </button>
          </div>
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:gap-4">
            <div className="text-sm sm:text-right">
              <p className="font-semibold text-white">{user?.displayName ?? user?.userName}</p>
              <p className="text-xs text-slate-400">{user?.email}</p>
            </div>
            <div className="flex items-center gap-3">
              <button
                onClick={toggleTheme}
                className="rounded-md border border-slate-700 bg-slate-900 px-3 py-2 text-sm font-medium text-slate-100 shadow-sm transition hover:bg-slate-800"
                type="button"
              >
                {theme === 'light' ? '🌙 Dark mode' : '☀️ Light mode'}
              </button>
              <button
                onClick={handleLogout}
                disabled={isLoggingOut}
                className="rounded-md bg-emerald-500 px-3 py-2 text-sm font-semibold text-black transition hover:bg-emerald-400 disabled:opacity-60"
                type="button"
              >
                {isLoggingOut ? 'Odhlasuji…' : 'Odhlásit'}
              </button>
            </div>
          </div>
        </div>
        <nav
          id="primary-navigation"
          className="mx-auto w-full max-w-7xl px-4 pb-4 sm:px-6 lg:px-8"
        >
          <div className="hidden flex-wrap gap-2 text-sm font-medium sm:flex">
            {navItems
              .filter((item) => item.minimal || !appConfig.minimalMode)
              .map((item) => (
                <Link
                  key={item.to}
                  to={item.to}
                  className="rounded-lg border border-slate-800 bg-slate-900/60 px-4 py-2 text-slate-200 transition hover:bg-slate-800/80"
                >
                  {item.label}
                </Link>
              ))}
          </div>
          <div className={`${mobileNavOpen ? 'flex' : 'hidden'} flex-col gap-2 text-sm font-medium sm:hidden`}>
            {navItems
              .filter((item) => item.minimal || !appConfig.minimalMode)
              .map((item) => (
                <Link
                  key={item.to}
                  to={item.to}
                  className="rounded-lg border border-slate-800 bg-slate-900/80 px-4 py-2 text-slate-200 transition hover:bg-slate-800"
                  onClick={closeMobileNav}
                >
                  {item.label}
                </Link>
              ))}
          </div>
        </nav>
      </header>
      <main className="mx-auto w-full max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
        <Outlet />
      </main>
    </div>
  );
};

const SettingsLayout = () => (
  <div className="space-y-6">
    <div className="-mx-4 overflow-x-auto px-4 pb-2">
      <nav className="flex min-w-full flex-wrap gap-2">
        {settingsItems
          .filter((item) => item.minimal || !appConfig.minimalMode)
          .map((item) => (
            <Link
              key={item.to}
              to={item.to}
              className="rounded-lg border border-slate-800 bg-slate-900/70 px-3 py-2 text-sm text-slate-200 transition hover:bg-slate-800"
            >
              {item.label}
            </Link>
          ))}
      </nav>
    </div>
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
          {!appConfig.minimalMode && <Route path="storage" element={<StorageSettingsPage />} />}
          {!appConfig.minimalMode && <Route path="sftp" element={<SftpSettingsPage />} />}
          {!appConfig.minimalMode && <Route path="webhooks" element={<WebhookSettingsPage />} />}
          {!appConfig.minimalMode && <Route path="printing" element={<PrintingSettingsPage />} />}
          {!appConfig.minimalMode && <Route path="observability" element={<ObservabilitySettingsPage />} />}
          {!appConfig.minimalMode && <Route path="backup" element={<BackupRestorePage />} />}
          {!appConfig.minimalMode && <Route path="export" element={<ExportJobsPage />} />}
          {!appConfig.minimalMode && <Route path="reports" element={<ReportsPage />} />}
          {!appConfig.minimalMode && <Route path="updates" element={<UpdatesPage />} />}
          <Route path="handover" element={<HandoverSettingsPage />} />
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
