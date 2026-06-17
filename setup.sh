#!/usr/bin/env bash
#
# setup.sh — initialize git and publish this project to GitHub.
#
# Usage:
#   chmod +x setup.sh
#   ./setup.sh            # creates a PRIVATE repo
#   ./setup.sh --public   # creates a PUBLIC repo
#
set -euo pipefail

# ---- Config -----------------------------------------------------------------
GH_USER="Sebadiah26"
REPO_NAME="MusicLibrary"
VISIBILITY="private"
COMMIT_MSG="Initial commit: Music Library ASP.NET Core app"

if [[ "${1:-}" == "--public" ]]; then
  VISIBILITY="public"
fi

# ---- Sanity checks ----------------------------------------------------------
if ! command -v git >/dev/null 2>&1; then
  echo "❌ git is not installed. Install it from https://git-scm.com/ and re-run."
  exit 1
fi

# ---- Initialize the local repo ----------------------------------------------
if [[ ! -d .git ]]; then
  echo "▶ Initializing git repository…"
  git init -q
fi

git add -A

if git diff --cached --quiet; then
  echo "ℹ Nothing new to commit."
else
  git commit -q -m "$COMMIT_MSG"
  echo "✔ Created commit: \"$COMMIT_MSG\""
fi

# Ensure the default branch is 'main'.
git branch -M main

# ---- Publish to GitHub ------------------------------------------------------
if command -v gh >/dev/null 2>&1; then
  echo "▶ Using GitHub CLI to create and push the repo ($VISIBILITY)…"
  gh repo create "$REPO_NAME" --"$VISIBILITY" --source=. --remote=origin --push
  echo "✅ Done! Repo: https://github.com/$GH_USER/$REPO_NAME"
else
  echo "ℹ GitHub CLI (gh) not found — falling back to plain git."
  echo "  First, create an EMPTY repo named '$REPO_NAME' at https://github.com/new"
  echo "  (no README, no .gitignore — this project already has one)."
  read -r -p "  Press Enter once the empty repo exists… "

  if git remote get-url origin >/dev/null 2>&1; then
    git remote set-url origin "https://github.com/$GH_USER/$REPO_NAME.git"
  else
    git remote add origin "https://github.com/$GH_USER/$REPO_NAME.git"
  fi

  git push -u origin main
  echo "✅ Done! Repo: https://github.com/$GH_USER/$REPO_NAME"
fi
