# ComicWeb — Database Backup Strategy

> **Last Updated**: 2026-08-07  
> **Database**: PostgreSQL (hosted on Render or local)  
> **Maintained by**: Infrastructure/DevOps Owner

---

## 1. Overview

This document defines the backup strategy, schedule, retention policy, and restore procedure for the ComicWeb PostgreSQL database.

A backup that has not been tested for restore is **not a backup**.

---

## 2. Production Environment

### 2.1 Render PostgreSQL (Managed)

If using **Render PostgreSQL** (managed database):

- Render provides **daily automated backups** with 7-day retention on paid plans.
- Point-in-time recovery is available depending on tier.
- Access backups via: Render Dashboard → Database → Backups tab.

**Action Required**: Verify backup retention is enabled and configure alerts for failed backups.

### 2.2 Self-hosted PostgreSQL (VPS/Docker)

If hosting PostgreSQL yourself:

| Property | Value |
|---|---|
| Tool | `pg_dump` |
| Format | Custom binary (`-Fc`) for smaller size and selective restore |
| Schedule | Daily at 02:00 UTC (non-peak hours) |
| Retention | 7 daily backups, 4 weekly snapshots |
| Storage | Off-server (e.g., AWS S3, Cloudflare R2, or Google Cloud Storage) |

---

## 3. Backup Command

```bash
# Full database dump
pg_dump \
  --host=<DB_HOST> \
  --port=<DB_PORT> \
  --username=<DB_USER> \
  --dbname=ComicWeb \
  --format=custom \
  --compress=9 \
  --file="/backups/comicweb_$(date +%Y%m%d_%H%M%S).pgdump"
```

Environment variables should store credentials — **never hardcode in scripts**.

```bash
PGPASSWORD=$DB_PASSWORD pg_dump ...
```

---

## 4. Backup Schedule (Cron)

```cron
# Daily backup at 02:00 UTC
0 2 * * * /opt/scripts/backup_comicweb.sh >> /var/log/comicweb_backup.log 2>&1
```

---

## 5. Retention Policy

| Type | Retention |
|---|---|
| Daily backups | 7 days |
| Weekly backups (Sunday) | 4 weeks |
| Monthly backups (1st of month) | 3 months |

Old backups should be pruned automatically by the backup script.

---

## 6. Restore Procedure

> [!CAUTION]
> A restore to production MUST be approved and coordinated. Always test on a staging environment first.

### 6.1 List Available Backups

```bash
ls -lh /backups/comicweb_*.pgdump | sort -k6
```

### 6.2 Restore to Staging (Test First)

```bash
# Create a fresh staging database
createdb comicweb_restore_test

# Restore from dump
pg_restore \
  --host=<STAGING_HOST> \
  --port=5432 \
  --username=<DB_USER> \
  --dbname=comicweb_restore_test \
  --no-owner \
  --role=<DB_USER> \
  /backups/comicweb_YYYYMMDD_HHMMSS.pgdump

# Verify: check row counts
psql -d comicweb_restore_test -c "SELECT count(*) FROM \"Stories\";"
psql -d comicweb_restore_test -c "SELECT count(*) FROM \"Chapters\";"
psql -d comicweb_restore_test -c "SELECT count(*) FROM \"Users\";"
```

### 6.3 Restore to Production

```bash
# STOP the application first to prevent writes during restore

# Drop and recreate production database (DESTRUCTIVE)
dropdb ComicWeb
createdb ComicWeb

# Restore
pg_restore \
  --host=<PROD_HOST> \
  --port=5432 \
  --username=<DB_USER> \
  --dbname=ComicWeb \
  --no-owner \
  /backups/comicweb_YYYYMMDD_HHMMSS.pgdump

# Re-apply EF Core migrations if needed
dotnet ef database update --project src/2.Infrastructure/ComicWeb.Persistence

# START the application
```

---

## 7. Verification Checklist (After Each Restore Test)

- [ ] All tables present
- [ ] Row counts match pre-backup counts (within tolerance)
- [ ] Application boots and connects to restored DB
- [ ] Login works
- [ ] Public story listing returns data
- [ ] Admin panel accessible

---

## 8. Monitoring & Alerts

- Set up an alert if the daily backup cron job **fails**.
- Log backup file size — a significantly smaller file may indicate a partial backup.
- Review backup logs weekly.

---

## 9. Security

- Backup files must be **encrypted at rest** when stored offsite.
- Use `gpg` or cloud-provider encryption (S3 SSE, R2 encryption).
- Restrict access to backup storage to operations team only.
- **Never commit backup files or connection strings to version control.**

---

## 10. Restore Test Schedule

| Frequency | Environment | Who |
|---|---|---|
| Monthly | Staging | DevOps |
| Before major releases | Staging | DevOps + Dev Lead |
| After production incident | Staging first, then Production | DevOps + Dev Lead |

**Restore tests must be documented.** Record: date, backup used, restore time, verification result.
