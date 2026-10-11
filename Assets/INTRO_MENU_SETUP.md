# Intro 메뉴 연결 및 검증

2026-10-11 사용자의 수정 요청에 따라 Unity 에디터에서 기존 Intro 씬을 연결하고 저장했다. SceneChangeToOne은 Game 씬 시작 기능을 유지한다.

## 현재 구성

- `Canvas`: SceneChange 하나, Canvas, GraphicRaycaster.
- `Canvas/MainMenu`: 기존 START(Button), Setting, Quit 버튼. 기존 배치를 유지하며 그룹으로 묶었다.
- `Canvas/SettingDisplay`: 기존 설정창. CloseSettings와 CancelRebinding 버튼을 추가했다.
- `Canvas/QuitConfirmation`: 전체 화면 클릭 차단 배경과 Dialog. Title/Message/Confirm/Cancel로 구성하며 처음에는 비활성이다.
- EventSystem 하나. Missing Script 없음.

## Inspector

Canvas의 SceneChange는 Settings Panel=`SettingDisplay`, Quit Confirmation Panel=`QuitConfirmation`, Main Menu Panel=`MainMenu`에 연결됐다. 키 이름 UI Text 8개, ReBindingKeyText, X/Y 감도 Slider와 Text가 기존 오브젝트에 연결됐다.

Binding Button Texts의 순서는 Forward / Backward / Left / Right / Run / Fire / Reload / Pause(ESC)이다. 버튼 자식 TMP 텍스트가 들어간다. 화면 모드 표시는 `SettingDisplay/Text/DisplayModeText/Fullscreen` 및 `Windowed`를 사용하며 전환 버튼 자체는 항상 남아 있다.

기존 폰트가 한글을 표시하지 못하므로 Intro의 키 이름과 입력 대기 안내를 영문으로 표시한다. 감도는 0.1~3.0이다. GameSessionManager와 KeyBinding.*, MouseSensitivity.X/Y, DisplayFullscreen 저장값을 공유한다.

## 버튼 이벤트

모든 버튼은 Canvas의 같은 SceneChange 컴포넌트를 대상으로 한다.

| 버튼 | 함수 |
|---|---|
| MainMenu/Button (START) | SceneChangeToOne |
| MainMenu/Setting | OpenSettings |
| MainMenu/Quit | OpenQuitConfirmation |
| SettingDisplay/CloseSettings | CloseSettings |
| SettingDisplay/CancelRebinding | CancelRebinding |
| QuitConfirmation/Dialog/Confirm | QuitGame |
| QuitConfirmation/Dialog/Cancel | CancelQuit |
| Foward / BackWard / Left / Right | RebindForward / RebindBackward / RebindLeft / RebindRight |
| Run / Fire / Reload / ESC | RebindRun / RebindFire / RebindReload / RebindPause |
| Full screen / Widowed | SetFullscreenMode / SetWindowedMode |

Slider 이벤트는 SceneChange가 연결된 참조에서 등록한다. 중복 Inspector 이벤트는 필요 없다. 입력 대기 중 Escape는 키 지정으로 처리되며, 창을 닫거나 Cancel Key Input을 눌러 대기를 취소한다. 입력 대기가 아닐 때 Escape는 현재 설정/종료창을 닫는다.

## 검증 결과

- Unity 컴파일 오류 없음. 저장된 씬과 열린 씬 모두 컨트롤러 1개, EventSystem 1개, Missing Script 0개, 잘못된 버튼 이벤트 0개.
- Play Mode에서 실제 Button.onClick 이벤트로 설정 열기/닫기, 입력 대기/취소, 종료창 열기/취소 등 10개 확인 통과.
- 화면 모드 2개 및 키 버튼 표시 8개 확인 통과. 화면 설정은 검증 전 값으로 복원.
- 종료 확인 버튼이 Editor의 Play Mode를 종료함을 확인.
- START 이벤트로 Game 씬 진입을 확인하고 GameSessionManager의 키 8개와 X 감도 저장값 일치를 확인.
- 1280×720 캡처에서 종료창 가독성과 설정창의 버튼/슬라이더 겹침 해결을 확인.
- 이전 임시 Editor Preview 검증 19개 통과.

물리 키보드의 새 키 캡처와 Escape 입력, 실제 Windows 빌드 프로세스 종료는 이번 검증에서 실행하지 않았다. Editor 버튼 이벤트 검증과 실제 OS 입력/빌드 검증을 구분한다.