# Security rotation runbook

## Scope

Treat the historical database credential, JWT signing key, and bootstrap administrator password as compromised because they were previously committed to Git history. Do not reuse any of them in any environment.

## Rotate now

1. Create a new least-privilege PostgreSQL application role and a new strong password. Update the deployment secret `ConnectionStrings__DefaultConnection`, then revoke the old role/password after validating application connectivity.
2. Generate a new random JWT signing key of at least 32 bytes. Set it as `Jwt__SigningKey` in the secret store and restart every API instance. This invalidates all existing access tokens; revoke all active refresh sessions in the database as part of the deployment.
3. Reset the bootstrap administrator password through the approved admin recovery process. Never put it in source, a migration, logs, tickets, or chat.
4. Audit deployment logs and database access for use of the former credentials.

## Remove historical secrets from Git

Coordinate with every developer and CI owner before rewriting shared history. Make a backup and rotate secrets first. Use either `git filter-repo` or BFG to remove the affected historical versions of configuration and seed files, verify with a secret scanner, then force-push only after the team agrees. Every clone, fork, CI cache, artifact, and package registry copy must be considered potentially contaminated.

Do not run a force-push automatically from this repository.

## EF Core design-time commands

The design-time DbContext factory intentionally has no fallback connection string. Before running EF commands locally, set `ConnectionStrings__DefaultConnection` in the shell or pass it after `-- --connection`. This prevents a database host, user, password, or deployment-specific value from being embedded in source code.
