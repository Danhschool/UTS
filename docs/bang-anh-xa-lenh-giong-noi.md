# Bảng nhận diện giọng nói và ánh xạ lệnh gameplay

Nguồn dữ liệu: `Assets/Resources/VoiceCommands/rts_voice_commands_uts_units_vi.json`  
Pipeline: Whisper (STT) → `RecognizedSpeechPhraseNormalizer` → `FuzzyVoiceCommandResolver` (Levenshtein, ngưỡng kích hoạt **0,72**) → **`commandId`** → handler gameplay.

Người chơi nhấn phím **V**, nói câu lệnh; hệ thống so khớp với **câu mẫu chính** (`PrimaryPhrase`) hoặc **biến thể** (`Aliases`), sau đó kích hoạt hành động tương ứng trong trận.

---

## Bảng lệnh tiêu biểu

*Bảng rút gọn cho báo cáo — các lệnh đại diện theo nhóm: dừng lại, chọn unit, thu gỗ, xây nhà, tuyển dân, nâng cấp máu, tấn công.*

| STT | Mã lệnh (`CommandId`) | Câu mẫu chính (nói vào micro) | Ánh xạ gameplay | Biến thể nhận diện (ví dụ) |
|-----|------------------------|-------------------------------|-----------------|-----------------------------|
| 1 | `stop` | **dung lai** | Dừng lại / hủy lệnh hiện tại | dừng lại, đứng yên, hủy lệnh |
| 2 | `select_idle_workers` | **chon dan ranh** | Chọn unit — dân / nông dân rảnh | chọn dân, nông dân rảnh, chọn 3 warrior, chọn cung thủ |
| 3 | `gather_wood` | **thu go** | Thu gỗ / chặt cây | thu gỗ, chặt cây, khai thác gỗ |
| 4 | `build_barracks` | **xay nha linh** | Xây nhà — doanh trại / nhà lính | xây nhà kho, xây lò rèn, xây tháp canh |
| 5 | `train_worker` | **tuyen dan** | Tuyển dân / huấn luyện nông dân | tuyển nông dân, tạo dân |
| 6 | `research_health` | **nang cap mau** | Nâng cấp máu / sinh lực | nâng cấp máu, upgrade health, tăng máu |
| 7 | `attack` | **tan cong** | Tấn công mục tiêu | tấn công, đánh, khai hỏa |

**Caption Word gợi ý:** *Bảng X. Các lệnh giọng nói tiêu biểu và ánh xạ hành động gameplay*

---

## Bảng 1 — Lệnh điều khiển quân (7 lệnh)

| STT | Mã lệnh (`CommandId`) | Câu mẫu chính | Ánh xạ gameplay | Biến thể nhận diện (ví dụ) |
|-----|------------------------|---------------|-----------------|-----------------------------|
| 1 | `stop` | dung lai | Dừng / hủy lệnh hiện tại của đơn vị đang chọn | dừng lại, đứng yên, hủy lệnh |
| 2 | `move` | di chuyen | Ra lệnh di chuyển (kết hợp click chuột chọn điểm đích) | di chuyển, đi đến đó |
| 3 | `attack` | tan cong | Ra lệnh tấn công mục tiêu | tấn công, đánh, khai hỏa |
| 4 | `attack_move` | tan cong di chuyen | Tấn công di chuyển (A-move) | a move, vừa đi vừa đánh |
| 5 | `hold_position` | giu vi tri | Giữ vị trí, không truy đuổi | giữ vị trí, đứng im bắn |
| 6 | `select_all_military` | chon toan bo quan | Chọn toàn bộ quân đội | chọn hết quân, chọn tất cả quân đội |
| 7 | `select_idle_workers` | chon dan ranh | Chọn dân / nông dân đang rảnh | nông dân rảnh, dân nhàn rỗi |

---

## Bảng 2 — Lệnh chọn nông dân / dân (10 lệnh)

| STT | Mã lệnh | Câu mẫu chính | Ánh xạ gameplay | Biến thể nhận diện (ví dụ) |
|-----|---------|---------------|-----------------|-----------------------------|
| 8 | `select_1_worker` | chon 1 dan | Chọn **1** nông dân/dân | chọn một dân, nông dân |
| 9 | `select_2_worker` | chon 2 dan | Chọn **2** nông dân/dân | chọn hai dân |
| 10 | `select_3_worker` | chon 3 dan | Chọn **3** nông dân/dân | chọn ba dân |
| 11 | `select_4_worker` | chon 4 dan | Chọn **4** nông dân/dân | chọn bốn dân |
| 12 | `select_5_worker` | chon 5 dan | Chọn **5** nông dân/dân | chọn năm dân |
| 13 | `select_6_worker` | chon 6 dan | Chọn **6** nông dân/dân | chọn sáu dân |
| 14 | `select_7_worker` | chon 7 dan | Chọn **7** nông dân/dân | chọn bảy dân |
| 15 | `select_8_worker` | chon 8 dan | Chọn **8** nông dân/dân | chọn tám dân |
| 16 | `select_9_worker` | chon 9 dan | Chọn **9** nông dân/dân | chọn chín dân |
| 17 | `select_10_worker` | chon 10 dan | Chọn **10** nông dân/dân | chọn mười dân |

---

## Bảng 3 — Lệnh chọn bộ binh / Warrior (10 lệnh)

| STT | Mã lệnh | Câu mẫu chính | Ánh xạ gameplay | Biến thể nhận diện (ví dụ) |
|-----|---------|---------------|-----------------|-----------------------------|
| 18 | `select_1_warrior` | chon 1 warrior | Chọn **1** bộ binh (Warrior) | chọn một bộ binh, chọn warrior |
| 19 | `select_2_warrior` | chon 2 warrior | Chọn **2** bộ binh | chọn hai bộ binh |
| 20 | `select_3_warrior` | chon 3 warrior | Chọn **3** bộ binh | chọn ba bộ binh |
| 21 | `select_4_warrior` | chon 4 warrior | Chọn **4** bộ binh | chọn bốn bộ binh |
| 22 | `select_5_warrior` | chon 5 warrior | Chọn **5** bộ binh | chọn năm bộ binh |
| 23 | `select_6_warrior` | chon 6 warrior | Chọn **6** bộ binh | chọn sáu bộ binh |
| 24 | `select_7_warrior` | chon 7 warrior | Chọn **7** bộ binh | chọn bảy bộ binh |
| 25 | `select_8_warrior` | chon 8 warrior | Chọn **8** bộ binh | chọn tám bộ binh |
| 26 | `select_9_warrior` | chon 9 warrior | Chọn **9** bộ binh | chọn chín bộ binh |
| 27 | `select_10_warrior` | chon 10 warrior | Chọn **10** bộ binh | chọn mười bộ binh |

---

## Bảng 4 — Lệnh chọn cung thủ / Archer (10 lệnh)

| STT | Mã lệnh | Câu mẫu chính | Ánh xạ gameplay | Biến thể nhận diện (ví dụ) |
|-----|---------|---------------|-----------------|-----------------------------|
| 28 | `select_1_archer` | chon 1 archer | Chọn **1** cung thủ (Archer) | chọn một cung thủ |
| 29 | `select_2_archer` | chon 2 archer | Chọn **2** cung thủ | chọn hai cung thủ |
| 30 | `select_3_archer` | chon 3 archer | Chọn **3** cung thủ | chọn ba cung thủ |
| 31 | `select_4_archer` | chon 4 archer | Chọn **4** cung thủ | chọn bốn cung thủ |
| 32 | `select_5_archer` | chon 5 archer | Chọn **5** cung thủ | chọn năm cung thủ |
| 33 | `select_6_archer` | chon 6 archer | Chọn **6** cung thủ | chọn sáu cung thủ |
| 34 | `select_7_archer` | chon 7 archer | Chọn **7** cung thủ | chọn bảy cung thủ |
| 35 | `select_8_archer` | chon 8 archer | Chọn **8** cung thủ | chọn tám cung thủ |
| 36 | `select_9_archer` | chon 9 archer | Chọn **9** cung thủ | chọn chín cung thủ |
| 37 | `select_10_archer` | chon 10 archer | Chọn **10** cung thủ | chọn mười cung thủ |

---

## Bảng 5 — Lệnh chọn đá binh / RockWarrior (10 lệnh)

| STT | Mã lệnh | Câu mẫu chính | Ánh xạ gameplay | Biến thể nhận diện (ví dụ) |
|-----|---------|---------------|-----------------|-----------------------------|
| 38 | `select_1_rockwarrior` | chon 1 rockwarrior | Chọn **1** đá binh (RockWarrior) | chọn một đá binh, rock warrior |
| 39 | `select_2_rockwarrior` | chon 2 rockwarrior | Chọn **2** đá binh | chọn hai đá binh |
| 40 | `select_3_rockwarrior` | chon 3 rockwarrior | Chọn **3** đá binh | chọn ba đá binh |
| 41 | `select_4_rockwarrior` | chon 4 rockwarrior | Chọn **4** đá binh | chọn bốn đá binh |
| 42 | `select_5_rockwarrior` | chon 5 rockwarrior | Chọn **5** đá binh | chọn năm đá binh |
| 43 | `select_6_rockwarrior` | chon 6 rockwarrior | Chọn **6** đá binh | chọn sáu đá binh |
| 44 | `select_7_rockwarrior` | chon 7 rockwarrior | Chọn **7** đá binh | chọn bảy đá binh |
| 45 | `select_8_rockwarrior` | chon 8 rockwarrior | Chọn **8** đá binh | chọn tám đá binh |
| 46 | `select_9_rockwarrior` | chon 9 rockwarrior | Chọn **9** đá binh | chọn chín đá binh |
| 47 | `select_10_rockwarrior` | chon 10 rockwarrior | Chọn **10** đá binh | chọn mười đá binh |

---

## Bảng 6 — Lệnh kinh tế và thu thập (2 lệnh)

| STT | Mã lệnh | Câu mẫu chính | Ánh xạ gameplay | Biến thể nhận diện (ví dụ) |
|-----|---------|---------------|-----------------|-----------------------------|
| 48 | `gather_wood` | thu go | Ra lệnh thu gỗ / chặt cây | thu gỗ, chặt cây, khai thác gỗ |
| 49 | `gather_stone` | thu da | Ra lệnh thu đá / đào đá | thu đá, đào đá, khai đá |

---

## Bảng 7 — Lệnh xây dựng công trình (5 lệnh)

| STT | Mã lệnh | Câu mẫu chính | Ánh xạ gameplay | Biến thể nhận diện (ví dụ) |
|-----|---------|---------------|-----------------|-----------------------------|
| 50 | `build_storehouse` | xay nha kho | Xây nhà kho (Storehouse) | xây kho, nhà kho |
| 51 | `build_forge` | xay lo ren | Xây lò rèn (Forge) | lò rèn, dựng lò rèn |
| 52 | `build_barracks` | xay nha linh | Xây doanh trại / nhà lính (Barracks) | xây barrack, doanh trại |
| 53 | `build_corral` | xay corral | Xây chuồng nuôi (Corral) | xây chuồng, chuồng nuôi |
| 54 | `build_defense_tower` | xay thap canh | Xây tháp canh (Defense Tower) | tháp canh, xây tháp |

---

## Bảng 8 — Lệnh huấn luyện / sản xuất đơn vị (4 lệnh)

| STT | Mã lệnh | Câu mẫu chính | Ánh xạ gameplay | Biến thể nhận diện (ví dụ) |
|-----|---------|---------------|-----------------|-----------------------------|
| 55 | `train_worker` | tuyen dan | Huấn luyện dân / nông dân (Worker) | tuyển dân, tuyển nông dân, tạo dân |
| 56 | `train_warrior` | tuyen warrior | Huấn luyện bộ binh (Warrior) | tuyển warrior, huấn luyện warrior |
| 57 | `train_archer` | tuyen archer | Huấn luyện cung thủ (Archer) | tuyển archer, huấn luyện cung thủ |
| 58 | `train_rockwarrior` | tuyen rockwarrior | Huấn luyện đá binh (RockWarrior) | tuyển rockwarrior, tạo rock warrior |

---

## Bảng 9 — Lệnh nghiên cứu / nâng cấp công nghệ (6 lệnh)

| STT | Mã lệnh | Câu mẫu chính | Ánh xạ gameplay | Biến thể nhận diện (ví dụ) |
|-----|---------|---------------|-----------------|-----------------------------|
| 59 | `research_damage` | nang cap sat thuong | Nghiên cứu tăng sát thương | nâng cấp damage, upgrade damage |
| 60 | `research_health` | nang cap mau | Nghiên cứu tăng máu / sinh lực | nâng cấp máu, upgrade health |
| 61 | `research_move_speed` | nang cap toc do di chuyen | Nghiên cứu tăng tốc độ di chuyển | nâng cấp tốc độ, upgrade move speed |
| 62 | `research_attack_delay` | nang cap toc do danh | Nghiên cứu tăng tốc độ đánh | đánh nhanh hơn, upgrade attack delay |
| 63 | `research_gather_amount` | nang cap luong thu thap | Nghiên cứu tăng lượng thu thập | thu thập nhiều hơn, upgrade gather amount |
| 64 | `research_gather_time` | nang cap toc do thu thap | Nghiên cứu tăng tốc độ thu thập | chặt gỗ nhanh hơn, upgrade gather time |

---

## Bảng 10 — Lệnh hủy tác vụ (3 lệnh)

| STT | Mã lệnh | Câu mẫu chính | Ánh xạ gameplay | Biến thể nhận diện (ví dụ) |
|-----|---------|---------------|-----------------|-----------------------------|
| 65 | `cancel_research` | huy nghien cuu | Hủy nghiên cứu đang thực hiện | hủy nghiên cứu, cancel research |
| 66 | `cancel_production` | huy san xuat | Hủy sản xuất / hàng đợi lính | hủy sản xuất, hủy hàng đợi |
| 67 | `cancel_building` | huy xay dung | Hủy xây dựng công trình | hủy xây dựng, hủy công trình |

---

## Tóm tắt theo nhóm

| Nhóm chức năng | Số lệnh | Mã lệnh (CommandId) |
|----------------|---------|---------------------|
| Điều khiển quân & chọn tổng | 7 | `stop`, `move`, `attack`, `attack_move`, `hold_position`, `select_all_military`, `select_idle_workers` |
| Chọn nông dân (1–10) | 10 | `select_1_worker` … `select_10_worker` |
| Chọn bộ binh (1–10) | 10 | `select_1_warrior` … `select_10_warrior` |
| Chọn cung thủ (1–10) | 10 | `select_1_archer` … `select_10_archer` |
| Chọn đá binh (1–10) | 10 | `select_1_rockwarrior` … `select_10_rockwarrior` |
| Thu thập tài nguyên | 2 | `gather_wood`, `gather_stone` |
| Xây dựng | 5 | `build_storehouse`, `build_forge`, `build_barracks`, `build_corral`, `build_defense_tower` |
| Huấn luyện | 4 | `train_worker`, `train_warrior`, `train_archer`, `train_rockwarrior` |
| Nghiên cứu | 6 | `research_damage`, `research_health`, `research_move_speed`, `research_attack_delay`, `research_gather_amount`, `research_gather_time` |
| Hủy tác vụ | 3 | `cancel_research`, `cancel_production`, `cancel_building` |
| **Tổng cộng** | **67** | |

---

## Ghi chú kỹ thuật

1. **Chuẩn hóa trước khi so khớp:** mọi câu nói và câu mẫu đều chuyển về dạng không dấu, chữ thường (ví dụ *"Dừng lại"* → `dung lai`).
2. **Ngưỡng khớp lệnh:** điểm tương đồng Levenshtein ≥ **0,72** (`MinSimilarity`) thì phát `commandId`; ngưỡng bám **0,58** (`PhraseSnapMinSimilarity`) chỉ dùng để gợi ý câu gần mẫu.
3. **Lệnh cần tương tác chuột:** `move`, `attack`, `attack_move`, `gather_*`, `build_*` sau khi nhận diện giọng nói vẫn cần người chơi **click** chọn vị trí hoặc mục tiêu trên bản đồ (chưa trích xuất tọa độ từ câu nói).
4. **Tài liệu thiết kế 35 ý định:** `docs/voice-command-dataset.vi.json` mô tả intent cấp cao (có slot địa điểm, mục tiêu…); bảng trên là **67 lệnh runtime** đã triển khai trong game UTS.

---

*Bảng phục vụ báo cáo đồ án — có thể copy vào Word và đặt caption: **Bảng X. Ánh xạ lệnh giọng nói tiếng Việt sang hành động gameplay**.*
