#!/bin/bash
# ai-worker launcher (env + exec) — the worker itself is the .NET console.
# Deploy: dotnet publish ai-worker -c Release -r linux-x64 --self-contained
# Secrets via environment: Llm__CoreApiKey, Llm__ApiKey (never committed).
set -e
DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
exec "$DIR/ai-worker/bin/Release/net8.0/linux-x64/publish/ai-worker"
