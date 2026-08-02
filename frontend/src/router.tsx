import { createBrowserRouter, Navigate } from 'react-router'

import { RequireAuth } from '@/components/RequireAuth'
import { AppLayout } from '@/layouts/AppLayout'
import { LoginPage } from '@/pages/LoginPage'
import { RegisterPage } from '@/pages/RegisterPage'
import { PlansPage } from '@/pages/PlansPage'
import { PlanDetailPage } from '@/pages/PlanDetailPage'
import { AccountsPage } from '@/pages/AccountsPage'
import { CommitmentsPage } from '@/pages/CommitmentsPage'
import { HouseholdPage } from '@/pages/HouseholdPage'
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
          // Übersicht aller Perioden, ein Klick öffnet den Monat im Detail.
          { path: '/plan', element: <PlansPage /> },
          { path: '/plan/:year/:month', element: <PlanDetailPage /> },

          // Eine Seite für alle Verträge — Sparpläne und Kredite sind auch
          // Verträge. Gruppiert wird nach Budget, nicht nach Typ.
          { path: '/contracts', element: <CommitmentsPage /> },
          { path: '/accounts', element: <AccountsPage /> },

          { path: '/household', element: <HouseholdPage /> },
        ],
      },
    ],
  },

  { path: '*', element: <NotFoundPage /> },
])
