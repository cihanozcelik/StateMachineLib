# Git Hooks

This directory contains git hooks to enforce repository rules.

## Installation

After cloning the repository, run:

```bash
.githooks/install.sh
```

## Hooks

### pre-push

Prevents direct pushes to the `main` branch. All changes must go through feature branches and pull requests.

**Usage:**
```bash
# Create a feature branch
git checkout -b feature/my-feature

# Make changes and commit
git add .
git commit -m "feat: my feature"

# Push feature branch (allowed)
git push origin feature/my-feature

# Create PR on GitHub and merge
```

**Trying to push to main directly will fail:**
```bash
git checkout main
git push origin main
# 🚫 ERROR: Direct push to 'main' branch is not allowed!
```

