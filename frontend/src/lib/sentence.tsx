import { Fragment, type ReactNode } from 'react'

/**
 * Fills a catalog sentence's `{word}` placeholders with the sentence dialog's own
 * buttons (issue #215, decision 28: „Satz statt Formular“). The template is one
 * whole string from the catalog; this only substitutes, it never concatenates
 * translated fragments of its own.
 *
 * An unknown token — one with no matching entry in `words` — is left in the
 * output as its literal text, braces included, rather than silently vanishing.
 */
export function fillSentence(template: string, words: Record<string, ReactNode>): ReactNode {
  return template.split(/(\{\w+\})/g).map((part, index) => {
    const match = /^\{(\w+)\}$/.exec(part)
    if (!match) return <Fragment key={index}>{part}</Fragment>
    const word = words[match[1]]
    return <Fragment key={index}>{word ?? part}</Fragment>
  })
}
