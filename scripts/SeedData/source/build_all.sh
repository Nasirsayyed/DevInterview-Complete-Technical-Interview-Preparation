#!/usr/bin/env sh
# Regenerates every JSON content pack in scripts/SeedData from the Python sources.
set -e
cd "$(dirname "$0")"
for f in technologies q_csharp q_dotnet q_sql q_frontend q_cs q_other challenges flashcards learning_paths companies; do
  python3 "$f.py"
done
python3 validate.py
