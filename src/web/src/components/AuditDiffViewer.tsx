import { useMemo } from 'react';
import { AuditLogEntry } from '../api/audit';

type DiffRow = {
  field: string;
  original?: unknown;
  current?: unknown;
};

type ViewMode = 'update' | 'create' | 'delete' | 'unknown';

const formatValue = (value: unknown): string => {
  if (value === null || value === undefined) {
    return '—';
  }

  if (typeof value === 'string') {
    if (value.length === 0) {
      return '—';
    }

    const parsedDate = Date.parse(value);
    if (!Number.isNaN(parsedDate) && value.length >= 19 && value.includes('T')) {
      return new Date(parsedDate).toLocaleString();
    }

    return value;
  }

  if (typeof value === 'number' || typeof value === 'boolean' || typeof value === 'bigint') {
    return value.toString();
  }

  if (Array.isArray(value)) {
    if (value.length === 0) {
      return '[]';
    }

    return value.map((item) => formatValue(item)).join(', ');
  }

  try {
    return JSON.stringify(value, null, 2);
  } catch (error) {
    return String(value);
  }
};

const normaliseField = (field: string) => {
  if (!field) {
    return field;
  }

  // split camelCase / PascalCase into spaced words
  const withSpaces = field
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/_/g, ' ') // handle snake_case just in case
    .trim();

  return withSpaces.charAt(0).toUpperCase() + withSpaces.slice(1);
};

const toRows = (entry: AuditLogEntry): { mode: ViewMode; rows: DiffRow[] } => {
  if (!entry.changedFieldsJson) {
    return { mode: 'unknown', rows: [] };
  }

  try {
    const parsed = JSON.parse(entry.changedFieldsJson) as Record<string, unknown>;

    if (entry.action === 'Update') {
      const rows: DiffRow[] = Object.entries(parsed).map(([field, value]) => {
        if (
          value &&
          typeof value === 'object' &&
          'Original' in (value as Record<string, unknown>) &&
          'Current' in (value as Record<string, unknown>)
        ) {
          const castValue = value as { Original?: unknown; Current?: unknown };
          return {
            field,
            original: castValue.Original,
            current: castValue.Current,
          };
        }

        return {
          field,
          original: undefined,
          current: value,
        };
      });

      return { mode: 'update', rows };
    }

    if (entry.action === 'Create') {
      const rows: DiffRow[] = Object.entries(parsed).map(([field, value]) => ({
        field,
        current: value,
      }));

      return { mode: 'create', rows };
    }

    if (entry.action === 'Delete') {
      const rows: DiffRow[] = Object.entries(parsed).map(([field, value]) => ({
        field,
        original: value,
      }));

      return { mode: 'delete', rows };
    }

    const rows: DiffRow[] = Object.entries(parsed).map(([field, value]) => ({
      field,
      current: value,
    }));

    return { mode: 'unknown', rows };
  } catch (error) {
    return { mode: 'unknown', rows: [] };
  }
};

const modeLabel: Record<ViewMode, string> = {
  update: 'Změněná pole',
  create: 'Vytvořená pole',
  delete: 'Smazaná pole',
  unknown: 'Detaily změn',
};

interface AuditDiffViewerProps {
  entry: AuditLogEntry;
}

export const AuditDiffViewer = ({ entry }: AuditDiffViewerProps) => {
  const { mode, rows } = useMemo(() => toRows(entry), [entry]);

  if (!entry.changedFieldsJson) {
    return entry.changeSummary ? (
      <span className="text-xs text-slate-600 dark:text-slate-300">{entry.changeSummary}</span>
    ) : (
      <span className="text-xs text-slate-500 dark:text-slate-400">Žádné detaily</span>
    );
  }

  if (rows.length === 0) {
    return (
      <details className="rounded border border-slate-200 bg-slate-50 p-3 text-xs dark:border-slate-700 dark:bg-slate-900/40">
        <summary className="cursor-pointer font-semibold text-slate-700 dark:text-slate-200">
          {modeLabel[mode]}
        </summary>
        <pre className="mt-2 whitespace-pre-wrap break-words text-slate-600 dark:text-slate-300">
          {entry.changedFieldsJson}
        </pre>
      </details>
    );
  }

  return (
    <details className="rounded border border-slate-200 bg-slate-50 text-xs dark:border-slate-700 dark:bg-slate-900/40">
      <summary className="cursor-pointer px-3 py-2 font-semibold text-slate-700 dark:text-slate-200">
        {modeLabel[mode]} ({rows.length})
      </summary>
      <div className="overflow-x-auto">
        <table className="min-w-full divide-y divide-slate-200 text-left dark:divide-slate-700">
          <thead className="bg-slate-100 uppercase tracking-wide text-[11px] text-slate-600 dark:bg-slate-800 dark:text-slate-300">
            <tr>
              <th className="px-3 py-2">Pole</th>
              {mode !== 'create' && <th className="px-3 py-2">Původní hodnota</th>}
              {mode !== 'delete' && <th className="px-3 py-2">Nová hodnota</th>}
            </tr>
          </thead>
          <tbody className="divide-y divide-slate-200 dark:divide-slate-800">
            {rows.map((row) => (
              <tr key={row.field}>
                <td className="px-3 py-2 font-medium text-slate-700 dark:text-slate-200">
                  {normaliseField(row.field)}
                </td>
                {mode !== 'create' && (
                  <td className="px-3 py-2 align-top text-slate-600 dark:text-slate-300">
                    <code className="whitespace-pre-wrap break-words">{formatValue(row.original)}</code>
                  </td>
                )}
                {mode !== 'delete' && (
                  <td className="px-3 py-2 align-top text-slate-600 dark:text-slate-300">
                    <code className="whitespace-pre-wrap break-words">{formatValue(row.current)}</code>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </details>
  );
};

export default AuditDiffViewer;
