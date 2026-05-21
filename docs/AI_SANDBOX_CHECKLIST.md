# AI Sandbox — Checklist test

Scene: `Assets/Scenes/AI_Sandbox.unity` (tạo bằng menu **RTS → AI → Create AI Sandbox Scene**).

Assets độ khó: **RTS → AI → Create Data_Re AI Assets** → `Assets/Data_Re/AI/Difficulty/`.

## Setup nhanh

1. Mở `AI_Sandbox.unity`.
2. Trên mỗi `AIController` (AIBot): gán `Difficulty Profile` = Easy / Medium / Hard hoặc dùng `SetDifficulty` từ code.
3. `AISandboxWinLoseChecklist` trên object Game Event Log — theo dõi Civil Central win/lose.
4. Play; quan sát khung **Game Event Log**.

## Win / lose — Civil Central (bắt buộc M6)

| # | Việc cần làm | Kỳ vọng |
|---|----------------|---------|
| 1 | Phá **Civil Central** phe AI (`Owner` ≠ Player1) | Log: **CHIẾN THẮNG — Đã phá hủy Civil Central của đối phương!**; checklist: `EnemyCivilCentralDestroyed` |
| 2 | Để AI/địch phá **Civil Central Player1** | Log: **THUA — Nhà chính (Civil Central) đã bị phá hủy!**; checklist: `PlayerCivilCentralDestroyed` |

Ghi chú: `GameEventLogMessageFormatter.FormatCivilCentralDestroyed` dùng góc nhìn **Player1** — phá nhà chính Player1 = thua.

## Checklist gameplay AI (sandbox)

- [ ] Registry nhận unit/building khi spawn/death
- [ ] Worker gather → return Store / Civil Central
- [ ] Train Worker tại Civil Central
- [ ] Build Store (đường về ngắn hơn)
- [ ] Build Barrack → train Warrior (hoặc mix quân)
- [ ] Build Forge → research 1 upgrade
- [ ] Defense tower tự bắn (không cần lệnh AI)
- [ ] Corral sinh food (passive)
- [ ] Scout / attack wave khi đủ quân (`Enable Attack` trên difficulty)
- [ ] AI **không** spam Stop / Cancel Building trên worker đang build

## Độ khó — smoke test

| Asset | Tick (s) | Attack | Worker cap |
|-------|----------|--------|------------|
| `AIDifficulty_Easy` | ~1.25 | Muộn (threshold cao) | 6 |
| `AIDifficulty_Medium` | ~0.65 | Chuẩn | 10 |
| `AIDifficulty_Hard` | ~0.35 | Sớm + counter | 14 |

Đổi SO trên `AIController` hoặc:

```csharp
aiController.SetDifficulty(sessionConfig, AIDifficultyLevel.Hard);
```

## Tài liệu liên quan

- `docs/HUONG_DAN_CHAY_AI.md` — gắn AI vào scene chính
- `docs/KINH_NGHIEM_AI.md` — pitfall
