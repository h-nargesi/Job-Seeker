#!/bin/bash
# ai-worker launcher (env + exec) — the worker itself is the .NET console.
# Deploy: dotnet publish ai-worker -c Release -r linux-x64 --self-contained -o ./publish-ai-worker
# Secrets via environment: Llm__CoreApiKey, Llm__ApiKey (never committed).
# Holds a logind sleep inhibitor for the whole run (blocks idle suspend).
set -e
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BIN="$ROOT/publish-ai-worker/ai-worker"
if command -v systemd-inhibit >/dev/null 2>&1; then
  exec systemd-inhibit --what=sleep:idle --who=ai-worker \
    --why="AI job run in flight" --mode=block "$BIN"
fi
echo "warning: systemd-inhibit not found - running without suspend protection" >&2
exec "$BIN"
