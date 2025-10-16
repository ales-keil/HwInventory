import React from 'react';

type PlaceholderPageProps = {
  title: string;
  description: string;
};

export const PlaceholderPage: React.FC<PlaceholderPageProps> = ({ title, description }) => (
  <div className="space-y-4 rounded-lg border border-dashed border-slate-300 bg-white p-6 text-slate-700 shadow-sm dark:border-slate-700 dark:bg-slate-900 dark:text-slate-200">
    <h2 className="text-xl font-semibold">{title}</h2>
    <p>{description}</p>
    <p className="text-sm text-slate-500 dark:text-slate-400">
      Scaffold API clients with React Query to fetch data from the secured ASP.NET Core backend. This shell ensures the dark/light
      theme toggle and navigation system are ready for the upcoming modules.
    </p>
  </div>
);
