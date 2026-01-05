import { FormEvent, useEffect, useMemo, useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  CaptchaConfiguration,
  CaptchaConfigurationRequest,
  LdapConfiguration,
  LdapConfigurationRequest,
  LdapConnectionTestResult,
  LdapRoleMapping,
  OidcConfiguration,
  SmsConnectorConfiguration,
  SmsConnectorRequest,
  SsprConfiguration,
  PasswordPolicyConfiguration,
  UserSummary,
  UserSession,
  createLdapRoleMapping,
  deleteLdapRoleMapping,
  dryRunLdap,
  getCaptchaConfiguration,
  getLdapConfiguration,
  getLdapRoleMappings,
  getOidcConfiguration,
  getSmsConfiguration,
  getSsprConfiguration,
  getPasswordPolicyConfiguration,
  getSecuritySummary,
  getSecurityAlerts,
  updateSecurityAlerts,
  getSecurityMetricsHistory,
  captureSecurityMetrics,
  testLdapConnection,
  getUserSessions,
  updateCaptchaConfiguration,
  updateLdapConfiguration,
  updateLdapRoleMapping,
  updateOidcConfiguration,
  updateSmsConfiguration,
  updateSsprConfiguration,
  updatePasswordPolicyConfiguration,
  searchSecurityUsers,
  revokeSession,
  revokeAllSessions,
  SecuritySummary,
  SecurityAlertConfiguration,
  SecurityMetricSnapshot
} from '../api/security';
import { AuditLogEntry, getAuditLogs } from '../api/audit';
import { HelpTooltip } from '../components/HelpTooltip';

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

const SECURITY_AUDIT_TYPES = [
  'Security.OIDC',
  'Security.LDAP',
  'Security.SSPR',
  'Security.Captcha',
  'Security.SMS',
  'Security.PasswordPolicy',
  'Security.Session',
  'Security.LdapRoleMapping',
  'Security.DataScopes'
];

const ROLE_OPTIONS = [
  { value: 'SuperAdmin', label: 'Super Admin' },
  { value: 'SRV Administrator', label: 'SRV Administrator' },
  { value: 'NET Administrator', label: 'NET Administrator' },
  { value: 'Aplikační admin', label: 'Aplikační admin' },
  { value: 'Helpdesk', label: 'Helpdesk' },
  { value: 'Helpdesk Read', label: 'Helpdesk Read' }
];

interface SecuritySettingsPageProps {
  variant?: 'full' | 'wizard';
}

export const SecuritySettingsPage = ({ variant = 'full' }: SecuritySettingsPageProps) => {
  const queryClient = useQueryClient();
  const [flash, setFlash] = useState<FlashMessage | null>(null);
  const headerDescription =
    variant === 'wizard'
      ? 'V rámci průvodce prvním spuštěním nastavte klíčové bezpečnostní integrace. Změny se uloží okamžitě.'
      : 'Spravujte externí identity, připojení k LDAP/AD a pravidla self-service resetu hesel. Změny se okamžitě zapisují do auditu.';

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

  const [captchaState, setCaptchaState] = useState<CaptchaConfiguration>({
    enabled: false,
    siteKey: '',
    hasSecret: false,
    verificationEndpoint: '',
    bypassToken: ''
  });
  const [captchaSecretInput, setCaptchaSecretInput] = useState('');
  const [captchaRotateSecret, setCaptchaRotateSecret] = useState(false);

  const [smsState, setSmsState] = useState<SmsConnectorConfiguration>({
    id: null,
    alias: 'Primary SMS',
    enabled: false,
    endpoint: '',
    sender: '',
    region: '',
    hasSecret: false,
    healthStatus: undefined,
    lastTestedAtUtc: undefined
  });
  const [smsSecretInput, setSmsSecretInput] = useState('');
  const [smsRotateSecret, setSmsRotateSecret] = useState(false);

  const [passwordPolicyState, setPasswordPolicyState] = useState<PasswordPolicyConfiguration>({
    enabled: true,
    minimumLength: 12,
    requireUppercase: true,
    requireLowercase: true,
    requireDigit: true,
    requireNonAlphanumeric: false,
    expirationDays: 90,
    historyCount: 5,
    lockoutAttempts: 5,
    lockoutDurationMinutes: 15
  });

  const [sessionSearchTerm, setSessionSearchTerm] = useState('');
  const [selectedUser, setSelectedUser] = useState<UserSummary | null>(null);
  const [alertsState, setAlertsState] = useState<SecurityAlertConfiguration>({
    enabled: true,
    minimumTwoFactorAdoptionPercentage: 75,
    lockedAccountThreshold: 5,
    pendingResetThreshold: 10,
    notificationEmails: []
  });
  const [alertEmailsInput, setAlertEmailsInput] = useState('');
  const [metricsWindow, setMetricsWindow] = useState(30);
  const [revokeReason, setRevokeReason] = useState('');

  const [roleMappings, setRoleMappings] = useState<LdapRoleMapping[]>([]);
  const [mappingDrafts, setMappingDrafts] = useState<Record<string, { groupName: string; roleName: string }>>({});
  const [newMapping, setNewMapping] = useState<{ groupName: string; roleName: string }>({
    groupName: '',
    roleName: ROLE_OPTIONS[0].value
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

  const captchaQuery = useQuery({
    queryKey: ['security', 'captcha'],
    queryFn: getCaptchaConfiguration,
    onSuccess: (data: CaptchaConfiguration) => {
      setCaptchaState(data);
      setCaptchaSecretInput('');
      setCaptchaRotateSecret(false);
    }
  });

  const smsQuery = useQuery({
    queryKey: ['security', 'sms'],
    queryFn: getSmsConfiguration,
    onSuccess: (data: SmsConnectorConfiguration | null) => {
      if (data) {
        setSmsState({ ...data });
      } else {
        setSmsState({
          id: null,
          alias: 'Primary SMS',
          enabled: false,
          endpoint: '',
          sender: '',
          region: '',
          hasSecret: false,
          healthStatus: undefined,
          lastTestedAtUtc: undefined
        });
      }
      setSmsSecretInput('');
      setSmsRotateSecret(false);
    }
  });

  const passwordPolicyQuery = useQuery({
    queryKey: ['security', 'password-policy'],
    queryFn: getPasswordPolicyConfiguration,
    onSuccess: (data: PasswordPolicyConfiguration) => {
      setPasswordPolicyState(data);
    }
  });

  const normalizedSearchTerm = sessionSearchTerm.trim();

  const userSearchQuery = useQuery({
    queryKey: ['security', 'user-search', normalizedSearchTerm],
    queryFn: () => searchSecurityUsers(normalizedSearchTerm),
    enabled: normalizedSearchTerm.length === 0 || normalizedSearchTerm.length >= 2
  });

  const sessionsQuery = useQuery({
    queryKey: ['security', 'sessions', selectedUser?.id ?? 'none'],
    queryFn: async () => {
      if (!selectedUser) {
        return [] as UserSession[];
      }
      return getUserSessions(selectedUser.id);
    },
    enabled: Boolean(selectedUser?.id)
  });

  const roleMappingQuery = useQuery({
    queryKey: ['security', 'ldap-role-mappings'],
    queryFn: getLdapRoleMappings,
    onSuccess: (data: LdapRoleMapping[]) => {
      setRoleMappings(data);
      const drafts = data.reduce<Record<string, { groupName: string; roleName: string }>>((acc, mapping) => {
        acc[mapping.id] = { groupName: mapping.groupName, roleName: mapping.roleName };
        return acc;
      }, {});
      setMappingDrafts(drafts);
    }
  });

  const auditQuery = useQuery({
    queryKey: ['audit', 'security'],
    queryFn: async () => {
      const responses = await Promise.all(
        SECURITY_AUDIT_TYPES.map((type) => getAuditLogs({ entityType: type }))
      );
      return responses.flat().sort((a, b) => new Date(b.performedAtUtc).getTime() - new Date(a.performedAtUtc).getTime());
    }
  });

  const summaryQuery = useQuery({
    queryKey: ['security', 'summary'],
    queryFn: getSecuritySummary,
    refetchInterval: 60000
  });
  const summary = summaryQuery.data;

  const alertsQuery = useQuery({
    queryKey: ['security', 'alerts'],
    queryFn: getSecurityAlerts,
    onSuccess: (data) => {
      setAlertsState(data);
      setAlertEmailsInput(data.notificationEmails.join('\n'));
    }
  });

  const metricsHistoryQuery = useQuery({
    queryKey: ['security', 'metrics', metricsWindow],
    queryFn: () => getSecurityMetricsHistory(metricsWindow),
    keepPreviousData: true,
    refetchInterval: 300000
  });
  const metricsHistory = metricsHistoryQuery.data ?? [];

  const renderStatusPill = (label: string, active: boolean) => (
    <span
      key={label}
      className={`inline-flex items-center gap-2 rounded-full px-3 py-1 text-xs font-semibold ${
        active
          ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/60 dark:text-emerald-100'
          : 'bg-amber-100 text-amber-800 dark:bg-amber-900/60 dark:text-amber-100'
      }`}
    >
      <span aria-hidden>{active ? '✅' : '⚠️'}</span>
      {label}
    </span>
  );

  const formatChange = (change?: SecuritySummary['oidcLastChange']) => {
    if (!change || !change.timestampUtc) {
      return '—';
    }
    const date = new Date(change.timestampUtc);
    return `${date.toLocaleString()} • ${change.actor ?? 'neznámý uživatel'}`;
  };

  const showFlash = (message: FlashMessage) => {
    setFlash(message);
    window.setTimeout(() => setFlash(null), 5000);
  };

  const oidcMutation = useMutation({
    mutationFn: updateOidcConfiguration,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['security', 'oidc'] });
      queryClient.invalidateQueries({ queryKey: ['audit', 'security'] });
      queryClient.invalidateQueries({ queryKey: ['security', 'summary'] });
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
      queryClient.invalidateQueries({ queryKey: ['security', 'summary'] });
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
      queryClient.invalidateQueries({ queryKey: ['security', 'summary'] });
      showFlash({ type: 'success', message: 'SSPR nastavení byla uložena.' });
    },
    onError: (error: unknown) => {
      showFlash({ type: 'error', message: error instanceof Error ? error.message : 'Nepodařilo se uložit SSPR nastavení.' });
    }
  });

  const captchaMutation = useMutation({
    mutationFn: updateCaptchaConfiguration,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['security', 'captcha'] });
      queryClient.invalidateQueries({ queryKey: ['audit', 'security'] });
      queryClient.invalidateQueries({ queryKey: ['security', 'summary'] });
      showFlash({ type: 'success', message: 'CAPTCHA nastavení bylo uloženo.' });
    },
    onError: (error: unknown) => {
      showFlash({ type: 'error', message: error instanceof Error ? error.message : 'Nepodařilo se uložit CAPTCHA nastavení.' });
    }
  });

  const smsMutation = useMutation({
    mutationFn: updateSmsConfiguration,
    onSuccess: (data: SmsConnectorConfiguration) => {
      setSmsState(data);
      setSmsSecretInput('');
      setSmsRotateSecret(false);
      queryClient.invalidateQueries({ queryKey: ['security', 'sms'] });
      queryClient.invalidateQueries({ queryKey: ['audit', 'security'] });
      queryClient.invalidateQueries({ queryKey: ['security', 'summary'] });
      showFlash({ type: 'success', message: 'SMS konektor byl uložen.' });
    },
    onError: (error: unknown) => {
      showFlash({ type: 'error', message: error instanceof Error ? error.message : 'Nepodařilo se uložit SMS konektor.' });
    }
  });

  const passwordPolicyMutation = useMutation({
    mutationFn: updatePasswordPolicyConfiguration,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['security', 'password-policy'] });
      queryClient.invalidateQueries({ queryKey: ['audit', 'security'] });
      queryClient.invalidateQueries({ queryKey: ['security', 'summary'] });
      showFlash({ type: 'success', message: 'Politika hesel byla uložena.' });
    },
    onError: (error: unknown) => {
      showFlash({ type: 'error', message: error instanceof Error ? error.message : 'Nepodařilo se uložit politiku hesel.' });
    }
  });

  const alertsMutation = useMutation({
    mutationFn: updateSecurityAlerts,
    onSuccess: (data: SecurityAlertConfiguration) => {
      setAlertsState(data);
      setAlertEmailsInput(data.notificationEmails.join('\n'));
      queryClient.invalidateQueries({ queryKey: ['security', 'alerts'] });
      queryClient.invalidateQueries({ queryKey: ['security', 'summary'] });
      showFlash({ type: 'success', message: 'Bezpečnostní alerty byly uloženy.' });
    },
    onError: (error: unknown) => {
      showFlash({ type: 'error', message: error instanceof Error ? error.message : 'Nepodařilo se uložit bezpečnostní alerty.' });
    }
  });

  const captureMetricsMutation = useMutation({
    mutationFn: captureSecurityMetrics,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['security', 'metrics'] });
      queryClient.invalidateQueries({ queryKey: ['security', 'summary'] });
      showFlash({ type: 'success', message: 'Metriky byly znovu vyhodnoceny.' });
    },
    onError: (error: unknown) => {
      showFlash({ type: 'error', message: error instanceof Error ? error.message : 'Nepodařilo se zachytit metriky.' });
    }
  });

  const revokeSessionMutation = useMutation({
    mutationFn: ({ sessionId, reason }: { sessionId: string; reason?: string }) => revokeSession(sessionId, reason),
    onSuccess: () => {
      if (selectedUser?.id) {
        queryClient.invalidateQueries({ queryKey: ['security', 'sessions', selectedUser.id] });
      }
      queryClient.invalidateQueries({ queryKey: ['audit', 'security'] });
      showFlash({ type: 'success', message: 'Sezení bylo odhlášeno.' });
    },
    onError: (error: unknown) => {
      showFlash({ type: 'error', message: error instanceof Error ? error.message : 'Nepodařilo se odhlásit sezení.' });
    }
  });

  const revokeAllSessionsMutation = useMutation({
    mutationFn: revokeAllSessions,
    onSuccess: () => {
      if (selectedUser?.id) {
        queryClient.invalidateQueries({ queryKey: ['security', 'sessions', selectedUser.id] });
      }
      queryClient.invalidateQueries({ queryKey: ['audit', 'security'] });
      showFlash({ type: 'success', message: 'Všechna sezení byla odhlášena.' });
    },
    onError: (error: unknown) => {
      showFlash({ type: 'error', message: error instanceof Error ? error.message : 'Nepodařilo se odhlásit všechna sezení.' });
    }
  });

  const createMappingMutation = useMutation({
    mutationFn: createLdapRoleMapping,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['security', 'ldap-role-mappings'] });
      queryClient.invalidateQueries({ queryKey: ['audit', 'security'] });
      showFlash({ type: 'success', message: 'Mapování bylo přidáno.' });
    },
    onError: (error: unknown) => {
      showFlash({ type: 'error', message: error instanceof Error ? error.message : 'Nepodařilo se přidat mapování.' });
    }
  });

  const updateMappingMutation = useMutation({
    mutationFn: ({ id, data }: { id: string; data: { groupName: string; roleName: string } }) =>
      updateLdapRoleMapping(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['security', 'ldap-role-mappings'] });
      queryClient.invalidateQueries({ queryKey: ['audit', 'security'] });
      showFlash({ type: 'success', message: 'Mapování bylo aktualizováno.' });
    },
    onError: (error: unknown) => {
      showFlash({ type: 'error', message: error instanceof Error ? error.message : 'Nepodařilo se aktualizovat mapování.' });
    }
  });

  const deleteMappingMutation = useMutation({
    mutationFn: deleteLdapRoleMapping,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['security', 'ldap-role-mappings'] });
      queryClient.invalidateQueries({ queryKey: ['audit', 'security'] });
      showFlash({ type: 'success', message: 'Mapování bylo odstraněno.' });
    },
    onError: (error: unknown) => {
      showFlash({ type: 'error', message: error instanceof Error ? error.message : 'Nepodařilo se odstranit mapování.' });
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

  const handleAlertsSubmit = (event: FormEvent) => {
    event.preventDefault();
    const emails = parseList(alertEmailsInput);
    alertsMutation.mutate({
      ...alertsState,
      notificationEmails: emails
    });
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

  const handlePasswordPolicySubmit = (event: FormEvent) => {
    event.preventDefault();
    passwordPolicyMutation.mutate({ ...passwordPolicyState });
  };

  const handleCaptchaSubmit = (event: FormEvent) => {
    event.preventDefault();
    const secret = captchaSecretInput.trim();
    const payload: CaptchaConfigurationRequest = {
      enabled: captchaState.enabled,
      siteKey: captchaState.siteKey.trim(),
      verificationEndpoint: captchaState.verificationEndpoint.trim(),
      secret: secret ? secret : undefined,
      rotateSecret: captchaRotateSecret || (!!secret && !captchaState.hasSecret),
      bypassToken: captchaState.bypassToken?.trim() ? captchaState.bypassToken.trim() : undefined
    };
    captchaMutation.mutate(payload);
  };

  const handleSmsSubmit = (event: FormEvent) => {
    event.preventDefault();
    const secret = smsSecretInput.trim();
    const payload: SmsConnectorRequest = {
      alias: smsState.alias?.trim() ? smsState.alias.trim() : 'Primary SMS',
      enabled: smsState.enabled,
      endpoint: smsState.endpoint.trim(),
      sender: smsState.sender && smsState.sender.toString().trim() ? smsState.sender.toString().trim() : undefined,
      region: smsState.region && smsState.region.toString().trim() ? smsState.region.toString().trim() : undefined,
      secret: secret ? secret : undefined,
      rotateSecret: smsRotateSecret || (!!secret && !smsState.hasSecret)
    };
    smsMutation.mutate(payload);
  };

  const handleAddMapping = (event: FormEvent) => {
    event.preventDefault();
    const groupName = newMapping.groupName.trim();
    if (!groupName) {
      showFlash({ type: 'error', message: 'Zadejte název AD skupiny.' });
      return;
    }
    createMappingMutation.mutate({ groupName, roleName: newMapping.roleName });
    setNewMapping({ groupName: '', roleName: ROLE_OPTIONS[0].value });
  };

  const handleMappingDraftChange = (id: string, field: 'groupName' | 'roleName', value: string) => {
    setMappingDrafts((current) => ({
      ...current,
      [id]: {
        ...(current[id] ?? { groupName: '', roleName: ROLE_OPTIONS[0].value }),
        [field]: value
      }
    }));
  };

  const handleSaveMapping = (id: string) => {
    const draft = mappingDrafts[id];
    if (!draft) {
      return;
    }
    const groupName = draft.groupName.trim();
    if (!groupName) {
      showFlash({ type: 'error', message: 'Zadejte název AD skupiny.' });
      return;
    }
    updateMappingMutation.mutate({ id, data: { groupName, roleName: draft.roleName } });
  };

  const handleDeleteMapping = (id: string) => {
    deleteMappingMutation.mutate(id);
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

  const handleSelectUser = (user: UserSummary) => {
    setSelectedUser(user);
    setRevokeReason('');
    queryClient.invalidateQueries({ queryKey: ['security', 'sessions', user.id] });
  };

  const handleRevokeSession = (sessionId: string) => {
    revokeSessionMutation.mutate({ sessionId, reason: revokeReason.trim() ? revokeReason.trim() : undefined });
  };

  const handleRevokeAllSessions = () => {
    if (selectedUser) {
      revokeAllSessionsMutation.mutate(selectedUser.id);
    }
  };

  const userResults = userSearchQuery.data ?? [];
  const sessions = sessionsQuery.data ?? [];

  return (
    <div className="space-y-10">
      <header className="flex flex-wrap items-start justify-between gap-4">
        <div>
          <h2 className="text-2xl font-semibold text-slate-900 dark:text-white">Bezpečnostní nastavení</h2>
          <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">{headerDescription}</p>
        </div>
        <HelpTooltip
          manualPath="security.html"
          label="Otevřít kapitolu nápovědy Bezpečnost & 2FA"
        />
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
        <div className="flex flex-wrap items-center justify-between gap-4">
          <div>
            <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Bezpečnostní přehled</h3>
            <p className="text-sm text-slate-500 dark:text-slate-400">
              Shrnutí stavu kritických integrací a připravenosti prostředí. Data se každou minutu automaticky aktualizují.
            </p>
          </div>
          <div
            className={`rounded-full px-4 py-2 text-sm font-semibold ${summary?.criticalReady ? 'bg-emerald-100 text-emerald-800 dark:bg-emerald-900/60 dark:text-emerald-100' : 'bg-amber-100 text-amber-800 dark:bg-amber-900/60 dark:text-amber-100'}`}
          >
            {summary?.criticalReady ? 'Průvodce splněn' : 'Ještě zbývá dokončit'}
          </div>
        </div>
        <div className="mt-4 flex flex-wrap gap-2">
          {renderStatusPill('OIDC', !!summary?.oidcEnabled)}
          {renderStatusPill('LDAP', !!summary?.ldapEnabled)}
          {renderStatusPill('SSPR', !!summary?.ssprEnabled)}
          {renderStatusPill('CAPTCHA', !!summary?.captchaEnabled)}
          {renderStatusPill('SMS konektor', !!summary?.smsEnabled)}
          {renderStatusPill('Password policy', !!summary?.passwordPolicyEnabled)}
        </div>
        <dl className="mt-6 grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          <div>
            <dt className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">Uživatelé s 2FA</dt>
            <dd className="text-lg font-semibold text-slate-900 dark:text-white">
              {summary ? `${summary.twoFactorUsers} / ${summary.totalUsers}` : '…'}
            </dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">Uzamčené účty</dt>
            <dd className="text-lg font-semibold text-slate-900 dark:text-white">{summary ? summary.lockedOutUsers : '…'}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">Poslední změna OIDC</dt>
            <dd className="text-sm text-slate-600 dark:text-slate-300">{formatChange(summary?.oidcLastChange)}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">Poslední změna LDAP</dt>
            <dd className="text-sm text-slate-600 dark:text-slate-300">{formatChange(summary?.ldapLastChange)}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">Poslední změna SSPR</dt>
            <dd className="text-sm text-slate-600 dark:text-slate-300">{formatChange(summary?.ssprLastChange)}</dd>
          </div>
          <div>
            <dt className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">Poslední změna policy</dt>
            <dd className="text-sm text-slate-600 dark:text-slate-300">{formatChange(summary?.passwordPolicyLastChange)}</dd>
          </div>
        </dl>
        {summary?.latestSnapshot && (
          <div className="mt-6 rounded-md border border-slate-200 bg-slate-50 p-4 text-sm dark:border-slate-700 dark:bg-slate-800/60 dark:text-slate-100">
            <div className="flex flex-wrap items-center justify-between gap-4">
              <div>
                <p className="font-semibold">Poslední snapshot: {new Date(summary.latestSnapshot.capturedAtUtc).toLocaleString()}</p>
                <p className="text-slate-600 dark:text-slate-300">
                  2FA: {summary.latestSnapshot.totalUsers > 0 ? ((summary.latestSnapshot.totpEnabled / summary.latestSnapshot.totalUsers) * 100).toFixed(1) : '0.0'}% • Uzamčené účty: {summary.latestSnapshot.lockedOut} • Čekající resety: {summary.latestSnapshot.pendingPasswordResets}
                </p>
              </div>
              {summary.latestSnapshot.alerts.length > 0 ? (
                <span className="inline-flex items-center gap-2 rounded-full bg-rose-100 px-3 py-1 text-xs font-semibold text-rose-700 dark:bg-rose-900/50 dark:text-rose-100">
                  <span aria-hidden>🚨</span>
                  Aktivní alerty ({summary.latestSnapshot.alerts.length})
                </span>
              ) : (
                <span className="inline-flex items-center gap-2 rounded-full bg-emerald-100 px-3 py-1 text-xs font-semibold text-emerald-700 dark:bg-emerald-900/50 dark:text-emerald-100">
                  <span aria-hidden>✅</span>
                  Bez aktivních alertů
                </span>
              )}
            </div>
          </div>
        )}
        {summaryQuery.isLoading && (
          <p className="mt-4 text-xs text-slate-500 dark:text-slate-400">Načítání bezpečnostního přehledu…</p>
        )}
        {summaryQuery.isError && (
          <p className="mt-4 text-xs text-rose-500">Nepodařilo se načíst bezpečnostní přehled.</p>
        )}
      </section>

      <section className="grid gap-6 rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900 lg:grid-cols-2">
        <form onSubmit={handleAlertsSubmit} className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Alerty a prahové hodnoty</h3>
              <p className="text-sm text-slate-500 dark:text-slate-400">
                Nastavte upozornění na nízké pokrytí 2FA, zvýšený počet uzamčených účtů nebo čekajících resetů. Při překročení prahů odejde e-mail na zadané adresy.
              </p>
            </div>
            <label className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                className="h-4 w-4"
                checked={alertsState.enabled}
                onChange={(event) => setAlertsState((state) => ({ ...state, enabled: event.target.checked }))}
              />
              Alerty povoleny
            </label>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Min. adopce 2FA (%)
              <input
                type="number"
                min={0}
                max={100}
                step={1}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                value={alertsState.minimumTwoFactorAdoptionPercentage}
                onChange={(event) =>
                  setAlertsState((state) => ({ ...state, minimumTwoFactorAdoptionPercentage: Number(event.target.value) }))
                }
                required
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Prah uzamčených účtů
              <input
                type="number"
                min={0}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                value={alertsState.lockedAccountThreshold}
                onChange={(event) =>
                  setAlertsState((state) => ({ ...state, lockedAccountThreshold: Number(event.target.value) }))
                }
                required
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Prah čekajících resetů
              <input
                type="number"
                min={0}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                value={alertsState.pendingResetThreshold}
                onChange={(event) =>
                  setAlertsState((state) => ({ ...state, pendingResetThreshold: Number(event.target.value) }))
                }
                required
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Příjemci (po jednom na řádek)
              <textarea
                className="mt-1 h-28 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                value={alertEmailsInput}
                onChange={(event) => setAlertEmailsInput(event.target.value)}
                placeholder="soc@example.com\nsecops@example.com"
              />
            </label>
          </div>

          <div className="flex justify-end gap-3">
            <button
              type="submit"
              className="rounded bg-sky-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-sky-700 disabled:opacity-50"
              disabled={alertsMutation.isPending}
            >
              {alertsMutation.isPending ? 'Ukládám…' : 'Uložit alerty'}
            </button>
          </div>
          {alertsQuery.isError && (
            <p className="text-xs text-rose-500">Nepodařilo se načíst nastavení alertů.</p>
          )}
        </form>

        <div className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Historie bezpečnostních metrik</h3>
              <p className="text-sm text-slate-500 dark:text-slate-400">
                Automatické snapshoty zachycují počty uživatelů, adopci 2FA a případné alerty. Vyberte časové okno a sledujte trend.
              </p>
            </div>
            <div className="flex items-center gap-2">
              <select
                className="rounded border border-slate-300 bg-white px-3 py-1 text-sm text-slate-900 focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                value={metricsWindow}
                onChange={(event) => setMetricsWindow(Number(event.target.value))}
              >
                <option value={7}>7 dní</option>
                <option value={14}>14 dní</option>
                <option value={30}>30 dní</option>
                <option value={90}>90 dní</option>
              </select>
              <button
                type="button"
                onClick={() => captureMetricsMutation.mutate()}
                className="rounded border border-slate-300 px-3 py-1 text-sm font-medium text-slate-700 hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800 disabled:opacity-50"
                disabled={captureMetricsMutation.isPending}
              >
                {captureMetricsMutation.isPending ? 'Probíhá…' : 'Zachytit nyní'}
              </button>
            </div>
          </div>

          {metricsHistoryQuery.isLoading && <p className="text-xs text-slate-500 dark:text-slate-400">Načítání metrik…</p>}
          {metricsHistoryQuery.isError && <p className="text-xs text-rose-500">Nepodařilo se načíst historii metrik.</p>}

          {!metricsHistoryQuery.isLoading && metricsHistory.length === 0 && (
            <p className="text-xs text-slate-500 dark:text-slate-400">Zatím nejsou dostupné žádné záznamy.</p>
          )}

          {metricsHistory.length > 0 && (
            <div className="max-h-72 overflow-auto rounded border border-slate-200 dark:border-slate-700">
              <table className="min-w-full divide-y divide-slate-200 text-left text-sm dark:divide-slate-700">
                <thead className="bg-slate-50 dark:bg-slate-800/60">
                  <tr>
                    <th className="px-3 py-2 font-semibold text-slate-700 dark:text-slate-200">Čas</th>
                    <th className="px-3 py-2 font-semibold text-slate-700 dark:text-slate-200">2FA adopce</th>
                    <th className="px-3 py-2 font-semibold text-slate-700 dark:text-slate-200">Uzamčeno</th>
                    <th className="px-3 py-2 font-semibold text-slate-700 dark:text-slate-200">Reset požadavky</th>
                    <th className="px-3 py-2 font-semibold text-slate-700 dark:text-slate-200">Alerty</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
                  {metricsHistory.map((snapshot: SecurityMetricSnapshot) => {
                    const captured = new Date(snapshot.capturedAtUtc);
                    const adoption = snapshot.totalUsers > 0 ? ((snapshot.totpEnabled / snapshot.totalUsers) * 100).toFixed(1) : '0.0';
                    return (
                      <tr key={snapshot.capturedAtUtc} className="bg-white dark:bg-slate-900">
                        <td className="px-3 py-2 text-slate-600 dark:text-slate-300">{captured.toLocaleString()}</td>
                        <td className="px-3 py-2 text-slate-600 dark:text-slate-300">{adoption}%</td>
                        <td className="px-3 py-2 text-slate-600 dark:text-slate-300">{snapshot.lockedOut}</td>
                        <td className="px-3 py-2 text-slate-600 dark:text-slate-300">{snapshot.pendingPasswordResets}</td>
                        <td className="px-3 py-2 text-slate-600 dark:text-slate-300">
                          {snapshot.alerts.length === 0 ? (
                            <span className="text-xs text-slate-400">Bez alertu</span>
                          ) : (
                            <ul className="space-y-1">
                              {snapshot.alerts.map((alert) => (
                                <li key={`${snapshot.capturedAtUtc}-${alert.code}`} className="flex items-center gap-2 text-xs">
                                  <span aria-hidden>{alert.severity === 'error' ? '🚨' : '⚠️'}</span>
                                  <span>{alert.message}</span>
                                </li>
                              ))}
                            </ul>
                          )}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </section>

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
        <form onSubmit={handlePasswordPolicySubmit} className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Politika hesel a uzamykání účtů</h3>
              <p className="text-sm text-slate-500 dark:text-slate-400">
                Spravujte minimální požadavky na hesla, expiraci a reakci na opakované neúspěšné pokusy.
              </p>
            </div>
            <label className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                className="h-4 w-4"
                checked={passwordPolicyState.enabled}
                onChange={(event) => setPasswordPolicyState((state) => ({ ...state, enabled: event.target.checked }))}
              />
              Vynucovat politiku hesel
            </label>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Minimální délka hesla
              <input
                type="number"
                min={6}
                value={passwordPolicyState.minimumLength}
                onChange={(event) =>
                  setPasswordPolicyState((state) => ({ ...state, minimumLength: Number(event.target.value) }))
                }
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Expirace hesla (dny, 0 = bez expirace)
              <input
                type="number"
                min={0}
                value={passwordPolicyState.expirationDays}
                onChange={(event) =>
                  setPasswordPolicyState((state) => ({ ...state, expirationDays: Number(event.target.value) }))
                }
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Historie hesel (počet uchovaných)
              <input
                type="number"
                min={0}
                value={passwordPolicyState.historyCount}
                onChange={(event) =>
                  setPasswordPolicyState((state) => ({ ...state, historyCount: Number(event.target.value) }))
                }
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Uzamknout po (počet pokusů)
              <input
                type="number"
                min={1}
                value={passwordPolicyState.lockoutAttempts}
                onChange={(event) =>
                  setPasswordPolicyState((state) => ({ ...state, lockoutAttempts: Number(event.target.value) }))
                }
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Doba uzamčení (minuty)
              <input
                type="number"
                min={1}
                value={passwordPolicyState.lockoutDurationMinutes}
                onChange={(event) =>
                  setPasswordPolicyState((state) => ({ ...state, lockoutDurationMinutes: Number(event.target.value) }))
                }
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
              />
            </label>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="inline-flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                className="h-4 w-4"
                checked={passwordPolicyState.requireUppercase}
                onChange={(event) =>
                  setPasswordPolicyState((state) => ({ ...state, requireUppercase: event.target.checked }))
                }
              />
              Vyžadovat velká písmena
            </label>
            <label className="inline-flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                className="h-4 w-4"
                checked={passwordPolicyState.requireLowercase}
                onChange={(event) =>
                  setPasswordPolicyState((state) => ({ ...state, requireLowercase: event.target.checked }))
                }
              />
              Vyžadovat malá písmena
            </label>
            <label className="inline-flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                className="h-4 w-4"
                checked={passwordPolicyState.requireDigit}
                onChange={(event) =>
                  setPasswordPolicyState((state) => ({ ...state, requireDigit: event.target.checked }))
                }
              />
              Vyžadovat číslice
            </label>
            <label className="inline-flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                className="h-4 w-4"
                checked={passwordPolicyState.requireNonAlphanumeric}
                onChange={(event) =>
                  setPasswordPolicyState((state) => ({ ...state, requireNonAlphanumeric: event.target.checked }))
                }
              />
              Vyžadovat speciální znak
            </label>
          </div>

          <div className="flex justify-end">
            <button
              type="submit"
              className="rounded bg-sky-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-sky-700 disabled:opacity-50"
              disabled={passwordPolicyMutation.isPending}
            >
              {passwordPolicyMutation.isPending ? 'Ukládám…' : 'Uložit politiku hesel'}
            </button>
          </div>
        </form>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <form onSubmit={handleCaptchaSubmit} className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">CAPTCHA a ochrana formulářů</h3>
              <p className="text-sm text-slate-500 dark:text-slate-400">Definujte CAPTCHA konektor použitý pro přihlášení a SSPR.</p>
            </div>
            <label className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                className="h-4 w-4"
                checked={captchaState.enabled}
                onChange={(event) => setCaptchaState((state) => ({ ...state, enabled: event.target.checked }))}
              />
              CAPTCHA povolena
            </label>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Site key
              <input
                value={captchaState.siteKey}
                onChange={(event) => setCaptchaState((state) => ({ ...state, siteKey: event.target.value }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="public-site-key"
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Verifikační endpoint
              <input
                value={captchaState.verificationEndpoint}
                onChange={(event) => setCaptchaState((state) => ({ ...state, verificationEndpoint: event.target.value }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="https://captcha.example.com/verify"
              />
            </label>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Bypass token (pro podporu)
              <input
                value={captchaState.bypassToken ?? ''}
                onChange={(event) => setCaptchaState((state) => ({ ...state, bypassToken: event.target.value }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="DEV-BYPASS"
              />
            </label>
            <div className="space-y-2">
              <label className="inline-flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
                <input
                  type="checkbox"
                  className="h-4 w-4"
                  checked={captchaRotateSecret}
                  onChange={(event) => setCaptchaRotateSecret(event.target.checked)}
                />
                Obnovit tajný klíč (aktuálně {captchaState.hasSecret ? 'nastaven' : 'není nastaven'})
              </label>
              <input
                type="password"
                value={captchaSecretInput}
                onChange={(event) => setCaptchaSecretInput(event.target.value)}
                disabled={!captchaRotateSecret && captchaState.hasSecret}
                className="w-full rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 disabled:cursor-not-allowed disabled:bg-slate-100 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:disabled:bg-slate-800/60"
                placeholder="Nový tajný klíč"
              />
            </div>
          </div>

          {captchaQuery.isError && (
            <p className="text-sm text-rose-600">Nepodařilo se načíst aktuální nastavení CAPTCHA.</p>
          )}

          <div className="flex justify-end">
            <button
              type="submit"
              className="rounded bg-sky-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-sky-700 disabled:opacity-50"
              disabled={captchaMutation.isPending || captchaQuery.isLoading}
            >
              {captchaMutation.isPending ? 'Ukládám…' : 'Uložit CAPTCHA'}
            </button>
          </div>
        </form>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <form onSubmit={handleSmsSubmit} className="space-y-4">
          <div className="flex items-center justify-between">
            <div>
              <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">SMS konektor</h3>
              <p className="text-sm text-slate-500 dark:text-slate-400">Používá se pro zasílání OTP a notifikací.</p>
            </div>
            <label className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                className="h-4 w-4"
                checked={smsState.enabled}
                onChange={(event) => setSmsState((state) => ({ ...state, enabled: event.target.checked }))}
              />
              Aktivní konektor
            </label>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Alias konektoru
              <input
                value={smsState.alias ?? ''}
                onChange={(event) => setSmsState((state) => ({ ...state, alias: event.target.value }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="primary-sms"
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Endpoint URL
              <input
                value={smsState.endpoint}
                onChange={(event) => setSmsState((state) => ({ ...state, endpoint: event.target.value }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="https://sms-gateway.example.com/send"
              />
            </label>
          </div>

          <div className="grid gap-4 md:grid-cols-2">
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Sender ID
              <input
                value={smsState.sender ?? ''}
                onChange={(event) => setSmsState((state) => ({ ...state, sender: event.target.value }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="HWINVENTORY"
              />
            </label>
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Region / poznámka
              <input
                value={smsState.region ?? ''}
                onChange={(event) => setSmsState((state) => ({ ...state, region: event.target.value }))}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="EU"
              />
            </label>
          </div>

          <div className="space-y-2">
            <label className="inline-flex items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
              <input
                type="checkbox"
                className="h-4 w-4"
                checked={smsRotateSecret}
                onChange={(event) => setSmsRotateSecret(event.target.checked)}
              />
              Obnovit API token (aktuálně {smsState.hasSecret ? 'nastaven' : 'není nastaven'})
            </label>
            <input
              type="password"
              value={smsSecretInput}
              onChange={(event) => setSmsSecretInput(event.target.value)}
              disabled={!smsRotateSecret && smsState.hasSecret}
              className="w-full rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 disabled:cursor-not-allowed disabled:bg-slate-100 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100 dark:disabled:bg-slate-800/60"
              placeholder="Nový API token"
            />
          </div>

          {smsQuery.isError && (
            <p className="text-sm text-rose-600">Nepodařilo se načíst konfiguraci SMS konektoru.</p>
          )}

          <div className="flex justify-end">
            <button
              type="submit"
              className="rounded bg-sky-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-sky-700 disabled:opacity-50"
              disabled={smsMutation.isPending || smsQuery.isLoading}
            >
              {smsMutation.isPending ? 'Ukládám…' : 'Uložit SMS konektor'}
            </button>
          </div>
        </form>
      </section>

      {variant === 'full' && (
        <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="mb-4">
          <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Správa aktivních relací</h3>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Vyhledejte uživatele, zobrazte jeho aktivní relace a případně je ukončete. Všechny zásahy se zapisují do auditu.
          </p>
        </div>

        <div className="grid gap-6 md:grid-cols-[2fr,1fr]">
          <div className="space-y-3">
            <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
              Vyhledat uživatele (jméno, e-mail nebo login)
              <input
                value={sessionSearchTerm}
                onChange={(event) => setSessionSearchTerm(event.target.value)}
                className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                placeholder="např. jana.novakova"
              />
            </label>
            {normalizedSearchTerm.length === 1 && (
              <p className="text-xs text-slate-500 dark:text-slate-400">Pro vyhledávání zadejte alespoň 2 znaky.</p>
            )}
            <div className="max-h-60 space-y-2 overflow-y-auto rounded border border-slate-200 bg-white p-3 dark:border-slate-700 dark:bg-slate-900">
              {userSearchQuery.isLoading ? (
                <p className="text-sm text-slate-500 dark:text-slate-400">Načítám uživatele…</p>
              ) : userResults.length === 0 ? (
                <p className="text-sm text-slate-500 dark:text-slate-400">Žádný uživatel neodpovídá zadaným kritériím.</p>
              ) : (
                userResults.map((user) => (
                  <button
                    key={user.id}
                    type="button"
                    onClick={() => handleSelectUser(user)}
                    className={`flex w-full flex-col rounded border px-3 py-2 text-left text-sm transition ${
                      selectedUser?.id === user.id
                        ? 'border-sky-500 bg-sky-50 text-sky-900 dark:border-sky-500 dark:bg-sky-900/40 dark:text-sky-100'
                        : 'border-slate-200 hover:border-sky-400 hover:bg-slate-50 dark:border-slate-700 dark:hover:border-sky-500 dark:hover:bg-slate-800'
                    }`}
                  >
                    <span className="font-semibold">{user.displayName}</span>
                    <span className="text-xs text-slate-500 dark:text-slate-400">{user.email || 'bez e-mailu'}</span>
                    <span className="text-xs text-slate-500 dark:text-slate-400">{user.userName}</span>
                  </button>
                ))
              )}
            </div>
          </div>

          <div className="rounded border border-slate-200 bg-slate-50 p-4 text-sm dark:border-slate-700 dark:bg-slate-800 dark:text-slate-200">
            {selectedUser ? (
              <div className="space-y-2">
                <p className="font-semibold text-slate-900 dark:text-slate-100">Vybraný uživatel</p>
                <p>
                  {selectedUser.displayName}
                  <br />
                  <span className="text-xs text-slate-500 dark:text-slate-400">{selectedUser.email || 'bez e-mailu'}</span>
                  <br />
                  <span className="text-xs text-slate-500 dark:text-slate-400">{selectedUser.userName}</span>
                </p>
                <button
                  type="button"
                  onClick={() => setSelectedUser(null)}
                  className="rounded border border-slate-300 px-3 py-1 text-xs font-semibold text-slate-600 transition hover:bg-slate-100 dark:border-slate-600 dark:text-slate-200 dark:hover:bg-slate-700"
                >
                  Zrušit výběr
                </button>
              </div>
            ) : (
              <p className="text-slate-500 dark:text-slate-400">Zvolte uživatele ze seznamu vlevo.</p>
            )}
          </div>
        </div>

        {selectedUser && (
          <div className="mt-6 space-y-4">
            <div className="grid gap-4 md:grid-cols-[2fr,1fr]">
              <label className="flex flex-col text-sm font-medium text-slate-700 dark:text-slate-200">
                Důvod odhlášení (uloží se do auditu)
                <input
                  value={revokeReason}
                  onChange={(event) => setRevokeReason(event.target.value)}
                  className="mt-1 rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
                  placeholder="např. porušení politiky"
                />
              </label>
              <div className="flex items-end justify-end gap-2">
                <button
                  type="button"
                  onClick={handleRevokeAllSessions}
                  disabled={revokeAllSessionsMutation.isPending || sessions.length === 0}
                  className="rounded border border-rose-400 px-3 py-2 text-sm font-semibold text-rose-600 transition hover:bg-rose-50 disabled:cursor-not-allowed disabled:opacity-60 dark:border-rose-600 dark:text-rose-300 dark:hover:bg-rose-900/40"
                >
                  {revokeAllSessionsMutation.isPending ? 'Odhlasuji…' : 'Odhlásit všechna sezení'}
                </button>
              </div>
            </div>

            <div className="overflow-x-auto rounded border border-slate-200 dark:border-slate-700">
              <table className="min-w-full divide-y divide-slate-200 text-sm dark:divide-slate-700">
                <thead className="bg-slate-50 dark:bg-slate-800">
                  <tr>
                    <th className="px-3 py-2 text-left font-semibold text-slate-600 dark:text-slate-300">Sezení</th>
                    <th className="px-3 py-2 text-left font-semibold text-slate-600 dark:text-slate-300">Poslední aktivita</th>
                    <th className="px-3 py-2 text-left font-semibold text-slate-600 dark:text-slate-300">IP</th>
                    <th className="px-3 py-2 text-left font-semibold text-slate-600 dark:text-slate-300">Akce</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-slate-200 dark:divide-slate-700">
                  {sessions.length === 0 ? (
                    <tr>
                      <td colSpan={4} className="px-3 py-4 text-center text-slate-500 dark:text-slate-400">
                        Uživatel nemá žádná aktivní sezení.
                      </td>
                    </tr>
                  ) : (
                    sessions.map((session) => (
                      <tr key={session.id} className="bg-white text-slate-700 dark:bg-slate-900 dark:text-slate-200">
                        <td className="px-3 py-2">
                          <p className="font-mono text-xs">{session.sessionIdentifier}</p>
                          <p className="text-xs text-slate-500 dark:text-slate-400">Vytvořeno: {new Date(session.issuedAtUtc).toLocaleString()}</p>
                        </td>
                        <td className="px-3 py-2 text-xs text-slate-600 dark:text-slate-300">{new Date(session.lastSeenAtUtc).toLocaleString()}</td>
                        <td className="px-3 py-2 text-xs text-slate-600 dark:text-slate-300">{session.ipAddress ?? 'N/A'}</td>
                        <td className="px-3 py-2 text-right">
                          <button
                            type="button"
                            onClick={() => handleRevokeSession(session.id)}
                            disabled={revokeSessionMutation.isPending}
                            className="rounded border border-rose-400 px-3 py-1 text-xs font-semibold text-rose-600 transition hover:bg-rose-50 disabled:cursor-not-allowed disabled:opacity-60 dark:border-rose-600 dark:text-rose-300 dark:hover:bg-rose-900/40"
                          >
                            {revokeSessionMutation.isPending ? 'Odhlasuji…' : 'Odhlásit'}
                          </button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
        )}
        </section>
      )}

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="mb-4">
          <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Mapování AD skupin na role</h3>
          <p className="text-sm text-slate-500 dark:text-slate-400">
            Každé mapování přiřadí uživatelům z konkrétní skupiny odpovídající roli v aplikaci. Všechny změny jsou auditovány.
          </p>
        </div>

        <form onSubmit={handleAddMapping} className="mb-6 grid gap-4 md:grid-cols-[2fr,1fr,auto]">
          <input
            value={newMapping.groupName}
            onChange={(event) => setNewMapping((state) => ({ ...state, groupName: event.target.value }))}
            placeholder="CN=SRV-ADMINS,OU=Groups,DC=example,DC=com"
            className="rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
          />
          <select
            value={newMapping.roleName}
            onChange={(event) => setNewMapping((state) => ({ ...state, roleName: event.target.value }))}
            className="rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100"
          >
            {ROLE_OPTIONS.map((option) => (
              <option key={option.value} value={option.value}>
                {option.label}
              </option>
            ))}
          </select>
          <button
            type="submit"
            className="rounded bg-emerald-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-emerald-700 disabled:opacity-50"
            disabled={createMappingMutation.isPending}
          >
            {createMappingMutation.isPending ? 'Přidávám…' : 'Přidat mapování'}
          </button>
        </form>

        <div className="space-y-4">
          {roleMappingQuery.isLoading && <p className="text-sm text-slate-500 dark:text-slate-400">Načítám mapování…</p>}
          {roleMappingQuery.isError && (
            <p className="text-sm text-rose-600">Nepodařilo se načíst mapování AD skupin.</p>
          )}
          {!roleMappingQuery.isLoading && roleMappings.length === 0 && (
            <p className="text-sm text-slate-500 dark:text-slate-400">Zatím není definováno žádné mapování.</p>
          )}

          {roleMappings.map((mapping) => {
            const draft = mappingDrafts[mapping.id] ?? { groupName: mapping.groupName, roleName: mapping.roleName };
            return (
              <div
                key={mapping.id}
                className="rounded border border-slate-200 bg-slate-50 p-4 shadow-sm dark:border-slate-700 dark:bg-slate-800"
              >
                <div className="grid gap-4 md:grid-cols-[2fr,1fr]">
                  <input
                    value={draft.groupName}
                    onChange={(event) => handleMappingDraftChange(mapping.id, 'groupName', event.target.value)}
                    className="rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
                  />
                  <select
                    value={draft.roleName}
                    onChange={(event) => handleMappingDraftChange(mapping.id, 'roleName', event.target.value)}
                    className="rounded border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 shadow-sm focus:border-sky-500 focus:outline-none focus:ring-2 focus:ring-sky-200 dark:border-slate-700 dark:bg-slate-900 dark:text-slate-100"
                  >
                    {ROLE_OPTIONS.map((option) => (
                      <option key={option.value} value={option.value}>
                        {option.label}
                      </option>
                    ))}
                  </select>
                </div>
                <div className="mt-3 flex justify-end gap-2">
                  <button
                    type="button"
                    onClick={() => handleSaveMapping(mapping.id)}
                    className="rounded bg-sky-600 px-3 py-1.5 text-sm font-medium text-white shadow-sm transition hover:bg-sky-700 disabled:opacity-50"
                    disabled={updateMappingMutation.isPending}
                  >
                    {updateMappingMutation.isPending ? 'Ukládám…' : 'Uložit'}
                  </button>
                  <button
                    type="button"
                    onClick={() => handleDeleteMapping(mapping.id)}
                    className="rounded bg-rose-600 px-3 py-1.5 text-sm font-medium text-white shadow-sm transition hover:bg-rose-700 disabled:opacity-50"
                    disabled={deleteMappingMutation.isPending}
                  >
                    {deleteMappingMutation.isPending ? 'Mažu…' : 'Odstranit'}
                  </button>
                </div>
              </div>
            );
          })}
        </div>
      </section>

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <div className="mb-4 flex items-center justify-between">
          <div>
            <h3 className="text-lg font-semibold text-slate-900 dark:text-slate-100">Audit bezpečnostních změn</h3>
            <p className="text-sm text-slate-500 dark:text-slate-400">Poslední operace na nastaveních OIDC, LDAP, SSPR, CAPTCHA, SMS a mapování rolí.</p>
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
