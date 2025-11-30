#!/usr/bin/env bash
set -euo pipefail

# Load Docker secrets (files in /run/secrets) into environment variables if present.
# This allows using Docker secrets or swarm secrets mounted into the container.
load_secret_if_exists() {
  local secret_name="$1"
  local env_name="$2"
  local path="/run/secrets/${secret_name}"
  if [ -f "${path}" ]; then
    export "${env_name}"="$(cat "${path}")"
  fi
}

# Known secret names we support
load_secret_if_exists "twitch_client_id" "TWITCH_CLIENT_ID"
load_secret_if_exists "twitch_client_secret" "TWITCH_CLIENT_SECRET"
load_secret_if_exists "db_connection" "ConnectionStrings__DefaultConnection"

# If ConnectionStrings__DefaultConnection is not set, fall back to a file-based DB under /data
if [ -z "${ConnectionStrings__DefaultConnection:-}" ]; then
  export ConnectionStrings__DefaultConnection="Data Source=/data/ppsnr.db"
fi

# Allow disabling HTTPS redirection behind a reverse-proxy via env var (recommended to set in deploy)
# Default in production is to keep HTTPS redirection enabled, so nothing changed here.

# Execute the container CMD (dotnet app)
exec "${@}" 
