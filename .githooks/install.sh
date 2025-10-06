#!/bin/sh

# Configure Git to use .githooks directory
git config core.hooksPath .githooks

echo "✅ Git hooks configured successfully!"
echo "Git will now use hooks from .githooks/ directory."
echo "Main branch is now protected from direct pushes."

