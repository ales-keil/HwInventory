import { useEffect, useState } from 'react';
import { useSearchParams } from 'react-router-dom';
import { fetchManualIndex, ManualEntry } from '../api/help';

export function HelpCenterPage() {
  const [items, setItems] = useState<ManualEntry[]>([]);
  const [selected, setSelected] = useState<ManualEntry | null>(null);
  const [content, setContent] = useState<string>('');
  const [error, setError] = useState<string>('');
  const [searchParams, setSearchParams] = useSearchParams();

  const loadEntryContent = (entry: ManualEntry) => {
    setSelected(entry);
    fetch(entry.url)
      .then((res) => res.text())
      .then((text) => setContent(text))
      .catch((err) => setError(err.message));
  };

  const selectEntry = (entry: ManualEntry, updateQuery = true) => {
    if (updateQuery) {
      setSearchParams({ topic: entry.slug });
    }
    loadEntryContent(entry);
  };

  useEffect(() => {
    fetchManualIndex()
      .then((index) => {
        setItems(index);
        if (index.length === 0) {
          return;
        }

        const topicParam = searchParams.get('topic');
        const matchingEntry = topicParam ? index.find((entry) => entry.slug === topicParam) : undefined;
        if (matchingEntry) {
          loadEntryContent(matchingEntry);
        } else {
          selectEntry(index[0]);
        }
      })
      .catch((err) => setError(err.message));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (items.length === 0) {
      return;
    }
    const topicParam = searchParams.get('topic');
    if (!topicParam) {
      return;
    }
    if (selected?.slug === topicParam) {
      return;
    }
    const entry = items.find((item) => item.slug === topicParam);
    if (entry) {
      loadEntryContent(entry);
    }
  }, [items, searchParams, selected]);

  return (
    <div className="grid grid-cols-1 gap-6 lg:grid-cols-4">
      <div className="rounded border border-neutral-200 bg-white p-4 shadow-sm dark:border-neutral-700 dark:bg-neutral-900 lg:col-span-1">
        <h2 className="mb-3 text-lg font-semibold">Obsah nápovědy</h2>
        <ul className="space-y-2">
          {items.map((entry) => (
            <li key={entry.slug}>
              <button
                onClick={() => selectEntry(entry)}
                className={`w-full rounded px-2 py-1 text-left text-sm transition ${selected?.slug === entry.slug ? 'bg-indigo-600 text-white' : 'hover:bg-neutral-100 dark:hover:bg-neutral-700 dark:text-neutral-100'}`}
              >
                <div className="font-semibold">{entry.title}</div>
                <div className="text-xs text-neutral-500 dark:text-neutral-300">{entry.description}</div>
              </button>
            </li>
          ))}
        </ul>
      </div>
      <div className="rounded border border-neutral-200 bg-white p-4 shadow-sm dark:border-neutral-700 dark:bg-neutral-900 lg:col-span-3">
        <h2 className="mb-3 text-lg font-semibold">{selected?.title ?? 'Vyberte kapitolu'}</h2>
        {error && <div className="mb-2 text-sm text-red-600">{error}</div>}
        {!error && (
          <iframe
            srcDoc={content}
            title={selected?.title ?? 'Help content'}
            className="h-[70vh] w-full rounded border border-neutral-200 dark:border-neutral-700"
          />
        )}
      </div>
    </div>
  );
}
