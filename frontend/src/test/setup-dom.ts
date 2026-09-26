import '@testing-library/jest-dom/vitest'
import { cleanup } from '@testing-library/react'
import { afterEach } from 'vitest'

// Testing Library only cleans up on its own when the test globals are on; here
// they are not, so unmount after every test by hand.
afterEach(cleanup)
