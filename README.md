# LastNight

## English

LastNight is a single-player, third-person zombie survival prototype built with Unity. Defend your base against waves of zombies while venturing outside to collect ammunition, healing items, and barricade materials. Balance scavenging, rebuilding your defenses, and taking down special zombies to survive the next wave.

## 한국어

LastNight는 Unity로 개발 중인 싱글플레이 3인칭 좀비 서바이벌 프로토타입입니다. 몰려오는 좀비 웨이브로부터 거점을 지키며, 외부로 나가 탄약·회복 아이템·바리케이드 재료를 수집합니다. 보급품 탐색과 방어선 복구, 특수 좀비 처치의 우선순위를 판단하며 다음 웨이브에서 살아남으세요.

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
