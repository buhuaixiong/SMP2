# Backups Directory

This folder is intentionally empty in git. Database backups should be written outside the repository (e.g. `BACKUP_DIR` env var) or downloaded from secure storage when needed.

To create a local backup, run `node scripts/backup-database.js backup` and set `BACKUP_DIR` to a writable path outside the repo (e.g. `%TEMP%\supplier-backups`).
