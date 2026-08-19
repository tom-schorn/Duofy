import { createBrowserRouter, Navigate } from 'react-router'

import { RequireAuth } from '@/components/RequireAuth'
import { AppLayout } from '@/layouts/AppLayout'
import { LoginPage } from '@/pages/LoginPage'
import { RegisterPage } from '@/pages/RegisterPage'
import { PlansPage } from '@/pages/PlansPage'
import { PlanDetailPage } from '@/pages/PlanDetailPage'
import { AccountsPage } from '@/pages/AccountsPage'
import { BookPage } from '@/pages/BookPage'
import { CommitmentsPage } from '@/pages/CommitmentsPage'
import { HouseholdPage } from '@/pages/HouseholdPage'
import { ImportPage } from '@/pages/ImportPage'
import { NotFoundPage } from '@/pages/NotFoundPage'

export const router = createBrowserRouter([
  { path: '/', element: <Navigate to="/plan" replace /> },

  { path: '/login', element: <LoginPage /> },
  { path: '/register', element: <RegisterPage /> },

  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppLayout />,
        children: [
          // Overview of every period; one click opens a month in detail.
          { path: '/plan', element: <PlansPage /> },
          { path: '/plan/:year/:month', element: <PlanDetailPage /> },

          // One page for every commitment — savings plans and loans are
          // commitments too. Grouped by block, not by type.
          { path: '/contracts', element: <CommitmentsPage /> },
          // Das Buch steht für sich: eine Buchung gehört zu einem Konto,
          // nicht zu einem Plan. Im Monatsplan bleibt es als Tab, weil man
          // beim Planen hineinschauen will.
          { path: '/book', element: <BookPage /> },
          { path: '/accounts', element: <AccountsPage /> },

          // Bank files in, bookings out. Its own page because CSV and the bank
          // connection will arrive here too, and because it is a place things
          // are allowed to lie around in.
          { path: '/import', element: <ImportPage /> },

          { path: '/household', element: <HouseholdPage /> },
        ],
      },
    ],
  },

  { path: '*', element: <NotFoundPage /> },
])
