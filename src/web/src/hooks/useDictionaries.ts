import { useEffect, useMemo, useState } from 'react';
import { DictionaryEntry, getDictionaryEntries } from '../api/dictionaries';

type DictionaryMap = Record<string, DictionaryEntry[]>;

const dictionaryCache = new Map<string, DictionaryEntry[]>();
const inflightRequests = new Map<string, Promise<DictionaryEntry[]>>();

const buildStateFromCache = (types: string[]): DictionaryMap => {
  const result: DictionaryMap = {};
  types.forEach((type) => {
    const cached = dictionaryCache.get(type);
    if (cached) {
      result[type] = cached;
    }
  });
  return result;
};

export const useDictionaries = (types: string[]) => {
  const normalizedTypes = useMemo(() => {
    const unique = Array.from(new Set(types.map((type) => type.trim()).filter((type) => type.length > 0)));
    unique.sort();
    return unique;
  }, [types]);

  const [state, setState] = useState<DictionaryMap>(() => buildStateFromCache(normalizedTypes));
  const [loading, setLoading] = useState(() =>
    normalizedTypes.some((type) => !dictionaryCache.has(type))
  );
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const load = async () => {
      if (normalizedTypes.length === 0) {
        setState({});
        setLoading(false);
        setError(null);
        return;
      }

      const missing = normalizedTypes.filter((type) => !dictionaryCache.has(type));
      if (missing.length === 0) {
        setState(buildStateFromCache(normalizedTypes));
        setLoading(false);
        setError(null);
        return;
      }

      setLoading(true);
      try {
        const results = await Promise.all(
          missing.map(async (type) => {
            let request = inflightRequests.get(type);
            if (!request) {
              request = getDictionaryEntries(type);
              inflightRequests.set(type, request);
            }
            const entries = await request;
            dictionaryCache.set(type, entries);
            inflightRequests.delete(type);
            return { type, entries } as const;
          })
        );

        if (cancelled) {
          return;
        }

        const updated: DictionaryMap = { ...buildStateFromCache(normalizedTypes) };
        results.forEach(({ type, entries }) => {
          updated[type] = entries;
        });
        setState(updated);
        setError(null);
      } catch (err) {
        missing.forEach((type) => inflightRequests.delete(type));
        if (!cancelled) {
          setError((err as Error).message);
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    };

    void load();

    return () => {
      cancelled = true;
    };
  }, [normalizedTypes]);

  const refresh = async () => {
    if (normalizedTypes.length === 0) {
      setState({});
      return;
    }

    const refreshedEntries = await Promise.all(
      normalizedTypes.map(async (type) => {
        const entries = await getDictionaryEntries(type);
        dictionaryCache.set(type, entries);
        return { type, entries } as const;
      })
    );

    const nextState: DictionaryMap = {};
    normalizedTypes.forEach((type) => {
      const cached = dictionaryCache.get(type);
      if (cached) {
        nextState[type] = cached;
      }
    });

    refreshedEntries.forEach(({ type, entries }) => {
      nextState[type] = entries;
    });

    setState(nextState);
    setError(null);
    setLoading(false);
  };

  return { dictionaries: state, loading, error, refresh };
};
