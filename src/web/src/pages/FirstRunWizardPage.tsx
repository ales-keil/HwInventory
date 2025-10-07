import { useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { SecuritySettingsPage } from './SecuritySettingsPage';

const stepDefinitions = [
  {
    key: 'intro',
    title: 'Vítejte v průvodci prvním spuštěním',
    description:
      'Před tím, než začnete spravovat inventář, nastavte kritické bezpečnostní integrace. Průvodce je možné kdykoli znovu otevřít v menu Nastavení.',
    content: (
      <div className="space-y-4 text-sm text-slate-600 dark:text-slate-300">
        <p>
          Tento průvodce vás provede konfigurací externí autentizace, LDAP/AD připojení a self-service resetu hesel. Všechny
          změny se ukládají okamžitě a zapisují se do auditu.
        </p>
        <p>
          Připravte si přístupové údaje k OIDC providerovi, LDAP/AD serveru, SMTP a případným SMS/CAPTCHA konektorům. Každý krok
          lze přeskočit a později dokončit v Nastavení.
        </p>
      </div>
    )
  },
  {
    key: 'security',
    title: 'Bezpečnostní nastavení',
    description:
      'Zadejte údaje pro OIDC, LDAP/AD, SSPR, CAPTCHA a SMS konektory. Rozhraní níže je shodné s administrací Nastavení → Security.',
    content: <SecuritySettingsPage variant="wizard" />
  },
  {
    key: 'summary',
    title: 'Hotovo',
    description: 'Základní bezpečnostní integrace jsou připraveny. Doporučujeme pokračovat konfigurací zbylých modulů aplikace.',
    content: (
      <div className="space-y-4 text-sm text-slate-600 dark:text-slate-300">
        <p>
          Konfigurace byla dokončena. V Nastavení můžete kdykoli upravit jednotlivé konektory, přidat další mapování AD skupin
          nebo sledovat auditní záznamy.
        </p>
        <p>
          Pokračujte na dashboard, kde najdete souhrn aktivovaných modulů a další kroky pro nasazení platformy HW Inventory.
        </p>
      </div>
    )
  }
];

export const FirstRunWizardPage = () => {
  const navigate = useNavigate();
  const [activeStep, setActiveStep] = useState(0);

  const clampedStep = useMemo(() => Math.min(Math.max(activeStep, 0), stepDefinitions.length - 1), [activeStep]);
  const step = stepDefinitions[clampedStep];

  const goNext = () => {
    if (clampedStep < stepDefinitions.length - 1) {
      setActiveStep((current) => current + 1);
    }
  };

  const goPrevious = () => {
    if (clampedStep > 0) {
      setActiveStep((current) => current - 1);
    }
  };

  const finish = () => {
    navigate('/');
  };

  return (
    <div className="space-y-8">
      <header className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        <h1 className="text-2xl font-semibold text-slate-900 dark:text-white">Průvodce prvním spuštěním</h1>
        <p className="mt-2 text-sm text-slate-600 dark:text-slate-300">
          Krok {clampedStep + 1} z {stepDefinitions.length} – {step.title}
        </p>
        <p className="mt-3 text-sm text-slate-600 dark:text-slate-300">{step.description}</p>
      </header>

      <nav className="flex flex-wrap gap-3">
        {stepDefinitions.map((definition, index) => (
          <button
            key={definition.key}
            type="button"
            onClick={() => setActiveStep(index)}
            className={`rounded px-3 py-1 text-sm transition ${
              index === clampedStep
                ? 'bg-sky-600 text-white shadow'
                : 'border border-slate-300 text-slate-700 hover:bg-slate-100 dark:border-slate-700 dark:text-slate-200 dark:hover:bg-slate-800'
            }`}
          >
            {index + 1}. {definition.title}
          </button>
        ))}
      </nav>

      <section className="rounded-lg border border-slate-200 bg-white p-6 shadow-sm dark:border-slate-800 dark:bg-slate-900">
        {step.content}
      </section>

      <div className="flex justify-between">
        <button
          type="button"
          onClick={goPrevious}
          disabled={clampedStep === 0}
          className="rounded border border-slate-300 px-4 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50 dark:border-slate-600 dark:text-slate-100 dark:hover:bg-slate-800"
        >
          Zpět
        </button>
        {clampedStep < stepDefinitions.length - 1 ? (
          <button
            type="button"
            onClick={goNext}
            className="rounded bg-sky-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-sky-700"
          >
            Pokračovat
          </button>
        ) : (
          <button
            type="button"
            onClick={finish}
            className="rounded bg-emerald-600 px-4 py-2 text-sm font-semibold text-white shadow-sm transition hover:bg-emerald-700"
          >
            Dokončit
          </button>
        )}
      </div>
    </div>
  );
};
