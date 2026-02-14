# i18n Backlog

This document tracks user-facing strings that must be localized. Each entry lists the relevant source file, a brief description, representative strings, and current status. Update this table as strings move from hard-coded text to locale keys.

| Path | Area | Representative Strings | Notes | Status |
| --- | --- | --- | --- | --- |
| `src/components/AppHeader.vue` | Global header | "Supplier Management", "Sign out", "Signing out¡­", "Back" | Include dynamic fallbacks like `User`. | Hard-coded |
| `src/App.vue` | Shell chrome | No direct strings, but ensure fallback fonts support Thai glyphs. | Verify layout spacing once translations are in place. | Pending review |
| `src/views/LoginView.vue` | Auth flow | "Please sign in to your account", "Username", "Password", "Need an account? Register as a supplier" | Includes placeholders (`e.g. admin001`) and Element Plus messages ("Please enter both username and password"). | Hard-coded |
| `src/components/ChangePasswordDialog.vue` | Auth/profile | Dialog title, helper copy, validation warnings. | Confirm error messages surfaced from backend. | Hard-coded |
| `src/views/DashboardView.vue` | Executive dashboard | Widget titles ("Total Suppliers"), empty states, tooltips. | Contains date formatting and percentage labels. | Hard-coded |
| `src/views/ApprovalDashboardView.vue` | Approvals | Filters ("Pending Approvals"), status badges, progress copy. | Includes plural-sensitive counts. | Hard-coded |
| `src/views/ApprovalQueueView.vue` | Approvals | Table headers ("Supplier", "Submitted"), action buttons ("Review"). | Sync with shared table components. | Hard-coded |
| `src/views/SupplierDirectoryView.vue` & subcomponents | Supplier directory | Search prompts ("Search suppliers"), filter labels, KPI cards ("Active Suppliers"). | Contains nested components under `supplier-directory`. | Hard-coded |
| `src/components/ProfileWizard.vue` | Supplier onboarding | Step labels, instruction blocks, validation hints. | Long-form paragraphs; check for text expansion. | Hard-coded |
| `src/views/SupplierRegistrationView.vue` & `src/components/SupplierRegistrationForm.vue` | Supplier onboarding | Form field labels, helper text, success messages. | Mix of Element Plus validation rules and custom messages. | Hard-coded |
| `src/views/SupplierView.vue` & dashboard panels | Supplier profile | Section headings ("Company Overview"), tab labels, timeline descriptions. | Several computed status labels. | Hard-coded |
| `src/views/PurchasingGroupsView.vue` | Purchasing groups | Table headings, filters, modal copy. | Verify dynamic role names from backend. | Hard-coded |
| `src/views/RoleHomeView.vue` | Role landing page | Welcome banner, module descriptions. | Includes marketing tone copy. | Hard-coded |
| `src/components/SystemModulesPanel.vue` | Landing modules | Card titles ("Approvals"), CTA buttons ("Go to module"). | Shared on multiple routes. | Hard-coded |
| `src/views/AdminPermissionsView.vue` & related admin views | Admin console | Permission labels, instructions, toast messages. | Ensure resource keys cover nested config groups. | Hard-coded |
| `src/api` service responses | API layer | Error handler messages ("Failed to fetch suppliers") surfaced via `ElMessage`. | Requires alignment with backend message localization. | Hard-coded |
| Backend notifications / exports (TBD) | Server side | Email templates, CSV/PDF headers. | Gather from backend repo when integrating. | Pending discovery |

## Next Actions

- Continue populating representative strings as additional features are audited (reports, scheduled jobs, PDFs).
- Align with backend team to catalog server-originated messages for localization.
- Once strings are externalized, update the `Status` column to reflect progress (e.g., `Externalized`, `In translation`).