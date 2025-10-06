#!/bin/sh

# Install git hooks
HOOKS_DIR="$(git rev-parse --show-toplevel)/.githooks"
GIT_HOOKS_DIR="$(git rev-parse --git-dir)/hooks"

echo "Installing git hooks..."

# Copy pre-push hook
cp "$HOOKS_DIR/pre-push" "$GIT_HOOKS_DIR/pre-push"
chmod +x "$GIT_HOOKS_DIR/pre-push"

echo "✅ Git hooks installed successfully!"
echo "Main branch is now protected from direct pushes."

