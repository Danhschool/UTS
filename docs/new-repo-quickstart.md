# New Repo Quickstart (30-60 phút)

Mục tiêu: vào repo mới và bắt đầu code ngay theo cùng kiến trúc/chuẩn như URTS hiện tại.

## 0) Chuẩn bị (5 phút)

1. Copy `docs/` và `.cursor/rules/` từ repo cũ.
2. Đảm bảo project Unity mở được và compile không lỗi cơ bản.
3. Tạo branch làm việc riêng cho migration/setup.

## 1) Dựng khung kiến trúc (10-15 phút)

1. Tạo khung thư mục `Assets/Scripts` theo các domain chuẩn.
2. Tạo core contracts:
   - `IEvent` + `Bus<T>`
   - `ICommand` + `BaseCommand` + `CommandContext`
   - `ISelectable`, `IDamageable`, `IGatherable`
3. Tạo enum `Owner` dùng xuyên suốt event/resource/tech.

## 2) Dựng data model tối thiểu (10 phút)

1. Tạo ScriptableObject tối thiểu:
   - `AbstractUnitSO`, `UnitSO`, `BuildingSO`
   - `SupplySO`, `SupplyCostSO`
   - `UnlockableSO`, `UpgradeSO`, `TechTreeSO`
2. Tạo vài asset demo cho test sandbox:
   - 1 unit worker
   - 1 building command post
   - 1 resource node

## 3) Dựng runtime flow tối thiểu (10-15 phút)

1. `PlayerInput`:
   - Chọn object qua `ISelectable`
   - Dispatch command qua `BaseCommand`
2. `RuntimeUI`:
   - Hiển thị command cơ bản
3. `Supplies` (hoặc resource store):
   - Subscribe `SupplyEvent`
4. `FogVisibilityManager` (nếu dùng fog):
   - Cập nhật `IHideable`

## 4) Smoke test bằng sandbox (10-15 phút)

1. Gather test:
   - Worker gather resource -> return -> `SupplyEvent` cập nhật HUD
2. Build test:
   - Worker place/build building -> event spawn/death chạy đúng
3. Combat test:
   - Unit detect enemy và attack được (nếu combat đã dựng)

## 5) Tiêu chí sẵn sàng mở rộng

- Command flow hoạt động ổn: input -> command -> entity.
- Event flow hoạt động ổn: entity -> bus -> UI/tech/resource.
- Có ít nhất 1 sandbox scene độc lập để regression test nhanh.
- Team/AI thống nhất dùng `docs/README.md` làm entrypoint.
