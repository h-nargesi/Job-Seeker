#!/bin/bash
# Stamp <semver>+<githash>[-dirty] into both extension manifests (version_name)
# and echo the publish commands that carry the hash into the .NET binaries.
# Stamps live in the working tree only — committing them is never required.
set -e
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
HASH="$(git -C "$ROOT" rev-parse --short HEAD)"
if ! git -C "$ROOT" diff --quiet; then
  HASH="$HASH-dirty"
fi

for manifest in agent-extension assistant-extension; do
  stamped="$(node -e '
    const fs = require("fs");
    const file = process.argv[1];
    const manifest = JSON.parse(fs.readFileSync(file, "utf8"));
    manifest.version_name = manifest.version + "+" + process.argv[2];
    fs.writeFileSync(file, JSON.stringify(manifest, null, "\t") + "\n");
    process.stdout.write(manifest.version_name);
  ' "$ROOT/$manifest/manifest.json" "$HASH")"
  echo "$manifest/manifest.json -> version_name $stamped"
done

echo
echo "publish with the hash:"
echo "  dotnet publish core-decision-dotnet -c Release -o publish-core /p:SourceRevisionId=$HASH"
echo "  dotnet publish ai-worker -c Release -r linux-x64 --self-contained -o publish-ai-worker /p:SourceRevisionId=$HASH"
