import { InformationCircleIcon } from '@heroicons/react/24/outline';
import clsx from 'clsx';

interface HelpTooltipProps {
  /** Relative path to the help center route (e.g. "/help/security"). */
  manualPath: string;
  /** Accessible label describing the linked help topic. */
  label: string;
  /** Optional flag to render a compact inline variant. */
  inline?: boolean;
}

/**
 * Renders a contextual help button that links to the integrated Help Center.
 * The component keeps the markup intentionally simple so it works without
 * additional UI libraries and remains fully accessible for keyboard users.
 */
export function HelpTooltip({ manualPath, label, inline = false }: HelpTooltipProps) {
  const href = manualPath.startsWith('/') ? manualPath : `/docs/manual/${manualPath}`;

  return (
    <a
      href={href}
      target="_blank"
      rel="noreferrer"
      className={clsx(
        'inline-flex items-center rounded-full border border-primary-500 text-primary-500 transition-colors focus:outline-none focus-visible:ring focus-visible:ring-primary-500/50',
        inline ? 'px-1 py-1 text-xs' : 'px-2 py-1 text-sm'
      )}
      aria-label={label}
      title={label}
    >
      <InformationCircleIcon className={clsx('h-4 w-4', inline ? '' : 'mr-1')} aria-hidden="true" />
      {!inline && <span className="font-medium">Nápověda</span>}
    </a>
  );
}

export default HelpTooltip;
