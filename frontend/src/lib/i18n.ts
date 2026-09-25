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
 * A new language is two copied files plus one line in `RESOURCES` — no code
 * beyond that. Missing keys fall back to German.
 *
 * Only German ships for now, so the language is fixed and nothing is detected.
 * Resources are bundled, which makes `init` synchronous: the first render
 * already has every text.
 */

import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'

import de from '@/locales/de.json'
import deHelp from '@/locales/de.help.json'

export const FALLBACK_LANGUAGE = 'de'

export const RESOURCES = {
  de: { translation: de, help: deHelp },
}

if (!i18n.isInitialized) {
  void i18n.use(initReactI18next).init({
    resources: RESOURCES,
    lng: FALLBACK_LANGUAGE,
    fallbackLng: FALLBACK_LANGUAGE,
    ns: ['translation', 'help'],
    defaultNS: 'translation',
    // React escapes on its own.
    interpolation: { escapeValue: false },
    returnNull: false,
  })
}

/** The active language, for `Intl` — never a hard-coded `de-DE`. */
export function locale(): string {
  return i18n.resolvedLanguage ?? i18n.language ?? FALLBACK_LANGUAGE
}

export { i18n }
