# Localization Workflow & QA Guide

This guide explains how to add or update localized content for the supplier system and how to validate that changes work in Chinese, English, and Thai.

## Supported locales
- `en` (`en-US`) ¡ª default and fallback locale
- `zh` (`zh-CN`)
- `th` (`th-TH`)

Resource files live in `src/locales/<locale>.json`. Use the same key structure across locales and keep entries sorted logically (grouped by domain such as `common`, `auth`, `header`).

## Adding or updating strings
1. **Capture new copy** in `docs/i18n/backlog.md` before you edit code. Note the path, purpose, and reviewer.
2. **Create keys** in the source locale file (`en.json`). Prefer nested namespaces: `auth.notifications.success` rather than flat keys.
3. **Update other locale files** with translated values. When a translation is pending, duplicate the English text and add a `TODO` comment in backlog.
4. **Reference from code** with `useI18n().t('key.path')`. Never embed literal user-facing text.
5. **Run the build** (`npm run build`) to ensure type checking and bundling succeed.

## Translation workflow
- Source of truth: English (`en.json`).
- Handoff: export the updated keys (e.g., via diff of locale files) and send to translators. Suggested tooling: Lokalise, Crowdin, or shared spreadsheet.
- Review: bilingual product owner verifies terminology for Chinese and Thai before merging.
- Versioning: commit locale updates alongside the feature that depends on them. Tag releases with translation cut-off if batching.

## Locale selection & persistence
- `useLocaleStore` stores the selected locale in `localStorage` (`supplier-system.locale`).
- Changes immediately update `vue-i18n` and rerender active views.
- The store falls back to browser preferences (`navigator.languages`) when no saved value exists.
- `LocaleSwitcher` can be used on any screen. Override its appearance with scoped CSS (`:deep(.locale-select)`) as needed.

## Formatting helpers
Use `src/utils/intl.ts` to format locale-sensitive values:

```ts
import { useLocaleFormatters } from '@/utils/intl'

const { formatDate, formatNumber, formatCurrency } = useLocaleFormatters()

formatDate(new Date(), { dateStyle: 'medium' })
formatNumber(12345.6, { maximumFractionDigits: 2 })
formatCurrency(999, 'THB')
```

These helpers automatically respect the active locale and Thai digit grouping.

## Testing checklist
- Switch through all locales with `LocaleSwitcher` and confirm UI text updates.
- Trigger validation and toast messages (e.g., empty login form) to verify localized copies.
- Verify date/number displays on dashboards once corresponding components consume the new `format*` helpers.
- Run `npm run build` before merging to catch missing imports or typing errors.
- Optional: add Vitest snapshot tests for critical components with mocked `i18n` locales.

## Maintenance tips
- Add a lint rule (future work) to flag hard-coded strings (`eslint-plugin-i18n`).
- Keep locale files alphabetized to reduce merge conflicts.
- Monitor runtime for untranslated keys (`vue-i18n` will emit console warnings in dev mode).
- If backend starts sending localized messages, align keys or surface them through a translation layer.

For outstanding translation tasks or new modules, update `docs/i18n/backlog.md` so the localization backlog stays current.