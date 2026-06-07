# Trả lời phản biện — Phần II. Công nghệ & lựa chọn giải pháp

Bổ sung cho [`Luyen-Phan-Bien-Do-An.md`](./Luyen-Phan-Bien-Do-An.md) (câu 6–11).

**Phần I (câu 1–5):** [`Tra-Loi-Phan-Bien.md`](./Tra-Loi-Phan-Bien.md)

---

## Mục lục

- [Câu 6 — Bảng 1.1](#cau-6)
- [Câu 7 — Whisper vs Vosk](#cau-7)
- [Câu 8 — Parser/Grammar vs fuzzy](#cau-8)
- [Câu 9 — trade-off GGML vi](#cau-9)
- [Câu 10 — luồng nền, FPS, phần cứng](#cau-10)
- [Câu 11 — Mirror vs Netcode](#cau-11)

---

<a id="cau-6"></a>

## Câu 6 — Bảng 1.1: tiêu chí sống còn vs ưu tiên

**Hỏi:** Google / Azure / Whisper — tiêu chí nào *bắt buộc* với RTS, tiêu chí nào chỉ *ưu tiên*?

### Trả lời ngắn

**Sống còn (loại được ngay nếu không đạt):**

| Tiêu chí | Vì sao với RTS + LAN + đề tài |
|----------|-------------------------------|
| **Offline / không phụ thuộc Internet** | LAN khép kín; voice phải chạy khi không có mạng ngoài |
| **Độ trễ chấp nhận được** | Lệnh macro cần phản hồi trong vài giây — cloud RTT biến động |
| **Tiếng Việt** | Đề tài voice VN; cloud có nhưng gắn online |
| **Chi phí vận hành = 0** | Đồ án/demo LAN — không trả API theo phút |

→ **Google / Azure** yếu ở **Internet + độ trễ + chi phí** → chỉ **trung bình**.

**Ưu tiên (có thể bù bằng thiết kế):**

| Tiêu chí | Ghi chú |
|----------|---------|
| **Tùy biến từ điển sẵn (grammar)** | Cloud/Azure có; Whisper **không** — em bù bằng **JSON + fuzzy** |
| **Độ chính xác tuyệt đối** | Cloud có thể cao hơn; RTS chấp nhận finite vocabulary + ngưỡng 0,72 |
| **Nhẹ CPU** | Whisper trade-off: offline nhưng nặng máy — tune model/step |

→ Chọn **Whisper** vì thắng các tiêu chí **sống còn**; nhược **grammar** đã xử lý ở tầng sau STT.

### Bảng 1.1 — nhắc nhanh

| | Google | Azure | **Whisper (đã chọn)** |
|---|--------|-------|------------------------|
| Triển khai | Online | Online + một phần local | **Offline cục bộ** |
| Internet | Cao | TB–cao | **Không** |
| Độ trễ LAN | Biến động mạng | Biến động mạng | **CPU/GPU local** |
| Chi phí | Theo API | Có thể phát sinh | **Mã nguồn mở** |
| Từ điển lệnh | Có (phụ thuộc nền tảng) | Có | **Fuzzy hậu kỳ** |
| Phù hợp đề tài | TB | TB | **Cao** |

### Code — Whisper đáp ứng tiêu chí đã chọn

| Tiêu chí | File |
|----------|------|
| STT offline | `Assets/Scripts/SpeechRecognition/Whisper/WhisperSpeechRecognitionBackend.cs` |
| Model local | `Assets/StreamingAssets/Whisper/` (`WhisperSpeechDefaults.BaseModelRelativePath`) |
| `language = vi` | `Assets/Scripts/SpeechRecognition/Core/WhisperSpeechDefaults.cs` → `DefaultLanguage = "vi"` |
| Giảm độ trễ stream | `Assets/Scripts/SpeechRecognition/Core/WhisperStreamingTuning.cs` |
| Bù không có grammar Whisper | `Assets/Scripts/SpeechRecognition/Core/FuzzyVoiceCommandResolver.cs` + `Assets/Resources/VoiceCommands/rts_voice_commands_uts_units_vi.json` |
| Abstraction backend (đã khảo sát Vosk) | `Assets/Scripts/SpeechRecognition/Core/ISpeechRecognitionBackend.cs`; `Assets/StreamingAssets/VoskModels/` |

### Mẫu miệng (~ 40 giây)

> Với RTS LAN offline, tiêu chí sống còn là không phụ thuộc Internet, độ trễ ổn định, hỗ trợ tiếng Việt và không tốn phí API — Google và Azure yếu ba điểm này. Whisper chạy local trong Unity qua `WhisperSpeechRecognitionBackend`, model trong StreamingAssets, `DefaultLanguage = vi`. Nhược điểm không có grammar sẵn em bù bằng fuzzy và dataset JSON. Tùy biến từ điển cloud và độ chính xác tuyệt đối chỉ là ưu tiên, không quan trọng bằng offline và latency cho đề tài.

---

<a id="cau-7"></a>

## Câu 7 — Whisper vs Vosk

**Hỏi:** Vì sao Whisper hơn Vosk cho tiếng Việt + LAN? Vì sao vẫn có `VoskModels`? Có so sánh WER/latency không?

### Trả lời ngắn

**Chọn Whisper cho trận chính** vì: mô hình đa ngôn ngữ mạnh hơn, chịu **biến thể phát âm** tốt hơn trước khi fuzzy; tích hợp sẵn qua **whisper.unity**; không phụ thuộc **grammar cố định** lúc giải mã (Vosk grammar lớn dễ lỗi).

**Vosk** vẫn trong project = **giai đoạn khảo sát / sandbox** (model VN nhẹ, hỗ trợ grammar) — **không** phải backend đang chạy khi nhấn V trong trận.

**WER/latency:** Chưa có **bảng số chính thức** trong báo cáo — so sánh **định tính** lúc prototype; có thể bổ sung đo N mẫu trước bảo vệ.

### So sánh nhanh

| Tiêu chí | **Vosk** | **Whisper (đã chọn)** |
|----------|----------|------------------------|
| Model VN | `vosk-model-vn-0.4`, `small-vn-0.4` | `ggml-base.bin` (multilingual, `language=vi`) |
| Giới hạn từ vựng | **Grammar JSON** lúc decode | Open transcript → **fuzzy + JSON** sau |
| Nhẹ / latency | Thường nhẹ hơn | Nặng CPU hơn, tune `LowLatency` |
| Độ linh hoạt câu nói | Kém nếu ngoài grammar | Cao hơn + fuzzy bù |
| Trận chính (phím V) | Không | **Có** |

### Vì sao vẫn có `StreamingAssets/VoskModels`?

| Thành phần | Ý nghĩa |
|------------|---------|
| `Assets/StreamingAssets/VoskModels/` | Model VN để **thử Vosk** khi đầu dự án |
| `Assets/3rdParty/Plugins/Vosk.dll`, `libvosk.dll` | Native plugin Vosk |
| `Assets/Scenes/Vosk.unity` | Scene sandbox thử STT |
| `VoiceCommandProfile.BuildVoskGrammarJson()` | Sinh grammar cho Vosk nếu gắn backend Vosk |
| `VoiceCommandRouter` (comment tooltip) | Thiết kế **đổi backend** qua `ISpeechRecognitionBackend` — hiện gắn Whisper |

→ Giữ lại để **chứng minh đã khảo sát**, không xóa asset thử nghiệm; **production = Whisper**.

### Code — backend thực tế

| Vai trò | File |
|---------|------|
| Backend trận chính | `Assets/Scripts/SpeechRecognition/Whisper/WhisperSttOnlyDriver.cs` |
| | `Assets/Scripts/SpeechRecognition/VoiceCommandOneShotCapture.cs` |
| Adapter Whisper | `Assets/Scripts/SpeechRecognition/Whisper/WhisperSpeechRecognitionBackend.cs` |
| Interface đổi engine | `Assets/Scripts/SpeechRecognition/Core/ISpeechRecognitionBackend.cs` |
| Map lệnh sau STT | `Assets/Scripts/SpeechRecognition/Core/VoiceCommandOneShotTranscriptMapper.cs` |
| Grammar Vosk (chưa dùng chính) | `Assets/Scripts/SpeechRecognition/Core/VoiceCommandProfile.cs` → `BuildVoskGrammarJson()` |
| Sandbox UI (text còn nhắc Vosk) | `Assets/Scripts/SpeechRecognition/SpeechRecognitionSandboxFeedback.cs` |
| Model Whisper | `Assets/StreamingAssets/Whisper/` |
| Model Vosk | `Assets/StreamingAssets/VoskModels/vosk-model-vn-0.4/`, `vosk-model-small-vn-0.4/` |

**Lưu ý:** Không có class `VoskSpeechRecognitionBackend` trong `Assets/Scripts` — Vosk là **hướng đã thử**, Whisper là **hiện thực**.

### WER / latency — trả lời trung thực

| | Trạng thái |
|---|------------|
| **WER** (Word Error Rate) | Chưa đo chuẩn trên cùng N mẫu âm thanh |
| **Latency** | Whisper: log thủ công / quan sát one-shot (~vài giây, phụ thuộc CPU); Vosk sandbox: nhẹ hơn nhưng grammar hạn chế |
| **Cách bổ sung** | Cùng 20–30 câu ghi âm → chạy cả hai engine → % khớp `commandId` + thời gian V→lệnh |

### Mẫu miệng (~ 45 giây)

> Em khảo sát cả Vosk và Whisper. Vosk có model tiếng Việt nhẹ và grammar tại decode — phù hợp lệnh cố định, nhưng grammar lớn dễ lỗi và kém linh hoạt phát âm. Whisper chạy local qua whisper.unity, `language=vi`, transcript đa dạng hơn rồi em fuzzy với JSON. Trận chính dùng `WhisperSttOnlyDriver` và `VoiceCommandOneShotCapture`, không dùng Vosk backend. Thư mục VoskModels và scene Vosk là prototype còn lại. Em chưa có bảng WER chính thức; so sánh chủ yếu định tính và có thể bổ sung đo N mẫu trước bảo vệ.

---

<a id="cau-8"></a>

## Câu 8 — Parser/Grammar có còn đúng nghĩa NLP?

**Hỏi:** Whisper không giới hạn từ vựng lúc giải mã — bù bằng fuzzy + JSON. “Parser/Grammar” trong sơ đồ có đúng nghĩa NLP không?

### Trả lời ngắn

**Không phải NLP parser đầy đủ** (không phân tích cú pháp Chủ–Vị–Ngữ, không slot-filling tổng quát).

Trong báo cáo, “Parser/Grammar” = **finite vocabulary command matching**:

1. **Chuẩn hóa** text STT  
2. **So khớp mờ** với mẫu trong JSON  
3. Trả **`commandId`** → gameplay  

→ Đúng hơn gọi là **command resolver / template matching**, không phải grammar engine kiểu Vosk hay NLU cloud.

**Grammar** trong thiết kế: **tập lệnh đóng** (`PrimaryPhrase` + `Aliases` trong JSON), không phải CFG/parser sinh câu tự do.

### Luồng thực tế trong code

```
Whisper (text tự do) → Normalizer → Levenshtein vs JSON → commandId → PlayerInput
```

| Bước | File |
|------|------|
| STT (không grammar) | `WhisperSttOnlyDriver.cs` |
| Chuẩn hóa (bỏ dấu, lowercase) | `RecognizedSpeechPhraseNormalizer.cs`, `VietnameseTextNormalizer.cs` |
| Dataset lệnh đóng | `rts_voice_commands_uts_units_vi.json` |
| So khớp Levenshtein | `StringSimilarity.cs` |
| Chọn commandId + ngưỡng | `RecognizedSpeechPhraseMapper.cs` |
| Facade resolver | `FuzzyVoiceCommandResolver.cs` |
| Profile + ngưỡng 0,72 / 0,58 | `VoiceCommandProfile.cs` (`MinSimilarity`, `PhraseSnapMinSimilarity`) |
| Nối STT → event | `VoiceCommandOneShotTranscriptMapper.cs` |
| commandId → hành động | `VoiceCommandIdActionRules.cs` → `PlayerInput.Voice.cs` |

### Grammar Vosk vs pipeline Whisper

| | Vosk (đã khảo sát) | Pipeline đã chọn |
|---|-------------------|------------------|
| Grammar | `VoiceCommandProfile.BuildVoskGrammarJson()` — giới hạn **lúc decode** | Không dùng |
| Sau STT | — | **Fuzzy** trên finite set |

### “Parser” trong code = gì?

| Thành phần | Có phải NLP parser? |
|------------|---------------------|
| `RecognizedSpeechPhraseNormalizer` | **Không** — tiền xử lý chuỗi |
| `RecognizedSpeechPhraseMapper` + `StringSimilarity` | **Không** — classification / nearest template |
| `VoiceCommandIdActionRules.TryParseSelectCommand` | **Rule đơn giản** — tách `select_3_warrior` (đã có `commandId`, không parse câu tự nhiên) |
| `docs/voice-command-dataset.vi.json` (`required_slots`) | **Thiết kế tương lai** — chưa slot-filling runtime |

### Một câu thống nhất báo cáo ↔ code

> Sơ đồ ghi “Parser/Grammar” theo nghĩa **lệnh có cấu trúc + từ điển đóng**; implementation là **normalizer + fuzzy matching + commandId**, không dùng parser ngữ pháp tiếng Việt.

### Mẫu miệng (~ 40 giây)

> Whisper xuất text mở, không bị grammar khóa lúc giải mã. Em bù bằng tập lệnh JSON hữu hạn: chuẩn hóa trong `RecognizedSpeechPhraseNormalizer`, so Levenshtein trong `RecognizedSpeechPhraseMapper`, ngưỡng 0,72 trong `VoiceCommandProfile`, rồi map `commandId` sang gameplay. Đây không phải NLP parser đầy đủ — là template matching trên finite command set. Grammar trong báo cáo là mô tả tập lệnh đóng; grammar Vosk trong `BuildVoskGrammarJson` chỉ là hướng đã thử, không dùng ở Whisper. Slot-filling từ câu tự nhiên là hướng mở rộng trong kiến nghị.

---

<a id="cau-9"></a>

## Câu 9 — `language = vi` và trade-off GGML multilingual

**Hỏi:** `language = vi`, mô hình **GGML multilingual**: trade-off giữa độ chính xác tiếng Việt và kích thước/tốc độ mô hình đã chọn?

### Trả lời ngắn

- Em ưu tiên **độ chính xác/robust** trước biến thể phát âm tiếng Việt, nên dùng **ggml-base.bin (multilingual)** (nặng hơn, inference chậm hơn).
- Để giảm tác động tốc độ, em **khóa** `language = vi`, `translateToEnglish = false`, bật **VAD** (`useVad = true`) và chạy **low-latency tuning** (stepSec/lengthSec, single-segment, drop buffer).
- Trong project có đường dẫn model **ggml-small-q8_0**: dùng khi máy yếu (nhẹ hơn), nhưng chấp nhận độ khớp lệnh có thể giảm → lúc đó cần tinh chỉnh ngưỡng fuzzy/tập lệnh.

### Code — nơi thể hiện trade-off

| Vai trò | File |
|---|---|
| Chọn model base vs small | `Assets/Scripts/SpeechRecognition/Core/WhisperSpeechDefaults.cs` (`BaseModelRelativePath`, `SmallQ8ModelRelativePath`) |
| Khóa `language=vi`, `translateToEnglish=false`, set `stepSec`, bật VAD | `Assets/Scripts/SpeechRecognition/Whisper/WhisperSpeechRecognitionBackend.cs` (`ApplyWhisperSettingsFromProfile`) |
| Set model path mặc định + low-latency settings | `Assets/Scripts/SpeechRecognition/Whisper/WhisperSttOnlyDriver.cs` (ConfigureWhisperManager) |
| Tuning giảm độ trễ | `Assets/Scripts/SpeechRecognition/Core/WhisperStreamingTuning.cs` |
| Prompt theo lệnh RTS (tăng đúng domain) | `Assets/Scripts/SpeechRecognition/Core/WhisperVoicePromptBuilder.cs` |

### Một câu nhớ

> Base model cho độ chính xác tiếng Việt tốt hơn; em “mua lại” tốc độ bằng `language=vi`, VAD và low-latency tuning. Model small q8 là phương án khi CPU/RAM hạn chế.

### Mẫu miệng (~ 45 giây)

> Em chọn GGML multilingual để Whisper chịu được biến thể phát âm tiếng Việt tốt hơn, nên ưu tiên **ggml-base.bin** dù nặng hơn và inference chậm hơn. Để giữ thời gian phản hồi, em khóa `language = vi`, tắt `translateToEnglish`, bật VAD (`useVad = true`) và dùng low-latency tuning thông qua `stepSec/lengthSec`, `singleSegment` và cơ chế drop buffer. Project cũng có `ggml-small-q8_0.bin` như phương án khi máy yếu; khi đổi sang small thì chấp nhận accuracy lệnh có thể giảm và sẽ tinh chỉnh ngưỡng fuzzy/tập lệnh.

---

<a id="cau-10"></a>

## Câu 10 — Inference luồng nền, FPS, phần cứng

**Hỏi:** Whisper chạy luồng nền — CPU yếu thì FPS và độ trễ STT thế nào? Ngưỡng phần cứng tối thiểu?

### Trả lời ngắn

- **Inference** qua `whisper.unity` + `async Task` (`InitModel`, `CreateStream`) — **không block** main thread kiểu sync, nhưng vẫn **ăn CPU** → máy yếu có thể **giật FPS** lúc Whisper chạy.
- Trận chính dùng **one-shot PTT** (nhấn V, tối đa 12s) — **không** bật mic/STT liên tục cả trận → giảm áp lực CPU so với streaming 24/7.
- **Độ trễ STT:** profile `LowLatency` (~1–3s mỗi lần infer, phụ thuộc CPU); end-to-end = thời gian nói + infer + fuzzy.
- **CPU yếu:** FPS có thể tụt khi infer; STT chậm hơn → đổi `ggml-small-q8_0` hoặc chấp nhận latency cao hơn.
- **Phần cứng đề xuất (MVP):** Windows, CPU **4 nhân** (i5/Ryzen 5 đời mới), **8 GB RAM**, ổ trống ~300 MB cho model; test bằng Unity Profiler.

### Code — luồng xử lý & giảm tải

| Việc | File |
|------|------|
| Async load model / stream | `WhisperSpeechRecognitionBackend.cs` (`WaitForModelLoadedAsync`, `CreateStream`) |
| One-shot PTT, max 12s | `VoiceCommandOneShotCapture.cs` (`_maxCaptureSeconds`, `BeginCaptureRoutine`) |
| Profile low-latency | `WhisperSttOnlyDriver.cs` → `WhisperStreamingTuning.ApplyLowLatency` |
| Tham số step/length/VAD | `WhisperStreamingTuning.cs` (`LowLatencyStepSec = 1f`, `lengthSec = 5f`) |
| Model ~148 MB | `StreamingAssets/Whisper/ggml-base.bin` (`README_WHISPER_MODEL.txt`) |
| Log model sẵn sàng | `VoiceCommandRuntimeDiagnostics.cs` |
| Chỉ chạy voice khi gameplay unlock | `GameplayStartupGate` (qua `PlayerInput` / loading — xem `docs/Game-Module-Guide.md`) |

### CPU yếu — ảnh hưởng gì?

| Hiện tượng | Nguyên nhân | Cách giảm |
|------------|-------------|-----------|
| FPS tụt nhẹ khi nhấn V | Whisper infer cùng CPU với game | One-shot ngắn; không stream liên tục |
| STT chậm 3–5s+ | Model base + CPU yếu | `ggml-small-q8_0.bin` hoặc `LowLatency` |
| Model chưa load | Lần đầu `InitModel` | Đợi log `[Voice] STT sẵn sàng` trước khi demo |

### Mẫu miệng (~ 45 giây)

> Whisper inference chạy qua whisper.unity bằng async Task nên không khóa main thread hoàn toàn, nhưng vẫn cạnh tranh CPU với game. Em giảm tải bằng one-shot push-to-talk trong `VoiceCommandOneShotCapture` — chỉ thu khi nhấn V, tối đa 12 giây, không STT liên tục cả trận. Low-latency tuning trong `WhisperStreamingTuning` nhắm độ trễ khoảng 1–3 giây tùy CPU. Máy yếu có thể giật FPS lúc infer và STT chậm hơn; em đề xuất CPU 4 nhân, 8 GB RAM, hoặc đổi sang model nhẹ hơn. Em kiểm tra bằng Unity Profiler khi bật/tắt voice, chưa có module đo FPS tự động trong code.

---

<a id="cau-11"></a>

## Câu 11 — Mirror + Telepathy vs Netcode for GameObjects

**Hỏi:** Vì sao chọn **Mirror + Telepathy** cho LAN, không dùng Netcode for GameObjects? Khi nào Mirror không còn phù hợp?

### Trả lời ngắn

- Em chọn Mirror vì **server-authoritative (Host-Client)**: client gửi yêu cầu, server xác thực rồi mới áp lệnh (giảm desync/cheat cho lệnh RTS).
- Trong code đã dùng cơ chế Mirror theo kiểu **command → server xử lý → sync qua SyncVar** (phù hợp pipeline voice/command của game).
- Netcode for GameObjects (Unity) sẽ yêu cầu **đổi kiến trúc networking** (NetworkObject/NetworkBehaviour, cơ chế RPC/ownership khác) nên không còn “cắm vào” hiện tại với ít sửa.
- Khi Mirror không phù hợp: cần **prediction/latency hiding phức tạp**, scale nhiều người chơi/độ trễ Internet, hoặc cần mô hình client-authoritative/dedicated orchestration theo chuẩn khác.

### Code — nơi thể hiện Mirror server-authoritative

| Vai trò | File |
|---------|------|
| Gửi lệnh lên server (`[Command]`) | `Assets/Scripts/Netplay/RtsUtsPlayerCommands.cs` |
| Nguồn sự thật “owner/phe” (`[SyncVar] hook`) | `Assets/Scripts/Netplay/RtsUtsNetworkEntity.cs` |
| Relay command client ↔ server | `Assets/Scripts/Netplay/RtsUtsClientCommandRelay.cs` |
| Bootstrap scene mạng | `Assets/Scripts/Netplay/RtsNetGameSceneBootstrap.cs`, `Assets/Scripts/Netplay/RtsMatchServerSpawnRunner.cs` |

*(Mirror transport Telepathy TCP dùng cho LAN trong setup project của em.)*

### Một câu nhớ

> Mirror phù hợp vì em đã làm RTS theo hướng “client gửi lệnh, server xác thực + SyncVar sync”; đổi sang Netcode for GameObjects sẽ tốn công refactor networking và authority.

### Mẫu miệng (~ 45 giây)

> Em chọn Mirror + Telepathy cho LAN vì kiến trúc lệnh của em cần server-authoritative: client chỉ gửi yêu cầu, server xử lý command và sync lại trạng thái qua SyncVar. Trong code, các lệnh voice/chuột đều lên server qua `[Command]` ở `RtsUtsPlayerCommands.cs`, và phe/owner được đồng bộ bằng `[SyncVar]` hook trong `RtsUtsNetworkEntity.cs`. Nếu dùng Netcode for GameObjects thì cơ chế NetworkObject/NetworkBehaviour và ownership/RPC khác nên phải refactor lớn, không còn cắm trực tiếp. Mirror sẽ kém phù hợp khi cần scale/latency online lớn hoặc yêu cầu prediction và authority theo kiểu khác.

---

*Câu 12+ sẽ bổ sung sau nếu có.*
