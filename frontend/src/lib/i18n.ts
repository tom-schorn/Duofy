/**
 * The translation unit.
 *
 * Every visible text lives in a catalog under `src/locales`, never in a
 * component. Two catalogs per language:
 *
 * * `<lang>.json` — the short UI texts, namespace `translation` (the default)
 * * `<lang>.help.json` — the help panel, namespace `help`; long texts with
 *   markers that `Trans` maps onto components
 *
 * A new language is two copied files plus one entry in `RESOURCES` — no code
 * beyond that. Missing keys fall back to German.
 *
 * Only German ships for now, so the language is fixed and nothing is detected.
 * Resources are bundled, which makes `init` synchronous: the first render
 * already has every text.
 */

import i18n, { type InitOptions } from 'i18next'
import { initReactI18next } from 'react-i18next'

import de from '@/locales/de.json'
import deHelp from '@/locales/de.help.json'

export const FALLBACK_LANGUAGE = 'de'

/** Every language the app ships, with both catalogs. */
export const RESOURCES = {
  de: { translation: de, help: deHelp },
}

/**
 * The settings every instance uses — the app's and the one the catalog test
 * builds with a test language. Kept in one place so the test checks what runs.
 */
export const OPTIONS: InitOptions = {
  fallbackLng: FALLBACK_LANGUAGE,
  ns: ['translation', 'help'],
  defaultNS: 'translation',
  // React escapes on its own.
  interpolation: { escapeValue: false },
  returnNull: false,
}

if (!i18n.isInitialized) {
  void i18n.use(initReactI18next).init({
    ...OPTIONS,
    resources: RESOURCES,
    lng: FALLBACK_LANGUAGE,
  })
}

/** The active language, for `Intl` — never a hard-coded `de-DE`. */
export function locale(): string {
  return i18n.resolvedLanguage ?? i18n.language ?? FALLBACK_LANGUAGE
}

export { i18n }
