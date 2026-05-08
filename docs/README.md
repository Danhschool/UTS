# URTS Documentation Hub

Thư mục `docs` này là bộ tài liệu tổng hợp để:
- Hiểu nhanh kiến trúc hiện tại của URTS.
- Dùng lại chuẩn làm việc khi chuyển sang repo mới.
- Làm nền để mở rộng theo plan AoE-style.

## 1) Tài liệu chính

- `urts-architecture-map.md`  
  Bản đồ kiến trúc chi tiết theo domain: class/interface/struct/enum, chức năng, method chính và quan hệ giao tiếp.

## 2) Tài liệu tổng hợp & vận hành

- `survey-summary.md`  
  Tóm tắt toàn bộ khảo sát ở mức điều hành: luồng hệ thống, điểm mạnh, rủi ro, ưu tiên refactor.

- `repo-migration-checklist.md`  
  Checklist chuyển sang repo mới: cần mang gì, setup gì, kiểm tra gì để bắt đầu code ngay.

- `new-repo-quickstart.md`  
  Quy trình 30-60 phút để khởi tạo môi trường code trong repo mới theo đúng chuẩn hiện tại.

## 3) Cách sử dụng nhanh

1. Đọc `survey-summary.md` để có bức tranh lớn trong 5-10 phút.  
2. Mở `urts-architecture-map.md` khi cần tra chi tiết class và quan hệ.  
3. Khi đổi repo, làm theo `repo-migration-checklist.md` + `new-repo-quickstart.md`.

## 4) Phạm vi và giới hạn

- Tài liệu được tổng hợp chủ yếu từ `Assets/Scripts`.
- Một phần wiring runtime nằm ở Unity Inspector/Behavior Graph assets nên cần kiểm tra thêm trong Editor nếu muốn 100% mapping.
