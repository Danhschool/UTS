# Phím tắt UTS — danh sách đã có trong game

**Cập nhật:** 2026-05-30  
**Hướng dẫn người chơi (JSON):** [hotkeys_uts_vi.json](hotkeys_uts_vi.json)

---

## Chuột

- [x] Click trái — chọn unit/công trình
- [x] Kéo chuột trái — chọn nhiều unit
- [x] Double click trái — chọn cùng loại trên màn hình
- [x] Click phải — ra lệnh
- [x] Shift + kéo — thêm vào lựa chọn (một phần)

## Hệ thống

- [x] **Esc** — `CancelSelection`

## Chọn nhanh (HotkeySystem)

- [x] **Q / Ctrl+Q** — Worker
- [x] **W / Ctrl+W** — Warrior
- [x] **E / Ctrl+E** — Archer
- [x] **R / Ctrl+R** — RockWarrior
- [x] **A / Ctrl+A** — Civil Central
- [x] **S / Ctrl+S** — Forge
- [x] **D / Ctrl+D** — Barracks

## Thanh lệnh & unit

- [x] **1 … 9** — `ActionBarSlot`
- [x] **H** — `StopUnits` (MP relay stop)

## Camera (binding / legacy)

- [x] **↑↓←→** — pan (`PlayerInput`)
- [x] Con lăn — zoom
- [x] **Home** — `CameraReset`
- [x] **F** — `CameraFollowUnit`

## Đặt công trình

- [x] **Esc** — hủy ghost (cùng CancelSelection)
- [x] Click phải — đặt nhà

---

Khi thêm phím mới: cập nhật `HotkeyId`, `HotkeyDefaults`, handler, **và** `hotkeys_uts_vi.json` + mục tương ứng trong `game_manual_vi.json`.
