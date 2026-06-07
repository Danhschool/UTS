# Trả lời phản biện — Phần I. Mở đầu & định vị đề tài

Bổ sung cho [`Luyen-Phan-Bien-Do-An.md`](./Luyen-Phan-Bien-Do-An.md) (câu 1–5).

**Phần II (câu 6–11):** [`Tra-Loi-Phan-Bien-II-Cong-Nghe.md`](./Tra-Loi-Phan-Bien-II-Cong-Nghe.md)

---

<a id="cau-1"></a>

## Câu 1 — Đóng góp khoa học – kỹ thuật

**Hỏi:** Đóng góp là gì? Khác “game Unity có mic”? Đo bằng chỉ số nào?

### Trả lời ngắn

Em xây **pipeline voice tiếng Việt offline** → chuẩn hóa → so khớp mờ với tập lệnh cố định → thực thi gameplay RTS, chạy trong **chơi đơn / PvE bot / LAN 1v1**. Khác “có mic” ở chỗ có **STT + fuzzy + ánh xạ lệnh + tích hợp trận thật**, không chỉ thu âm hay hiện chữ.

### Code minh họa khi trả lời

| Việc | File |
|------|------|
| Nhấn V, thu âm one-shot | `Assets/Scripts/SpeechRecognition/VoiceCommandPushToTalkInput.cs` |
| Ghi âm tối đa 12s | `Assets/Scripts/SpeechRecognition/VoiceCommandOneShotCapture.cs` |
| Whisper STT offline | `Assets/Scripts/SpeechRecognition/Whisper/WhisperSpeechRecognitionBackend.cs` |
| Interface STT | `Assets/Scripts/SpeechRecognition/Core/ISpeechRecognitionBackend.cs` |
| Cấu hình `language=vi` | `Assets/Scripts/SpeechRecognition/Core/WhisperSpeechDefaults.cs` |
| Prompt thuật ngữ RTS | `Assets/Scripts/SpeechRecognition/Core/WhisperVoicePromptBuilder.cs` |
| Mô hình GGML | `Assets/StreamingAssets/Whisper/` |
| Chuẩn hóa tiếng Việt | `Assets/Scripts/SpeechRecognition/Core/VietnameseTextNormalizer.cs` |
| So khớp mờ (Levenshtein) | `Assets/Scripts/SpeechRecognition/Core/FuzzyVoiceCommandResolver.cs` |
| Map transcript → commandId | `Assets/Scripts/SpeechRecognition/Core/VoiceCommandOneShotTranscriptMapper.cs` |
| Dataset ~29 lệnh | `docs/voice-command-dataset.vi.json` |
| commandId → hành động game | `Assets/Scripts/SpeechRecognition/Core/VoiceCommandIdActionRules.cs` |
| Thực thi voice trong trận | `Assets/Scripts/Player/PlayerInput.Voice.cs` → `TryExecuteVoiceCommand` |
| Lệnh chuột/voice chung | `Assets/Scripts/Commands/BaseCommand.cs`, `MoveCommand.cs`, … |
| Bot PvE | `Assets/Scripts/AI/Core/AIController.cs`, `Assets/Scripts/PvAI/PvAiOfflineSpawnService.cs` |
| LAN Mirror | `Assets/Scripts/Netplay/RtsUtsPlayerCommands.cs`, `RtsUtsNetworkEntity.cs` |
| Fog of war | `Assets/Scripts/Player/OwnerFogVisionLayers.cs` |
| Luồng scene / loading | `docs/Game-Module-Guide.md` |

### Khác “Unity có mic” (1 câu)

Mic + text/chat → em có **dataset lệnh + fuzzy + `TryExecuteVoiceCommand`** và **Mirror LAN**; voice và chuột cùng đi qua `BaseCommand`.

### Chỉ số đo

- **Latency STT** — log trong `VoiceCommandOneShotCapture` / Whisper backend  
- **% nhận đúng lệnh** — N mẫu ghi âm vs `voice-command-dataset.vi.json`  
- **% thực thi đúng trong game** — kịch bản trận (đã chọn unit, đủ tài nguyên)  
- **FPS** — Profiler khi bật/tắt STT  
- **LAN** — số trận desync/disconnect  

*Nếu PDF chưa có bảng số: nói đã test chức năng + sẽ trình bày số liệu tại buổi bảo vệ.*

### Hạn chế (nói nhanh)

- Chỉ lệnh có cấu trúc, không hội thoại tự do  
- `move` / `attack` vẫn cần click đích (`PlayerInput.Voice.cs`)  
- MVP — trọng tâm pipeline voice, không phải game thương mại đầy đủ  

### Mẫu miệng (~ 1 phút)

> Đóng góp chính là pipeline voice tiếng Việt offline tích hợp RTS: phím V → Whisper → chuẩn hóa → fuzzy với JSON → `commandId` → `PlayerInput.TryExecuteVoiceCommand`, cùng luồng `BaseCommand` với chuột. Chạy được PvE (`AIController`) và LAN (`RtsUtsPlayerCommands`). Khác game có mic vì có tầng ánh xạ lệnh và dataset, không chỉ STT. Đánh giá bằng latency STT, accuracy lệnh, success trong game, FPS và ổn định LAN.

---

<a id="cau-2"></a>

## Câu 2 — Vì sao chọn RTS, không phải MOBA/RTT?

**Hỏi:** Vì sao RTS làm bối cảnh voice? Sang MOBA thì pipeline đổi gì?

### Trả lời ngắn

**RTS** có vòng lặp **kinh tế – xây dựng – quân sự** song song, APM cao, nhiều lệnh **có cấu trúc** (“chọn dân rảnh”, “thu gỗ”, “xây doanh trại”) — phù hợp finite command + voice hơn **MOBA** (một tướng, ít macro) hay **RTT** (chỉ combat, không khai thác/xây base).

Sang **MOBA**: giữ lõi STT (`Whisper` → fuzzy); **đổi dataset, prompt, rules và executor** — bỏ gather/build/train, thêm skill/item/ping; selection chuyển từ **nhiều unit** sang **một hero**.

### Code chứng minh “đây là RTS” (khi hội đồng hỏi “sao biết là RTS?”)

| Đặc trưng RTS | File |
|---------------|------|
| Dataset lệnh macro RTS | `docs/voice-command-dataset.vi.json` (`GATHER_RESOURCE`, `BUILD_STRUCTURE`, `TRAIN_UNIT`, …) |
| Map lệnh kinh tế / xây / train / research | `Assets/Scripts/SpeechRecognition/Core/VoiceCommandIdActionRules.cs` (`gather_*`, `build_*`, `train_*`, `research_*`) |
| Chọn nhiều unit / dân rảnh / toàn quân | `Assets/Scripts/Player/PlayerInput.Voice.cs` — `VoiceSelectIdleWorkers`, `VoiceSelectAllMilitary`, `VoiceSelectUnits` |
| Thu tài nguyên | `Assets/Scripts/Commands/GatherCommand.cs`, `Assets/Scripts/Units/Worker.cs` |
| Kiểm tra đủ gỗ/đá/lương thực | `Assets/Scripts/Player/SupplyAffordability.cs` |
| AI kinh tế bot | `Assets/Scripts/AI/Managers/AIEconomyManager.cs` |
| AI xây base / quân sự | `Assets/Scripts/AI/Managers/AIBaseManager.cs`, `AIMilitaryManager.cs` |
| Di chuyển đội hình | `Assets/Scripts/Units/Formation/UnitSquareFormationPlanner.cs` |
| Prompt Whisper theo từ lệnh RTS | `Assets/Scripts/SpeechRecognition/Core/WhisperVoicePromptBuilder.cs` |

### So sánh nhanh (nói 3 câu)

| Thể loại | Vì sao không chọn làm chính |
|----------|------------------------------|
| **MOBA** | Một hero; lệnh voice chủ yếu skill + di chuyển, ít lệnh macro — không tận dụng dataset gather/build hiện tại |
| **RTT** | Không có vòng kinh tế–xây dựng — phần lớn intent trong JSON (`thu go`, `xay nha linh`) không có ý nghĩa |
| **RTS** | Đúng phạm vi code: multi-unit, tài nguyên, công trình, research — voice thay được nhiều click macro |

### Sang MOBA — phần nào giữ, phần nào đổi

| Tầng | Giữ / đổi | File liên quan |
|------|-----------|----------------|
| Thu âm + Whisper | **Giữ** | `VoiceCommandPushToTalkInput.cs`, `WhisperSpeechRecognitionBackend.cs` |
| Chuẩn hóa + fuzzy | **Giữ** | `VietnameseTextNormalizer.cs`, `FuzzyVoiceCommandResolver.cs` |
| Dataset lệnh | **Đổi** — skill, ultimate, mua đồ, ping | thay `docs/voice-command-dataset.vi.json` |
| Prompt Whisper | **Đổi** từ vựng MOBA | `WhisperVoicePromptBuilder.cs` |
| Ánh xạ commandId | **Đổi** | `VoiceCommandIdActionRules.cs` |
| Thực thi gameplay | **Đổi** — cast skill 1 hero, không chọn 20 worker | `PlayerInput.Voice.cs` (và command/skill system MOBA) |
| AI planner kinh tế | **Bỏ / thay** | `AIEconomyManager.cs` không còn phù hợp |

### Mẫu miệng (~ 1 phút)

> Em chọn RTS vì người chơi phải xử lý song song kinh tế, xây dựng và quân sự — APM cao, nhiều lệnh lặp lại có cấu trúc, phù hợp tập lệnh hữu hạn và voice. Trong code, dataset và `VoiceCommandIdActionRules` có gather, build, train, research; `PlayerInput.Voice` chọn nhiều dân/quân — đặc trưng RTS, không có ở MOBA/RTT. MOBA chỉ điều khiển một tướng; RTT bỏ vòng kinh tế. Nếu chuyển sang MOBA, em giữ pipeline Whisper và fuzzy, nhưng phải viết lại JSON lệnh, prompt, rules và executor — từ macro base sang skill và item; phần `AIEconomyManager` và gather/build không còn dùng được.

---

<a id="cau-3"></a>

## Câu 3 — “Sẵn sàng phát hành” vs “MVP”

**Hỏi:** Mục tiêu “sẵn sàng phát hành” (Mở đầu) khác “MVP” (mục 1.3.3) thế nào?

### Trả lời ngắn

- **Sẵn sàng phát hành** = sản phẩm **chạy được trọn vòng** như một game Windows: menu → vào trận → chơi → kết thúc; **3 chế độ** (đơn / PvE / LAN); voice + chuột; không chỉ demo kỹ thuật tách rời.
- **MVP** = **giới hạn nội dung & phạm vi**: ít phe/loại unit, không cốt truyện, không đa ngôn ngữ, **chỉ LAN** (không matchmaking Internet), lệnh voice hữu hạn — đủ chứng minh đề tài, **chưa** game thương mại đầy đủ.

→ Không mâu thuẫn: **phát hành mức MVP** (vertical slice hoàn chỉnh), không phải AAA đủ tính năng.

### Code — phần “sẵn sàng phát hành” (trọn luồng)

| Việc | File |
|------|------|
| Menu, điều hướng trước trận | `Assets/Scripts/UI/Pregame/MainMenuUIController.cs`, `PregameSetupUIController.cs` |
| Chọn SP / MP | `Assets/Scripts/Game/Pregame/PregamePlayMode.cs` |
| Session map / độ khó | `Assets/Scripts/Game/Pregame/PregameSessionState.cs` |
| Load scene qua Loading | `Assets/Scripts/Game/Startup/GameplaySceneLoader.cs` |
| Chờ spawn xong mới chơi | `Assets/Scripts/Game/Startup/GameplayInGameReadyController.cs` |
| Pause / tốc độ trận | `Assets/Scripts/Game/GamePauseService.cs`, `GameSpeedController.cs` |
| Kết thúc trận → menu | `Assets/Scripts/Game/MatchOutcomeFlow.cs` |
| PvE bot | `Assets/Scripts/PvAI/PvAiGameSceneBootstrap.cs`, `PvAiOfflineSpawnService.cs` |
| LAN lobby + netplay | `Assets/Scripts/UI/Pregame/PregameMpLobbyCoordinator.cs`, `Assets/Scripts/Netplay/RtsNetGameSceneBootstrap.cs` |
| Voice trong trận thật | `Assets/Scripts/Player/PlayerInput.Voice.cs` |
| Luồng tổng thể (doc) | `docs/Game-Module-Guide.md` |

### Code / phạm vi — phần “MVP” (cố ý chưa làm)

| Giới hạn MVP | Thể hiện |
|--------------|----------|
| Chỉ LAN, không online | Mirror + lobby IP — `PregameMpLobbyCoordinator.cs`, không có matchmaking server |
| Ít chế độ | `PregamePlayMode` chỉ `SinglePlayer` / `Multiplayer` — không campaign |
| Lệnh voice hữu hạn | `docs/voice-command-dataset.vi.json` — không open conversation |
| Nội dung unit/phe giới hạn | Prefab/archetype cố định trong `PlayerInput.Voice.cs` (`worker`, `warrior`, …) |
| Hướng mở rộng thương mại | Kiến nghị Kết luận báo cáo — chưa có trong code |

### Một câu phân biệt (nhớ thuộc)

> **Phát hành** = đóng gói `.exe`, luồng chơi ổn định, đủ tính năng cốt lõi RTS + voice + LAN. **MVP** = cố ý thu hẹp nội dung và hạ tầng (không story, không online ghép trận) để tập trung chứng minh pipeline kỹ thuật.

### Mẫu miệng (~ 45 giây)

> “Sẵn sàng phát hành” em hiểu là người chơi tải game Windows, vào menu, chọn đơn hoặc LAN, chơi trận RTS đầy đủ với voice — luồng có `GameplaySceneLoader`, `MatchOutcomeFlow`, PvE và Mirror. “MVP” là giới hạn phạm vi đồ án: ít phe và unit, không cốt truyện, chỉ LAN không matchmaking, tập lệnh voice cố định. Tức là phát hành **bản MVP hoàn chỉnh về kỹ thuật**, chưa phải sản phẩm thương mại mở rộng như kiến nghị cuối báo cáo.

---

<a id="cau-4"></a>

## Câu 4 — Giảm APM / giảm tải thao tác

**Hỏi:** Đã đo APM trước/sau voice chưa? Nếu chưa, chứng minh “giảm tải thao tác” thế nào?

### Trả lời ngắn

- **Chưa đo APM chính thức** (không có counter APM trong code) — nói thẳng với hội đồng.
- Mục tiêu đề tài là **giảm thao tác cơ học lặp lại** (chọn dân, gather, train, build…) bằng **một lần nói + PTT** thay nhiều click/hotkey.
- Chứng minh bằng: **đếm số thao tác** cùng một nhiệm vụ (chuột vs voice), **thời gian hoàn thành**, hoặc test người dùng — không bắt buộc chỉ có con số APM.

### Lệnh voice thay được nhiều click (chỉ code)

| Nhiệm vụ | Voice | Thay thế thao tác chuột | File |
|----------|-------|-------------------------|------|
| Chọn dân rảnh | `select_idle_workers` | Box-select / click từng worker | `PlayerInput.Voice.cs` → `VoiceSelectIdleWorkers` |
| Chọn toàn quân | `select_all_military` | Ctrl+click / double-click từng loại | `VoiceSelectAllMilitary`, `VoiceSelectAllOfArchetypeOnScreen` |
| Chọn N unit | `select_n_*` | Nhiều click hoặc hotkey + lặp | `VoiceSelectUnits`, `VoiceCommandIdActionRules.TryParseSelectCommand` |
| Thu gỗ/đá | `gather_wood`, `gather_stone` | Chọn worker → click mỏ gần camera | `VoiceTryGatherNearestSupply` |
| Dừng | `stop` | Hotkey stop / click UI | `StopSelectedUnitsFromHotkey` (voice gọi chung) |
| Xây / train / research | `build_*`, `train_*`, `research_*` | Mở action bar → tìm nút → (có thể) click map | `VoiceTryArmOrExecuteCommand` + rules trong `VoiceCommandIdActionRules.cs` |

### Lệnh voice **chưa** giảm APM nhiều

| Lệnh | Vì sao | File |
|------|--------|------|
| `move`, `attack`, `attack_move` | Voice chỉ **arm** lệnh — vẫn **click map** | `VoiceTryArmCommand` (`requiresWorldClick: true`) |
| Mọi lệnh | Thêm bước **nhấn V + nói** (đổi kênh, không xóa hết chuột) | `VoiceCommandPushToTalkInput.cs` |

### So sánh với hotkey (baseline chuột)

| Kênh | File |
|------|------|
| Hotkey chọn nhóm / lệnh | `Assets/Scripts/Hotkeys/` (`HotkeyHandlerBase`, `HotkeyBindingPreferences`) |
| Click / double-click chọn hàng loạt | `Assets/Scripts/Player/PlayerInput.cs` (double-click cùng loại unit) |
| Voice | `PlayerInput.Voice.cs` → `TryExecuteVoiceCommand` |

→ Voice **bổ sung** kênh, ưu tiên macro; chuột/hotkey vẫn cần cho micro (`move`/`attack`).

### Cách chứng minh nếu chưa có bảng APM

| Cách | Làm gì |
|------|--------|
| **Đếm thao tác** | Cùng kịch bản (vd. “10 dân rảnh đi thu gỗ”): đếm click/hotkey **chuột only** vs **voice** |
| **Thời gian** | Stopwatch từ lúc quyết định → lệnh thực thi xong |
| **APM ước lượng** | `APM ≈ (số click + số phím) / phút` trong đoạn 2–3 phút macro |
| **Quan sát demo** | Live: “chọn dân rảnh” — 1 câu vs kéo box |
| **Bổ sung trước bảo vệ** | Ghi 5 kịch bản × 3 lần, bảng so sánh (không cần tool trong game) |

*Code hiện chỉ log voice/STT:* `VoiceCommandRuntimeDiagnostics.cs`, `GameEventLog` trong `PlayerInput.Voice.cs` — **chưa** log đếm input toàn cục.

### Mẫu miệng (~ 45 giây)

> Em chưa triển khai module đo APM tự động trong game. Đề tài hướng tới giảm thao tác lặp: các lệnh như chọn dân rảnh, chọn quân, thu gỗ, train, build thực thi qua `TryExecuteVoiceCommand` — một lần voice thay nhiều click so với box-select hoặc action bar. Riêng move/attack voice chỉ arm lệnh, vẫn cần click map nên chưa giảm APM micro. Em chứng minh bằng đếm số thao tác và thời gian cùng kịch bản chuột-only vs voice, và có thể bổ sung bảng số liệu trước buổi bảo vệ.

---

<a id="cau-5"></a>

## Câu 5 — 20–30 vs 35 vs 29 lệnh: con số nào đúng?

**Hỏi:** Mở đầu 20–30, Kết luận 35, Phụ lục 29 — chốt bao nhiêu, vì sao lệch?

### Trả lời ngắn (chốt khi bảo vệ)

| Nguồn | Số | Ý nghĩa |
|-------|-----|---------|
| **Mở đầu 20–30** | Ước lượng **phạm vi thiết kế** ban đầu | Cam kết *finite command*, không open-domain |
| **Phụ lục 29** | **Mã lệnh gameplay cốt lõi** (snake_case) | Bảng demo báo cáo — **nên dùng làm số chính thức** khi nói “bao nhiêu lệnh” |
| **Kết luận 35** | Đếm **intent trong dataset tài liệu** (~34) | Làm tròn / gộp nhóm — **không** trùng cách đếm Phụ lục |
| **Code trận chính (V)** | **27 rule** + pattern `select_N_*`; JSON **67 entry** | 67 = 27 lệnh + 40 biến thể `select_1..10` × 4 loại unit |

→ **Không sai số — khác cách đếm:** loại lệnh (29) vs entry fuzzy (67) vs intent thiết kế (34).

### Code — đếm thực tế trong project

| Dataset / rule | Số entry | File |
|----------------|----------|------|
| Lệnh **trận chính** (nhấn V) | **67** `CommandId` | `Assets/Resources/VoiceCommands/rts_voice_commands_uts_units_vi.json` |
| Mapper trận chính nạp file trên | default path | `Assets/Scripts/SpeechRecognition/Core/VoiceCommandOneShotTranscriptMapper.cs` |
| **27** lệnh map gameplay | `BuildActions()` | `Assets/Scripts/SpeechRecognition/Core/VoiceCommandIdActionRules.cs` |
| `select_3_warrior`… (không liệt kê từng dòng) | pattern `select_N_archetype` | `VoiceCommandIdActionRules.TryParseSelectCommand` |
| Intent thiết kế / sandbox | **34** intent | `docs/voice-command-dataset.vi.json`, `Assets/Resources/VoiceCommands/rts_voice_commands_standard_vi.json` |
| Import intent → CommandId | | `Assets/Scripts/SpeechRecognition/Core/VoiceCommandDocsDatasetImporter.cs` |
| Router sandbox (34 intent) | | `Assets/Scripts/SpeechRecognition/Core/VoiceCommandRouter.cs` |

### 27 lệnh cốt lõi trong `VoiceCommandIdActionRules` (đếm nhanh)

`stop`, `move`, `attack`, `attack_move`, `hold_position`, `select_all_military`, `select_idle_workers`, `gather_wood`, `gather_stone`, 5×`build_*`, 4×`train_*`, 6×`research_*`, 3×`cancel_*` → **27** (+ `select_N_*` động).

*Phụ lục 29:* gần khớp 27; báo cáo có thêm vd. `gather_food` — **chưa** thấy trong `rts_voice_commands_uts_units_vi.json` (lệch nhỏ báo cáo ↔ code).

### Vì sao lệch giữa các chương?

1. **20–30** = phạm vi khi **lên đề cương**, chưa đóng băng dataset.  
2. **29** = bảng **rút gọn** cho độc giả (một dòng = một ý định gameplay).  
3. **35** ≈ **34 intent** file `voice-command-dataset.vi.json` (+ có thể tính thêm nhóm `select`).  
4. **67** = mỗi `select_1_worker`…`select_10_rockwarrior` là **một entry fuzzy riêng**, không phải 67 “loại lệnh”.

### Một câu nhớ (hội đồng hay hỏi tiếp)

> Em chốt **29 (≈27) loại lệnh gameplay** theo Phụ lục và `VoiceCommandIdActionRules`; file JSON trận chính có **67 entry** vì tách từng `select_N`; dataset thiết kế có **34 intent** cho mở rộng. Mở đầu 20–30 là phạm vi ban đầu, không mâu thuẫn khi giải thích rõ đơn vị đếm.

### Mẫu miệng (~ 40 giây)

> Ba con số khác đơn vị đếm. Mở đầu 20–30 là phạm vi ước lượng. Phụ lục 29 là mã lệnh cốt lõi — khớp 27 rule trong `VoiceCommandIdActionRules` cộng nhóm chọn N unit. Kết luận 35 gần với 34 intent trong `voice-command-dataset.vi.json`. Trong build, `rts_voice_commands_uts_units_vi.json` có 67 dòng vì mỗi select_1 đến select_10 là một entry fuzzy. Em chốt khi báo cáo: khoảng 29 loại lệnh gameplay, không phải 67 loại.

---

*Phần II (từ câu 6): [`Tra-Loi-Phan-Bien-II-Cong-Nghe.md`](./Tra-Loi-Phan-Bien-II-Cong-Nghe.md)*
