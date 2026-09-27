import { useTranslation } from 'react-i18next'

/** Behind the label of a field that may stay empty; a required field carries no mark. */
export function OptionalMark() {
  const { t } = useTranslation()
  return <span className="text-muted-foreground font-normal"> ({t('formErrors.optional')})</span>
}
