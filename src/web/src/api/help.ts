export interface ManualEntry {
  slug: string;
  title: string;
  description: string;
  url: string;
}

const helpIndex: ManualEntry[] = [
  {
    slug: 'installation',
    title: 'Instalace na IIS',
    description: 'Kroky pro instalaci aplikace na Microsoft IIS a kontrolu prostředí.',
    url: '/docs/manual/installation.html',
  },
  {
    slug: 'first-run',
    title: 'První spuštění',
    description: 'Průvodce inicializačním wizardem a nastavením kritických konektorů.',
    url: '/docs/manual/first-run.html',
  },
  {
    slug: 'inventory',
    title: 'Evidence zařízení',
    description: 'Správa serverů, síťových prvků a pracovních stanic.',
    url: '/docs/manual/inventory.html',
  },
  {
    slug: 'security',
    title: 'Bezpečnost & 2FA',
    description: 'RBAC, 2FA, SSPR a integrace s LDAP/OIDC.',
    url: '/docs/manual/security.html',
  },
  {
    slug: 'import-export',
    title: 'Import / Export',
    description: 'Dry-run, plánování exportů a řešení konfliktů.',
    url: '/docs/manual/import-export.html',
  },
  {
    slug: 'updates',
    title: 'Aktualizace a zálohy',
    description: 'Správa update balíčků, rollback a backup scénáře.',
    url: '/docs/manual/updates.html',
  },
];

export function fetchManualIndex(): Promise<ManualEntry[]> {
  return Promise.resolve(helpIndex);
}

export function getManualEntry(slug: string): ManualEntry | undefined {
  return helpIndex.find((entry) => entry.slug === slug);
}
