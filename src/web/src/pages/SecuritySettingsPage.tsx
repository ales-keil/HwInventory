import { FormEvent, useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  dryRunLdap,
  getLdapConfiguration,
  getOidcConfiguration,
  getSsprConfiguration,
  LdapConfiguration,
  LdapConfigurationRequest,
  LdapConnectionTestResult,
  OidcConfiguration,
  SsprConfiguration,
  testLdapConnection,
  updateLdapConfiguration,
  updateOidcConfiguration,
  updateSsprConfiguration
} from '../api/security';
import { AuditLogEntry, getAuditLogs } from '../api/audit';

interface FlashMessage {
  type: 'success' | 'error';
  message: string;
}

const parseClaimMappings = (input: string): Record<string, string> => {
  const mappings: Record<string, string> = {};
  input
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .forEach((line) => {
      const [key, value] = line.split('=', 2);
      if (key && value) {
        mappings[key.trim()] = value.trim();
      }
    });
  return mappings;
};

const stringifyClaimMappings = (mappings: Record<string, string>): string =>
  Object.entries(mappings)
    .map(([key, value]) => `${key}=${value}`)
    .join('\n');

const parseList = (input: string): string[] =>
  input
    .split(/\r?\n/)
    .map((item) => item.trim())
    .filter(Boolean);

const stringifyList = (items: string[]): string => items.join('\n');

const SECURITY_AUDIT_TYPES = ['Security.OIDC', 'Security.LDAP', 'Security.SSPR'];

export const SecuritySettingsPage = () => {
  const queryClient = useQueryClient();
  const [flash, setFlash] = useState<FlashMessage | null>(null);

  const [oidcState, setOidcState] = useState<OidcConfiguration>({
    enabled: false,
    authority: '',
    clientId: '',
    clientSecret: '',
    responseType: 'code',
    scopes: ['openid', 'profile', 'email'],
    claimMappings: { name: 'name', email: 'email' },
    usePkce: true
  });
  const [scopesInput, setScopesInput] = useState('openid profile email');
  const [claimMappingsInput, setClaimMappingsInput] = useState('name=name\nemail=email');

  const [ldapState, setLdapState] = useState<{
    form: LdapConfigurationRequest;
    hasPassword: boolean;
  }>({
    form: {
      enabled: false,
      host: '',
      port: 389,
      useSsl: false,
      bindDn: '',
      password: '',
      resetPassword: false,
      ignoreCertificateErrors: false,
      usersBaseDn: '',
      usersFilter: '',
      attributeMap: []
    },
    hasPassword: false
  });
  const [ldapTestResult, setLdapTestResult] = useState<LdapConnectionTestResult | null>(null);
  const [ldapDryRunResult, setLdapDryRunResult] = useState<{
    resultCount: number;
    truncated: boolean;
    notes?: string | null;
    users: Array<{ distinguishedName: string; attributes: Record<string, string | null> }>;
  } | null>(null);
  const [ldapAttributeMapInput, setLdapAttributeMapInput] = useState('');

  const [ssprState, setSsprState] = useState<SsprConfiguration>({
    enabled: false,
    requireTwoFactor: true,
    requireCaptcha: true,
    requireSmsOtp: false,
    tokenExpiryMinutes: 30,
    throttleWindowMinutes: 15,
    maxRequestsPerWindow: 3,
    smsConnectorKey: ''
  });

  const [auditExpanded, setAuditExpanded] = useState<string | null>(null);

  const oidcQuery = useQuery({
    queryKey: ['security', 'oidc'],
    queryFn: getOidcConfiguration,
    onSuccess: (data) => {
      if (data) {
        setOidcState(data);
        setScopesInput(data.scopes.join(' '));
        setClaimMappingsInput(stringifyClaimMappings(data.claimMappings));
      }
    }
  });

  const ldapQuery = useQuery({
    queryKey: ['security', 'ldap'],
    queryFn: getLdapConfiguration,
    onSuccess: (data: LdapConfiguration) => {
      setLdapState({
        form: {
          enabled: data.enabled,
          host: data.host,
          port: data.port,
          useSsl: data.useSsl,
          bindDn: data.bindDn,
          password: '',
          resetPassword: false,
          ignoreCertificateErrors: data.ignoreCertificateErrors,
          usersBaseDn: data.usersBaseDn,
          usersFilter: data.usersFilter ?? '',
          attributeMap: data.attributeMap
        },
        hasPassword: data.hasPassword
      });
      setLdapAttributeMapInput(stringifyList(data.attributeMap));
    }
  });

  const ssprQuery = useQuery({
    queryKey: ['security', 'sspr'],
    queryFn: getSsprConfiguration,
    onSuccess: (data) => {
      setSsprState(data);
    }
  });

  const auditQuery = useQuery({
    queryKey: ['audit', 'security'],
    queryFn: async () => {
      const responses = await Promise.all(SECURITY_AUDIT_TYPES.map((type) => getAuditLogs(type)));
      return responses.flat().sort((a, b) => new Date(b.performedAtUtc).getTime() - new Date(a.performedAtUtc).getTime());
    }
  });

  const showFlash = (message: FlashMessage) => {
    setFlash(message);
    window.setTimeout(() => setFlash(null), 5000);
  };

  const oidcMutation = useMutation({
    mutationFn: updateOidcConfiguration,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['security', 'oidc'] });
      queryClient.invalidateQueries({ queryKey: ['audit', 'security'] });
      showFlash({ type: 'success', message: 'OIDC konfigurace byla uložena.' });
    },
    onError: (error: unknown) => {
      showFlash({ type: 'error', message: error instanceof Error ? error.message : 'Nepodařilo se uložit OIDC konfiguraci.' });
    }
  });

  const ldapMutation = useMutation({
    mutationFn: updateLdapConfiguration,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['security', 'ldap'] });
      queryClient.invalidateQueries({ queryKey: ['audit', 'security'] });
      showFlash({ type: 'success', message: 'LDAP konfigurace byla uložena.' });
    },
    onError: (error: unknown) => {
      showFlash({ type: 'error', message: error instanceof Error ? error.message : 'Nepodařilo se uložit LDAP konfiguraci.' });
    }
  });

  const ssprMutation = useMutation({
    mutationFn: updateSsprConfiguration,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['security', 'sspr'] });
      queryClient.invalidateQueries({ queryKey: ['audit', 'security'] });
      showFlash({ type: 'success', message: 'SSPR nastavení byla uložena.' });
    },
    onError: (error: unknown) => {
      showFlash({ type: 'error', message: error instanceof Error ? error.message : 'Nepodařilo se uložit SSPR nastavení.' });
    }
  });

  const ldapTestMutation = useMutation({
    mutationFn: testLdapConnection,
    onSuccess: (data) => {
      setLdapTestResult(data);
      showFlash({ type: 'success', message: 'Test LDAP připojení dokončen.' });
    },
    onError: (error: unknown) => {
      setLdapTestResult(null);
      showFlash({ type: 'error', message: error instanceof Error ? error.message : 'LDAP test selhal.' });
    }
  });

  const ldapDryRunMutation = useMutation({
    mutationFn: dryRunLdap,
    onSuccess: (data) => {
      setLdapDryRunResult(data);
      showFlash({ type: 'success', message: 'LDAP dry-run byl úspěšný.' });
    },
    onError: (error: unknown) => {
      setLdapDryRunResult(null);
      showFlash({ type: 'error', message: error instanceof Error ? error.message : 'LDAP dry-run selhal.' });
    }
  });

  const handleOidcSubmit = (event: FormEvent) => {
    event.preventDefault();
    const payload: OidcConfiguration = {
      ...oidcState,
      scopes: scopesInput.split(/\s+/).map((scope) => scope.trim()).filter(Boolean),
      claimMappings: parseClaimMappings(claimMappingsInput)
    };
    oidcMutation.mutate(payload);
  };

  const handleLdapSubmit = (event: FormEvent) => {
    event.preventDefault();
    const trimmedFilter = ldapState.form.usersFilter?.trim();
    const payload: LdapConfigurationRequest = {
      ...ldapState.form,
      usersBaseDn: ldapState.form.usersBaseDn.trim(),
      usersFilter: trimmedFilter ? trimmedFilter : undefined,
      attributeMap: parseList(ldapAttributeMapInput),
      password: ldapState.form.password ? ldapState.form.password : undefined
    };
    ldapMutation.mutate(payload);
  };

  const handleSsprSubmit = (event: FormEvent) => {
    event.preventDefault();
    const payload: SsprConfiguration = {
      ...ssprState,
      smsConnectorKey: ssprState.smsConnectorKey?.trim() ? ssprState.smsConnectorKey.trim() : undefined
    };
    ssprMutation.mutate(payload);
  };

  useEffect(() => {
    if (ldapMutation.isSuccess) {
      setLdapState((current) => ({
        form: { ...current.form, password: '', resetPassword: false },
        hasPassword: current.form.password ? true : current.hasPassword
      }));
    }
  }, [ldapMutation.isSuccess]);

  const securityAuditEntries = useMemo(() => auditQuery.data ?? [], [auditQuery.data]);

  const renderAuditDetails = (entry: AuditLogEntry) => {
    if (!entry.changedFieldsJson) {
      return null;
    }
    try {
      const parsed = JSON.parse(entry.changedFieldsJson);
      return <pre className="mt-2 whitespace-pre-wrap rounded bg-slate-900/80 p-3 text-xs text-slate-100">{JSON.stringify(parsed, null, 2)}</pre>;
    } catch (error) {
      return <pre className="mt-2 whitespace-pre-wrap rounded bg-slate-900/80 p-3 text-xs text-slate-100">{entry.changedFieldsJson}</pre>;
    }
  };

  return (
    <div className="space-y-10">
      <header>
        <h2 className="text-2xl font-semibold text-slate-900 dark:text-white">Bezpečnostní nastavení</h2>
        <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
          Spravujte externí identity, připojení k LDAP/AD a pravidla self-service resetu hesel. Změny se okamžitě zapisují do auditu.
        </p>
      </header>

      {flash && (
        <div
          className={`rounded-md border px-4 py-3 text-sm ${
            flash.type === 'success'
              ? 'border-emerald-300 bg-emerald-50 text-emerald-800 dark:border-emerald-700 dark:bg-emerald-950 dark:text-emerald-100'
              : 'border-rose-300 bg-rose-50 text-rose-800 dark:border-rose-700 dark:bg-rose-950 dark:text-rose-100'
          }`}
        >
          {flash.message}
        </div>
      )}

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <form onSubmit={handleOidcSubmit} className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">SSO / OIDC</h3>
              <p className="text-sm text-slate-500 dark:text-slate-400">Konfigurace OpenID Connect přihlášení.</p>
            </div>
            <label className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                checked={oidcState.enabled}
                onChange={(event) => setOidcState((state) => ({ ...state, enabled: event.target.checked }))}
                className="h-4 w-4"
              />
              Povolit OIDC
            </label>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Authority URL
              <input
                value={oidcState.authority}
                onChange={(event) => setOidcState((state) => ({ ...state, authority: event.target.value }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="https://login.example.com"
                required
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Client ID
              <input
                value={oidcState.clientId}
                onChange={(event) => setOidcState((state) => ({ ...state, clientId: event.target.value }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="hw-inventory"
                required
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Client secret
              <input
                value={oidcState.clientSecret ?? ''}
                onChange={(event) => setOidcState((state) => ({ ...state, clientSecret: event.target.value }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="••••••"
                type="password"
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Response type
              <input
                value={oidcState.responseType ?? 'code'}
                onChange={(event) => setOidcState((state) => ({ ...state, responseType: event.target.value }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="code"
              />
            </label>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Scope hodnoty
              <input
                value={scopesInput}
                onChange={(event) => setScopesInput(event.target.value)}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="openid profile email"
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Claim mapování (key=value)
              <textarea
                value={claimMappingsInput}
                onChange={(event) => setClaimMappingsInput(event.target.value)}
                className="mt-1 h-24 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
              />
            </label>
          </div>

          <label className="inline-flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
            <input
              type="checkbox"
              checked={oidcState.usePkce}
              onChange={(event) => setOidcState((state) => ({ ...state, usePkce: event.target.checked }))}
              className="h-4 w-4"
            />
            Použít PKCE
          </label>

          <div className="flex justify-end">
            <button
              type="submit"
              className="rounded bg-sky-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-sky-700 disabled:opacity-50"
              disabled={oidcMutation.isPending}
            >
              {oidcMutation.isPending ? 'Ukládám…' : 'Uložit OIDC' }
            </button>
          </div>
        </form>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <form onSubmit={handleLdapSubmit} className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">LDAP / AD</h3>
              <p className="text-sm text-slate-500 dark:text-slate-400">Základní připojení a mapování atributů.</p>
            </div>
            <label className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                checked={ldapState.form.enabled}
                onChange={(event) =>
                  setLdapState((state) => ({
                    ...state,
                    form: { ...state.form, enabled: event.target.checked }
                  }))
                }
                className="h-4 w-4"
              />
              Povolit LDAP synchronizaci
            </label>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Host
              <input
                value={ldapState.form.host}
                onChange={(event) => setLdapState((state) => ({ ...state, form: { ...state.form, host: event.target.value } }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="ldap.example.com"
                required
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Port
              <input
                type="number"
                value={ldapState.form.port}
                onChange={(event) =>
                  setLdapState((state) => ({
                    ...state,
                    form: { ...state.form, port: Number(event.target.value) }
                  }))
                }
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                min={1}
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Bind DN
              <input
                value={ldapState.form.bindDn}
                onChange={(event) => setLdapState((state) => ({ ...state, form: { ...state.form, bindDn: event.target.value } }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="CN=Service,OU=Accounts,DC=example,DC=com"
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Heslo
              <input
                type="password"
                value={ldapState.form.password ?? ''}
                onChange={(event) =>
                  setLdapState((state) => ({
                    ...state,
                    form: { ...state.form, password: event.target.value }
                  }))
                }
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder={ldapState.hasPassword ? '••••••' : 'Zadejte heslo'}
              />
              <label className="mt-2 inline-flex items-center gap-2 text-xs font-normal text-slate-500 dark:text-slate-400">
                <input
                  type="checkbox"
                  checked={ldapState.form.resetPassword}
                  onChange={(event) =>
                    setLdapState((state) => ({
                      ...state,
                      form: { ...state.form, resetPassword: event.target.checked }
                    }))
                  }
                  className="h-4 w-4"
                />
                Resetovat uložené heslo (ponechá prázdné)
              </label>
            </label>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                checked={ldapState.form.useSsl}
                onChange={(event) => setLdapState((state) => ({ ...state, form: { ...state.form, useSsl: event.target.checked } }))}
                className="h-4 w-4"
              />
              Použít LDAPS / SSL
            </label>
            <label className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                checked={ldapState.form.ignoreCertificateErrors}
                onChange={(event) =>
                  setLdapState((state) => ({
                    ...state,
                    form: { ...state.form, ignoreCertificateErrors: event.target.checked }
                  }))
                }
                className="h-4 w-4"
              />
              Ignorovat chyby certifikátu
            </label>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Base DN pro uživatele
              <input
                value={ldapState.form.usersBaseDn}
                onChange={(event) => setLdapState((state) => ({ ...state, form: { ...state.form, usersBaseDn: event.target.value } }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="OU=Users,DC=example,DC=com"
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Filtr uživatelů
              <input
                value={ldapState.form.usersFilter ?? ''}
                onChange={(event) => setLdapState((state) => ({ ...state, form: { ...state.form, usersFilter: event.target.value } }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="(objectClass=person)"
              />
            </label>
          </div>

          <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
            Mapování atributů (každý řádek: attribute)
            <textarea
              value={ldapAttributeMapInput}
              onChange={(event) => setLdapAttributeMapInput(event.target.value)}
              className="mt-1 h-28 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
              placeholder={'displayName\nmail\ndepartment'}
            />
          </label>

          <div className="flex flex-wrap items-center gap-3">
            <button
              type="submit"
              className="rounded bg-sky-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-sky-700 disabled:opacity-50"
              disabled={ldapMutation.isPending}
            >
              {ldapMutation.isPending ? 'Ukládám…' : 'Uložit LDAP'}
            </button>
            <button
              type="button"
              className="rounded border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-600 dark:text-slate-100 dark:hover:bg-slate-800"
              onClick={() =>
                ldapTestMutation.mutate({
                  host: ldapState.form.host,
                  port: ldapState.form.port,
                  useSsl: ldapState.form.useSsl,
                  bindDn: ldapState.form.bindDn,
                  password: ldapState.form.password || undefined,
                  ignoreCertificateErrors: ldapState.form.ignoreCertificateErrors
                })
              }
              disabled={ldapTestMutation.isPending}
            >
              {ldapTestMutation.isPending ? 'Testuji…' : 'Test připojení'}
            </button>
            <button
              type="button"
              className="rounded border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-600 dark:text-slate-100 dark:hover:bg-slate-800"
              onClick={() =>
                ldapDryRunMutation.mutate({
                  connection: {
                    host: ldapState.form.host,
                    port: ldapState.form.port,
                    useSsl: ldapState.form.useSsl,
                    bindDn: ldapState.form.bindDn,
                    password: ldapState.form.password || undefined,
                    ignoreCertificateErrors: ldapState.form.ignoreCertificateErrors
                  },
                  usersBaseDn: ldapState.form.usersBaseDn,
                  usersFilter: ldapState.form.usersFilter || undefined,
                  attributeMap: parseList(ldapAttributeMapInput),
                  resultLimit: 20
                })
              }
              disabled={ldapDryRunMutation.isPending}
            >
              {ldapDryRunMutation.isPending ? 'Spouštím…' : 'Dry-run náhled'}
            </button>
          </div>

          {ldapTestResult && (
            <div className={`rounded-md border px-4 py-3 text-sm ${ldapTestResult.success ? 'border-emerald-300 bg-emerald-50 text-emerald-800 dark:border-emerald-700 dark:bg-emerald-950 dark:text-emerald-100' : 'border-rose-300 bg-rose-50 text-rose-800 dark:border-rose-700 dark:bg-rose-950 dark:text-rose-100'}`}>
              <p className="font-medium">Výsledek testu: {ldapTestResult.message}</p>
              {ldapTestResult.diagnostics && (
                <pre className="mt-2 whitespace-pre-wrap text-xs text-slate-600 dark:text-slate-300">
                  {JSON.stringify(ldapTestResult.diagnostics, null, 2)}
                </pre>
              )}
            </div>
          )}

          {ldapDryRunResult && (
            <div className="rounded-md border border-slate-200 bg-slate-50 p-4 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100">
              <p className="font-medium">
                Dry-run: {ldapDryRunResult.resultCount} záznamů{ldapDryRunResult.truncated ? ' (výsledek zkrácen)' : ''}
              </p>
              {ldapDryRunResult.notes && <p className="mt-1 text-xs text-slate-500">{ldapDryRunResult.notes}</p>}
              <div className="mt-3 space-y-3">
                {ldapDryRunResult.users.map((user) => (
                  <div key={user.distinguishedName} className="rounded border border-slate-200 bg-white p-3 text-xs dark:border-slate-700 dark:bg-slate-900">
                    <p className="font-semibold text-slate-700 dark:text-slate-200">{user.distinguishedName}</p>
                    <pre className="mt-2 whitespace-pre-wrap text-[11px] text-slate-600 dark:text-slate-300">
                      {JSON.stringify(user.attributes, null, 2)}
                    </pre>
                  </div>
                ))}
              </div>
            </div>
          )}
        </form>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <form onSubmit={handleSsprSubmit} className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Self-service reset hesla (SSPR)</h3>
              <p className="text-sm text-slate-500 dark:text-slate-400">Ovládá proces obnovy hesla pro lokální účty.</p>
            </div>
            <label className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                checked={ssprState.enabled}
                onChange={(event) => setSsprState((state) => ({ ...state, enabled: event.target.checked }))}
                className="h-4 w-4"
              />
              Povolit SSPR
            </label>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="inline-flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                checked={ssprState.requireTwoFactor}
                onChange={(event) => setSsprState((state) => ({ ...state, requireTwoFactor: event.target.checked }))}
                className="h-4 w-4"
              />
              Vyžadovat 2FA před odesláním resetu
            </label>
            <label className="inline-flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                checked={ssprState.requireCaptcha}
                onChange={(event) => setSsprState((state) => ({ ...state, requireCaptcha: event.target.checked }))}
                className="h-4 w-4"
              />
              Vyžadovat CAPTCHA
            </label>
            <label className="inline-flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                checked={ssprState.requireSmsOtp}
                onChange={(event) => setSsprState((state) => ({ ...state, requireSmsOtp: event.target.checked }))}
                className="h-4 w-4"
              />
              Povolit SMS OTP
            </label>
          </div>

          <div className="grid gap-4 md:grid-cols-3">
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Expirace tokenu (minuty)
              <input
                type="number"
                value={ssprState.tokenExpiryMinutes}
                onChange={(event) => setSsprState((state) => ({ ...state, tokenExpiryMinutes: Number(event.target.value) }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                min={5}
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Okno throttlingu (minuty)
              <input
                type="number"
                value={ssprState.throttleWindowMinutes}
                onChange={(event) => setSsprState((state) => ({ ...state, throttleWindowMinutes: Number(event.target.value) }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                min={1}
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Max. požadavků v okně
              <input
                type="number"
                value={ssprState.maxRequestsPerWindow}
                onChange={(event) => setSsprState((state) => ({ ...state, maxRequestsPerWindow: Number(event.target.value) }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                min={1}
              />
            </label>
          </div>

          <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
            SMS konektor (alias)
            <input
              value={ssprState.smsConnectorKey ?? ''}
              onChange={(event) => setSsprState((state) => ({ ...state, smsConnectorKey: event.target.value }))}
              className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
              placeholder="sms-primary"
            />
          </label>

          <div className="flex justify-end">
            <button
              type="submit"
              className="rounded bg-sky-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-sky-700 disabled:opacity-50"
              disabled={ssprMutation.isPending}
            >
              {ssprMutation.isPending ? 'Ukládám…' : 'Uložit SSPR'}
            </button>
          </div>
        </form>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="mb-4 flex items-center justify-between">
          <div>
            <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Audit bezpečnostních změn</h3>
            <p className="text-sm text-slate-500 dark:text-slate-400">Poslední operace na nastaveních OIDC, LDAP a SSPR.</p>
          </div>
          <button
            type="button"
            onClick={() => queryClient.invalidateQueries({ queryKey: ['audit', 'security'] })}
            className="rounded border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 dark:border-slate-600 dark:text-slate-100 dark:hover:bg-slate-800"
          >
            Obnovit
          </button>
        </div>

        {auditQuery.isLoading && <p className="text-sm text-slate-500 dark:text-slate-400">Načítám audit…</p>}
        {auditQuery.isError && <p className="text-sm text-rose-600">Nepodařilo se načíst auditní záznamy.</p>}

        <div className="divide-y divide-slate-200 dark:divide-slate-700">
          {securityAuditEntries.map((entry) => (
            <div key={entry.id} className="py-3">
              <button
                type="button"
                onClick={() => setAuditExpanded((current) => (current === entry.id ? null : entry.id))}
                className="flex w-full items-start justify-between gap-3 text-left"
              >
                <div>
                  <p className="text-sm font-semibold text-slate-900 dark:text-slate-100">
                    {entry.entityType} – {entry.action}
                  </p>
                  <p className="text-xs text-slate-500 dark:text-slate-400">
                    {new Date(entry.performedAtUtc).toLocaleString()} • {entry.performedBy}
                  </p>
                  {entry.changeSummary && (
                    <p className="mt-1 text-xs text-slate-600 dark:text-slate-300">{entry.changeSummary}</p>
                  )}
                </div>
                <span className="text-xs text-slate-500 dark:text-slate-400">{auditExpanded === entry.id ? '▲' : '▼'}</span>
              </button>
              {auditExpanded === entry.id && renderAuditDetails(entry)}
            </div>
          ))}

          {securityAuditEntries.length === 0 && !auditQuery.isLoading && (
            <p className="py-4 text-sm text-slate-500 dark:text-slate-400">Zatím nejsou k dispozici žádné záznamy.</p>
          )}
        </div>
      </section>
    </div>
  );
};
