/**
 * Wrapper migration script to expose the API migration utility at repository root.
 * Delegates to apps/api/scripts/migrate-add-state-history-tables.js so existing docs keep working.
 */

require('../apps/api/scripts/migrate-add-state-history-tables.js')
