import { Navigate, Outlet, useLocation } from 'react-router-dom';
import { useSession } from './SessionProvider';

export const SessionGuard = () => {
  const location = useLocation();
  const { user, isLoading, error } = useSession();

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-slate-100 dark:bg-slate-950">
        <div className="text-slate-600 dark:text-slate-300">Načítám relaci…</div>
      </div>
    );
  }

  if (error) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-red-50 p-6 text-center text-red-600">
        <div>
          <p className="text-lg font-semibold">Nepodařilo se načíst relaci.</p>
          <p className="mt-2 text-sm text-red-500">{error.message}</p>
        </div>
      </div>
    );
  }

  if (!user) {
    return <Navigate to="/login" replace state={{ from: location }} />;
  }

  return <Outlet />;
};
