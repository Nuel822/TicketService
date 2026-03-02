-- Creates both databases on the single PostgreSQL instance.
-- This script runs automatically when the postgres container starts for the
-- first time (via docker-entrypoint-initdb.d).
--
-- The primary database (ticketing) is already created by POSTGRES_DB env var.
-- This script creates the reporting database and grants privileges.

-- ── Reporting database ────────────────────────────────────────────────────────
CREATE DATABASE ticketing_reporting;

-- Grant all privileges on both databases to the shared user
GRANT ALL PRIVILEGES ON DATABASE ticketing TO ticketing_user;
GRANT ALL PRIVILEGES ON DATABASE ticketing_reporting TO ticketing_user;


