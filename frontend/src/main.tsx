import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { RouterProvider } from 'react-router'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'

import './index.css'
import { ThemeProvider } from '@/components/ThemeProvider'
import { Toaster } from '@/components/ui/sonner'
import { TooltipProvider } from '@/components/ui/tooltip'
import { ApiError } from '@/lib/api'
import { router } from '@/router'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      // An expired token does not get better by trying again — neither does a
      // missing plan.
      retry: (count, error) =>
        error instanceof ApiError && error.status < 500 ? false : count < 2,
      staleTime: 30 * 1000,
    },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <ThemeProvider>
      <QueryClientProvider client={queryClient}>
        <TooltipProvider>
          <RouterProvider router={router} />
          {/* Rückmeldung nach dem Speichern. Vorher passierte alles still —
              man wusste nur am Ergebnis, ob es geklappt hat. */}
          <Toaster position="bottom-right" />
        </TooltipProvider>
      </QueryClientProvider>
    </ThemeProvider>
  </StrictMode>,
)
