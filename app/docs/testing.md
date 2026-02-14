# Testing Guide

This project currently supports two layers of automated verification: unit tests (Vitest) and end-to-end tests (Playwright). Use the following steps to run regression suites locally or in CI.

## Environment Setup

- Install dependencies: `npm install`
- Install browser binaries for Playwright: `npx playwright install`

## Unit Tests (Vitest)

```bash
npm test
```

Use `npm run test -- --watch` during development for continuous feedback.

## End-to-End Tests (Playwright)

```bash
npm run test:e2e
```

Specs live under `tests/e2e`. Pass Playwright CLI flags (for example `--project=chromium`, `--reporter=html`) to customise runs.

## Recommended CI Sequence

1. `npm ci`
2. `npm run lint:locales` (optional - keeps locale keys aligned)
3. `npm test`
4. `npm run build`
5. `npm run test:e2e`

Split unit and e2e steps across jobs if you want faster feedback while preserving coverage.
