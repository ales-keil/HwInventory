import { appConfig } from '../config';
import { getManualEntry } from '../api/help';

interface HelpLinkProps {
  topic: string;
  label?: string;
  className?: string;
}

export function HelpLink({ topic, label = 'Nápověda', className = '' }: HelpLinkProps) {
  const entry = getManualEntry(topic);
  if (!entry) {
    return null;
  }

  const href = appConfig.minimalMode ? entry.url : `/help?topic=${encodeURIComponent(topic)}`;
  const baseClasses =
    'inline-flex items-center gap-1 rounded border border-slate-300 px-2 py-1 text-xs font-medium text-slate-600 transition hover:bg-slate-100 focus:outline-none focus:ring focus:ring-indigo-500 focus:ring-offset-1 dark:border-slate-600 dark:text-slate-200 dark:hover:bg-slate-800';

  return (
    <a
      href={href}
      target="_blank"
      rel="noreferrer noopener"
      className={`${baseClasses} ${className}`.trim()}
      title={`Otevřít nápovědu: ${entry.title}`}
      aria-label={`Otevřít nápovědu: ${entry.title}`}
    >
      <span aria-hidden="true">❓</span>
      <span>{label}</span>
    </a>
  );
}
