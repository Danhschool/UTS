# Luyện phản biện đồ án tốt nghiệp

**Đề tài:** Phát triển trò chơi chiến thuật thời gian thực tích hợp điều khiển bằng giọng nói  
**Sinh viên:** Nguyễn Danh Trường — MSSV 211200969 — KSCNTT2, Khóa 62  
**GVHD:** TS. Hoàng Văn Thông  
**Code tham chiếu:** `d:\Unity_3D\UTS`  
**Báo cáo:** `K62_211200969_NguyenDanhTruong_KSCNTT2.pdf`

Tài liệu dùng để **tập luyện trước hội đồng phản biện**: trả lời miệng, demo, và thống nhất giữa báo cáo – implementation.

**Gợi ý trả lời (ngắn + đường dẫn code):**

- Phần I (câu 1–5): [`Tra-Loi-Phan-Bien.md`](./Tra-Loi-Phan-Bien.md)
- Phần II (câu 6–11): [`Tra-Loi-Phan-Bien-II-Cong-Nghe.md`](./Tra-Loi-Phan-Bien-II-Cong-Nghe.md)

---

## Mục lục

1. [Mở đầu & định vị đề tài](#i-mở-đầu--định-vị-đề-tài)
2. [Công nghệ & lựa chọn giải pháp](#ii-công-nghệ--lựa-chọn-giải-pháp)
3. [Kiến trúc & thiết kế (Chương 2)](#iii-kiến-trúc--thiết-kế-chương-2)
4. [Nhận diện giọng nói & ánh xạ lệnh](#iv-nhận-diện-giọng-nói--ánh-xạ-lệnh)
5. [Gameplay RTS cốt lõi](#v-gameplay-rts-cốt-lõi)
6. [Mạng LAN & bảo mật (Mirror)](#vi-mạng-lan--bảo-mật-mirror)
7. [AI Bot (PvE)](#vii-ai-bot-pve)
8. [UI/UX & trải nghiệm](#viii-uiux--trải-nghiệm)
9. [Kiểm thử, đánh giá & hạn chế](#ix-kiểm-thử-đánh-giá--hạn-chế)
10. [Mở rộng & hướng nghiên cứu](#x-mở-rộng--hướng-nghiên-cứu)
11. [Câu hỏi khi demo trực tiếp](#xi-câu-hỏi-khi-demo-trực-tiếp)
12. [Gợi ý luyện tập & chuẩn bị](#gợi-ý-luyện-tập--chuẩn-bị)
13. [Điểm cần thống nhất báo cáo – code](#điểm-cần-thống-nhất-báo-cáo--code)

---

## I. Mở đầu & định vị đề tài


| #   | Câu hỏi                                                                                                                                                    |
| --- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 1   | **Đóng góp khoa học – kỹ thuật** của đề tài là gì, khác với “làm game Unity có mic” ở điểm nào? Có thể đo bằng chỉ số nào? → [gợi ý trả lời](./Tra-Loi-Phan-Bien.md#cau-1) |
| 2   | Vì sao chọn **RTS** làm bối cảnh cho giọng nói, không phải MOBA hay RTT? Nếu chuyển sang MOBA, pipeline giọng nói phải đổi gì? → [gợi ý trả lời](./Tra-Loi-Phan-Bien.md#cau-2) |
| 3   | Mục tiêu “**sẵn sàng phát hành**” trong Mở đầu khác “**MVP**” trong phạm vi (mục 1.3.3) như thế nào? Giải thích sự khác biệt cho hội đồng. → [gợi ý trả lời](./Tra-Loi-Phan-Bien.md#cau-3) |
| 4   | Đề tài giải quyết **giảm APM** — đã đo APM trước/sau khi dùng giọng nói chưa? Nếu chưa, làm sao chứng minh “giảm tải thao tác”? → [gợi ý trả lời](./Tra-Loi-Phan-Bien.md#cau-4) |
| 5   | **Phạm vi 20–30 lệnh** (Mở đầu) vs **35 ý định lệnh** (Kết luận) vs **29 lệnh** (Phụ lục) — con số chính thức là bao nhiêu và vì sao lệch giữa các chương? → [gợi ý trả lời](./Tra-Loi-Phan-Bien.md#cau-5) |


---

## II. Công nghệ & lựa chọn giải pháp

→ **Gợi ý trả lời:** [`Tra-Loi-Phan-Bien-II-Cong-Nghe.md`](./Tra-Loi-Phan-Bien-II-Cong-Nghe.md)

| #   | Câu hỏi                                                                                                                                                                             |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 6   | Trình bày lại **Bảng 1.1** (Google / Azure / Whisper): tiêu chí nào là *sống còn* với RTS, tiêu chí nào chỉ *ưu tiên*? → [gợi ý trả lời](./Tra-Loi-Phan-Bien-II-Cong-Nghe.md#cau-6) |
| 7   | Vì sao **Whisper** phù hợp hơn **Vosk** cho tiếng Việt trong game LAN, trong khi project vẫn có thư mục `StreamingAssets/VoskModels`? Có so sánh WER/latency giữa hai engine không? → [gợi ý trả lời](./Tra-Loi-Phan-Bien-II-Cong-Nghe.md#cau-7) |
| 8   | Whisper **không** giới hạn từ vựng ở tầng giải mã — bù bằng **fuzzy matching + JSON**. “Parser/Grammar” trong sơ đồ thiết kế có còn đúng nghĩa NLP không? → [gợi ý trả lời](./Tra-Loi-Phan-Bien-II-Cong-Nghe.md#cau-8) |
| 9   | `**language = vi`**, mô hình **GGML multilingual**: trade-off giữa độ chính xác tiếng Việt và kích thước/tốc độ mô hình đã chọn? → [gợi ý trả lời](./Tra-Loi-Phan-Bien-II-Cong-Nghe.md#cau-9) |
| 10  | Inference Whisper chạy **luồng nền** — nếu CPU yếu, FPS game và độ trễ STT xử lý thế nào? Ngưỡng phần cứng tối thiểu đề xuất? → [gợi ý trả lời](./Tra-Loi-Phan-Bien-II-Cong-Nghe.md#cau-10) |
| 11  | Vì sao **Mirror + Telepathy** cho LAN, không dùng Netcode for GameObjects? Khi nào Mirror không còn phù hợp? → [gợi ý trả lời](./Tra-Loi-Phan-Bien-II-Cong-Nghe.md#cau-11) |


**Tham chiếu code:** `Assets/Scripts/SpeechRecognition/Whisper/`, `Assets/StreamingAssets/Whisper/`

---

## III. Kiến trúc & thiết kế (Chương 2)

→ **Gợi ý trả lời:** [`Tra-Loi-Phan-Bien-III-Kien-Truc.md`](./Tra-Loi-Phan-Bien-III-Kien-Truc.md)

| #   | Câu hỏi                                                                                                                                               |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| 12  | Vẽ lại **pipeline 7 bước** (mic → ASR → chuẩn hóa → map → xác thực server → thực thi → sync). Bước nào **client-only**, bước nào **bắt buộc server**? → [gợi ý trả lời](./Tra-Loi-Phan-Bien-III-Kien-Truc.md#cau-12) |
| 13  | Tuân **SOLID** và **6 phân hệ** — chỉ ra **một interface** và **một class** minh họa **SRP** và **DIP** (không nói chung chung). → [gợi ý trả lời](./Tra-Loi-Phan-Bien-III-Kien-Truc.md#cau-13) |
| 14  | **Command Pattern** + **Event Bus theo phe** — tại sao tách hai lớp? Một lệnh `MoveCommand` đi qua những class nào từ input đến unit? → [gợi ý trả lời](./Tra-Loi-Phan-Bien-III-Kien-Truc.md#cau-14) |
| 15  | **Ma trận trách nhiệm** (Bảng 2.2): thêm lệnh giọng nói mới — sửa file/module nào *tối thiểu*? → [gợi ý trả lời](./Tra-Loi-Phan-Bien-III-Kien-Truc.md#cau-15) |
| 16  | **ScriptableObject** vs **JSON** cho lệnh thoại — khi nào dùng SO, khi nào JSON, ai là “source of truth” lúc runtime? → [gợi ý trả lời](./Tra-Loi-Phan-Bien-III-Kien-Truc.md#cau-16) |
| 17  | Một luồng “nói *tấn công* → unit đánh” trên Sequence diagram — có khớp `PlayerInput` / `RtsUtsPlayerCommands` không? → [gợi ý trả lời](./Tra-Loi-Phan-Bien-III-Kien-Truc.md#cau-17) |


**Tham chiếu code:** `Assets/Scripts/Commands/`, `Assets/Scripts/SpeechRecognition/Core/FuzzyVoiceCommandResolver.cs`, `docs/Game-Module-Guide.md`

---

## IV. Nhận diện giọng nói & ánh xạ lệnh

*Trọng tâm đề tài — nên luyện kỹ nhất.*


| #   | Câu hỏi                                                                                                                                                 |
| --- | ------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 18  | Giải thích **hai ngưỡng 0,58 và 0,72** (bám / kích hoạt). Hạ ngưỡng → tăng recall, tăng false positive — cân bằng thế nào trong trận thật?              |
| 19  | **Levenshtein** trên chuỗi **đã bỏ dấu** — hai lệnh khác nghĩa nhưng gần giống ký tự (đồng âm không dấu) xử lý ra sao?                                  |
| 20  | `**WhisperVoicePromptBuilder`**: prompt ảnh hưởng WER thế nào? Có ablation test (bật/tắt prompt) không?                                                 |
| 21  | **Push-to-talk phím V**, giới hạn **12 giây** one-shot — vì sao không streaming liên tục trong trận? Ưu/nhược với RTS đa tác vụ?                        |
| 22  | Kết luận ghi “**nhấn giữ phím V**” nhưng mục 4.4.3 mô tả **nhấn** — hành vi thực tế trong build là gì?                                                  |
| 23  | Lệnh `move` / `attack` cần **click chọn đích** sau giọng nói — có phải “điều khiển hoàn toàn bằng giọng”? % thao tác chuột còn lại ước lượng bao nhiêu? |
| 24  | `**VoiceCommandIdActionRules`** vs `**UnityEvent` trong Inspector** — luồng nào là chính thức trên bản demo phản biện?                                  |
| 25  | Lệnh `select_n_worker` — STT nhận “chọn ba dân” nhưng **slot-filling** chưa có. Hiện tại xử lý số lượng thế nào?                                        |
| 26  | Whisper trả về câu **không thuộc tập lệnh** — UX phản hồi gì? `GameEventLog` đủ để người chơi tự sửa phát âm không?                                     |


**Tham chiếu code:**

- `Assets/Scripts/SpeechRecognition/Core/FuzzyVoiceCommandResolver.cs`
- `Assets/Scripts/SpeechRecognition/Core/VietnameseTextNormalizer.cs`
- `Assets/Scripts/SpeechRecognition/VoiceCommandPushToTalkInput.cs`
- `Assets/Scripts/Player/PlayerInput.Voice.cs`
- Dataset: `voice-command-dataset.vi.json` (StreamingAssets hoặc profile tương ứng)

---

## V. Gameplay RTS cốt lõi


| #   | Câu hỏi                                                                                                      |
| --- | ------------------------------------------------------------------------------------------------------------ |
| 27  | Ba tài nguyên (gỗ/đá/lương thực) vs yêu cầu ghi **“gỗ, vàng”** — sai sót tài liệu hay thay đổi thiết kế?     |
| 28  | **NavMesh** + **formation vuông** — ai tính path, ai tính formation? Xung đột khi nhiều unit cùng di chuyển? |
| 29  | **Behavior Graph** (Hình 4.1) vs **AI Planner** — ranh giới hành vi vi mô unit và chiến lược vĩ mô bot?      |
| 30  | **Fog of war** — đồng bộ LAN: mỗi client thấy gì? `OwnerFogVisionLayers` khi host/client khác team?          |
| 31  | Điều kiện **thắng/thua** — server quyết định hay client? Race condition khi hai nhà chính cùng bị phá?       |


---

## VI. Mạng LAN & bảo mật (Mirror)


| #   | Câu hỏi                                                                                                                              |
| --- | ------------------------------------------------------------------------------------------------------------------------------------ |
| 32  | **Host = Server + Client**: host thoát giữa trận — hệ thống xử lý thế nào?                                                           |
| 33  | `**[Command]`** trong `RtsUtsPlayerCommands`: server kiểm tra **quyền sở hữu** (`UtsOwner`) thế nào? Client gửi `netId` giả thì sao? |
| 34  | Lệnh từ **giọng nói** trên LAN có đi qua `**CmdUts*`** giống chuột không, hay chỉ local? Có lỗ hổng desync/cheat không?              |
| 35  | **SyncVar** `UtsOwner` — hook đổi owner ảnh hưởng fog và commandable? Vì sao code nhắc “tránh team 0 lệch”?                          |
| 36  | Đồng bộ **scene** (Loading → Game): client join muộn có đủ trạng thái spawn không?                                                   |
| 37  | **Telepathy TCP** vs UDP cho RTS — trade-off độ trễ vs tin cậy đã chấp nhận?                                                         |


**Tham chiếu code:** `Assets/Scripts/Netplay/RtsUtsPlayerCommands.cs`, `RtsUtsNetworkEntity.cs`, `docs/Game-Module-Guide.md` (luồng Loading)

---

## VII. AI Bot (PvE)


| #   | Câu hỏi                                                                                                                          |
| --- | -------------------------------------------------------------------------------------------------------------------------------- |
| 38  | Báo cáo ghi AI dùng **FSM**; code có **AIController + 3 planner + priority queue**. Thống nhất thuật ngữ trước hội đồng thế nào? |
| 39  | `**AIWorldStateSnapshot`** — dữ liệu chỉ đọc, ai ghi, có lock khi unit chết giữa chu kỳ ~1 giây?                                 |
| 40  | Ba planner (kinh tế / cơ sở / quân sự) — xung đột cùng một worker: `**AIWorkerCommandGuard**` ưu tiên thế nào?                   |
| 41  | `**AIInfluenceMap**` dùng cho quyết định gì? Ví dụ một intent tấn công sinh từ influence.                                        |
| 42  | `**AIDifficultySO**` — thay đổi tham số nào? Có playtest tỷ lệ thắng theo mức khó?                                               |
| 43  | Trận **LAN 1v1**: `AIController` bị tắt — quên tắt thì hậu quả gì? Cơ chế tắt ở đâu?                                             |


**Tham chiếu code:** `Assets/Scripts/AI/Core/AIController.cs`, `Assets/Scripts/PvAI/`

---

## VIII. UI/UX & trải nghiệm


| #   | Câu hỏi                                                                                                 |
| --- | ------------------------------------------------------------------------------------------------------- |
| 44  | Tiêu chí UI/UX (Chương 3) — đã **đo** (SUS, thời gian nhiệm vụ, % lệnh voice đúng) hay mới **đề xuất**? |
| 45  | Phản hồi giọng nói trên HUD: phân biệt “nghe nhầm” vs “lệnh không hỗ trợ” vs “thiếu tài nguyên”?        |
| 46  | **Lobby LAN** — từ tạo phòng → vào trận mất bao nhiêu bước? Điểm gãy UX thường gặp?                     |


---

## IX. Kiểm thử, đánh giá & hạn chế


| #   | Câu hỏi                                                                                                                                               |
| --- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| 47  | Mục tiêu đo **latency STT, accuracy, FPS, ổn định LAN** — kết quả số liệu nằm ở đâu trong báo cáo? Nếu chưa có bảng số, bổ sung thế nào trước bảo vệ? |
| 48  | **Kịch bản kiểm thử** (2.6.7) — 3 testcase voice + 3 testcase network đã chạy thực tế?                                                                |
| 49  | Môi trường test: mic nào, mức ồn, size mô hình Whisper (`tiny` / `base` / …)?                                                                         |
| 50  | **Hạn chế lớn nhất** của đề tài — tự nêu trước khi hội đồng hỏi.                                                                                      |
| 51  | Tham khảo **GameDev.tv RTS Course** — phần reuse vs tự viết? Tránh trùng lặp / bản quyền?                                                             |
| 52  | Tại sao **không có chương riêng thử nghiệm định lượng** dù Mở đầu nhấn mạnh đo lường?                                                                 |


---

## X. Mở rộng & hướng nghiên cứu


| #   | Câu hỏi                                                                                            |
| --- | -------------------------------------------------------------------------------------------------- |
| 53  | **Slot-filling** (“chọn 5 cung thủ tại A”) — kiến trúc hiện tại cần thêm lớp nào, không phá SOLID? |
| 54  | **On-device LLM** cho lệnh tự nhiên — so với JSON + Levenshtein: rủi ro latency và multiplayer?    |
| 55  | **Matchmaking Internet** — từ LAN Mirror sang dedicated server cần refactor những `RtsUts`* nào?   |


*Khớp Kiến nghị trong Kết luận (ngắn / trung / dài hạn).*

---

## XI. Câu hỏi khi demo trực tiếp


| #   | Tình huống hội đồng                  | Cần trả lời / chứng minh                  |
| --- | ------------------------------------ | ----------------------------------------- |
| 56  | “Nói **tấn công** không click”       | Game làm gì? (arm command / log cảnh báo) |
| 57  | “Hai người cùng nói lệnh trong LAN”  | Xử lý authority và mic từng client        |
| 58  | “Whisper nhận **tiếng Anh** lẫn vào” | `language=vi`, normalizer, ngưỡng fuzzy   |
| 59  | “Host lag, client vẫn ra lệnh voice” | Desync? Command có lên server?            |
| 60  | “Sửa ngưỡng fuzzy khi đang chạy”     | Cần restart / reload profile?             |


---

## Gợi ý luyện tập & chuẩn bị

### Lịch 3 buổi (gợi ý)


| Buổi  | Nội dung                                                        |
| ----- | --------------------------------------------------------------- |
| **1** | Câu mục I, IV, IX — đóng góp, voice, số liệu, hạn chế           |
| **2** | Vẽ pipeline + LAN authority; demo PvE + 1 trận LAN              |
| **3** | Mock hội đồng: 5 câu ngẫu nhiên từ VI–VII, trả lời ≤ 2 phút/câu |


### Tài liệu nên có trước bảo vệ

- 1 slide **số liệu**: latency STT (trung bình), % lệnh đúng (N mẫu), FPS min/avg
- 1 trang **“thiết kế ↔ code”** (FSM vs Planner, Parser vs Fuzzy, voice trên LAN)
- Bảng lệnh thống nhất với `voice-command-dataset.vi.json` (29 / 35 — chốt một con số)

### Mẫu trả lời ngắn (khung 2 phút)

```
1. Ý chính (1 câu)
2. Cách hệ thống làm (2–3 câu, chỉ class/luồng chính)
3. Giới hạn / hướng cải thiện (1 câu)
```

---

## Điểm cần thống nhất báo cáo – code


| Chủ đề        | Trong báo cáo               | Trong code / ghi chú                                   |
| ------------- | --------------------------- | ------------------------------------------------------ |
| Số lệnh thoại | 20–30 / 35 / 29 (phụ lục)   | Đếm từ dataset JSON thực tế                            |
| AI            | FSM (Chương 1, 2)           | `AIController` + planners + queue                      |
| Parser        | Grammar / phân tích cú pháp | Fuzzy + normalizer (finite vocabulary)                 |
| Tài nguyên    | Gỗ, vàng (yêu cầu 2.1.3)    | Gỗ, đá, lương thực                                     |
| ASR           | Whisper (chính)             | Có thư mục Vosk (legacy / thử nghiệm?)                 |
| Voice MP      | Pipeline có bước server     | Kiểm tra `PlayerInput.Voice` vs `RtsUtsPlayerCommands` |
| PTT           | “Giữ V” vs “nhấn V”         | `VoiceCommandPushToTalkInput` / one-shot capture       |


*Cập nhật bảng này sau khi chỉnh báo cáo hoặc code — tránh bị hỏi “sách nói một đằng, demo một nẻo”.*

---

## Phụ lục: Danh sách lệnh thoại (tham chiếu báo cáo)

Báo cáo Phụ lục liệt kê 29 mã lệnh (`stop`, `move`, `attack`, `gather_`*, `build_*`, `train_*`, `research_*`, …). Khi phản biện, nên mở file dataset và đối chiếu **alias** + **ngưỡng fuzzy** cho 3–5 lệnh hay dùng demo.

---

*Tài liệu luyện tập — không thay thế báo cáo chính thức. Cập nhật khi chỉnh code hoặc bản PDF đồ án.*