# URTS Architecture Survey Summary

## Mục tiêu khảo sát

Khảo sát toàn bộ kiến trúc URTS hiện có để chuẩn bị mở rộng theo hướng AoE-style:
- Units, Buildings, Resources, Upgrades/Tech
- Events/EventBus
- UI
- AI/Behavior
- Shared systems: Input, Selection, Fog of War, Commands

## Kết quả tổng quan

- Phạm vi chính đã khảo sát: `Assets/Scripts` (116 file C#).
- Kiến trúc hiện tại dùng kết hợp:
  - **Command pattern** (`BaseCommand`, `CommandContext`)
  - **Event-driven** (`Bus<T>`, `IEvent`, owner-scoped events)
  - **Data-driven** (`ScriptableObject` cho unit/building/upgrade/cost/supply)
  - **Behavior graph** cho execution cấp thấp (move/attack/gather/build)

## Luồng vận hành cốt lõi

1. `PlayerInput` nhận input và chọn object qua `ISelectable`.
2. Command được chọn từ UI (`CommandSelectedEvent`) hoặc right-click context.
3. `PlayerInput` tạo `CommandContext` và gọi `BaseCommand.Handle`.
4. `AbstractUnit`/`Worker`/`BaseBuilding` cập nhật trạng thái runtime + blackboard.
5. Behavior actions/channels thực thi logic chi tiết (pathing, gather, build, combat).
6. Entities raise event qua `Bus<T>`.
7. `RuntimeUI`, `Supplies`, `TechTreeSO`, `FogVisibilityManager` phản ứng theo event.

## Điểm mạnh kiến trúc

- Tách trách nhiệm tương đối tốt giữa input, command, behavior, data và UI.
- Sử dụng interface nhỏ (`ISelectable`, `IDamageable`, `IGatherable`, `ITransporter`, ...) giúp mở rộng dễ.
- Mô hình owner-scoped events phù hợp cho RTS nhiều phe.
- Data bằng ScriptableObject giúp thêm unit/building/upgrade mới nhanh.

## Điểm cần chú ý/rủi ro

- Một số class trung tâm có nguy cơ phình trách nhiệm:
  - `AbstractCommandable`
  - `BaseBuilding`
  - `RuntimeUI`
- `Supplies` đang trộn logic resource model với HUD (state + view trong cùng class).
- Upgrade theo `PropertyPath` (reflection) linh hoạt nhưng dễ vỡ khi rename property.
- Một số wiring quan trọng nằm trong Inspector/Behavior Graph asset, không thể nhìn đầy đủ chỉ từ C#.

## Ảnh hưởng tới roadmap AoE-style

- Có nền tảng tốt để mở rộng:
  - Nhiều resource type
  - Production queue và tech tree
  - Command + event bus giúp thêm logic mới ít đụng lõi
- Trước khi scale lớn (AI mạnh hơn, nhiều building/unit hơn), nên ưu tiên:
  - Tách resource model khỏi UI
  - Giảm coupling ở các class trung tâm
  - Chuẩn hóa thêm abstraction cho production/research/population

## Tham chiếu chi tiết

- Tài liệu chi tiết đầy đủ: `urts-architecture-map.md`
