-- Least-privilege login for the public MCP server (ConnectionStrings:PublicReadOnly).
-- It can connect and SELECT the four public portfolio tables, and nothing else.
--
-- Run by hand, as the database owner, after migrations have created the tables:
--
--   psql "<owner connection string>" -v mcp_password='<generated password>' -f infra/sql/mcp-readonly-role.sql
--
-- The password only ever lives in the psql variable and the app's secret config.
-- Never commit it. Safe to re-run: it creates the role once, then resets the password
-- and re-applies the grants.
--
-- No default privileges on purpose: tables added later stay invisible to this role
-- until they're granted here explicitly.

\set ON_ERROR_STOP on

\if :{?mcp_password}
\else
  \echo 'Pass the password with -v mcp_password=...'
  \quit
\endif

DO $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'mcp_readonly') THEN
    CREATE ROLE mcp_readonly LOGIN;
  END IF;
END
$$;

ALTER ROLE mcp_readonly WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS
  PASSWORD :'mcp_password';

-- Second layer: even a mistaken grant can't write while sessions default to read-only.
ALTER ROLE mcp_readonly SET default_transaction_read_only = on;

GRANT CONNECT ON DATABASE :"DBNAME" TO mcp_readonly;
GRANT USAGE ON SCHEMA public TO mcp_readonly;

GRANT SELECT ON TABLE
  public."Projects",
  public."WorkExperiences",
  public."ProjectWorkExperience",
  public."Information"
TO mcp_readonly;
