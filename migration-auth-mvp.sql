START TRANSACTION;

DROP INDEX "IX_Users_Username";

ALTER TABLE "Users" ALTER COLUMN "Email" TYPE character varying(320);

ALTER TABLE "Users" ADD "FailedLoginAttempts" integer NOT NULL DEFAULT 0;

ALTER TABLE "Users" ADD "IsActive" boolean NOT NULL DEFAULT TRUE;

ALTER TABLE "Users" ADD "LastLoginAt" timestamp with time zone;

ALTER TABLE "Users" ADD "LockoutEndAt" timestamp with time zone;

ALTER TABLE "Users" ADD "MustChangePassword" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE "Users" ADD "NormalizedEmail" character varying(320) NOT NULL DEFAULT '';

ALTER TABLE "Users" ADD "NormalizedUsername" character varying(100) NOT NULL DEFAULT '';

ALTER TABLE "Users" ADD "UpdatedAt" timestamp with time zone;

UPDATE "Users" SET "NormalizedUsername" = upper("Username"), "NormalizedEmail" = upper("Email")

CREATE TABLE "RefreshSessions" (
    "Id" uuid NOT NULL,
    "UserId" integer NOT NULL,
    "TokenHash" character varying(64) NOT NULL,
    "JwtId" character varying(64) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "ExpiresAt" timestamp with time zone NOT NULL,
    "RevokedAt" timestamp with time zone,
    "ReplacedBySessionId" uuid,
    "CreatedByIp" text,
    "RevokedByIp" text,
    "UserAgent" text,
    "RevokeReason" text,
    CONSTRAINT "PK_RefreshSessions" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_RefreshSessions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
);

CREATE UNIQUE INDEX "IX_Users_NormalizedEmail" ON "Users" ("NormalizedEmail");

CREATE UNIQUE INDEX "IX_Users_NormalizedUsername" ON "Users" ("NormalizedUsername");

CREATE UNIQUE INDEX "IX_RefreshSessions_TokenHash" ON "RefreshSessions" ("TokenHash");

CREATE INDEX "IX_RefreshSessions_UserId_ExpiresAt" ON "RefreshSessions" ("UserId", "ExpiresAt");

CREATE INDEX "IX_RefreshSessions_UserId_RevokedAt" ON "RefreshSessions" ("UserId", "RevokedAt");

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260720051552_AddAuthenticationMvp', '8.0.29');

COMMIT;

