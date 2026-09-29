# LastNight

Unity zombie survival prototype.

## Development

- Unity version: see `ProjectSettings/ProjectVersion.txt`.
- `develop`: current project snapshot, with licensed downloads excluded.
- Create feature branches from `develop` (for example, `feature/barricade-ui`).
- This baseline intentionally has no parent commits from the older asset-containing history.

## Required local assets

`Assets/DownlodedAssets/` and its folder `.meta` are intentionally excluded from Git.
Restore legitimately acquired assets locally into their original paths, preserving the
asset `.meta` files/GUIDs. Without these dependencies, scenes may show missing references
and the project may not compile or render correctly. Do not force-add this directory.

Unity caches, generated project files, and local Git recovery backups are also excluded.
