HUD session — chỉ asset + thao tác Editor (không cần viết code)
================================================================

Mục tiêu: đưa ảnh panel/icon từ bản public mod (GPL) vào Unity, gán vào prefab HUD.

A) Chuẩn bị file ảnh (ngoài Unity)
---------------------------------
1. Từ bản cài game / mod public, tìm thư mục kiểu gui/session (panel HUD, icon
   tài nguyên, khung minimap). File thường là PNG.
2. Sao chép PNG vào thư mục này trong project:
   Assets/Art/UI/InGame/ThirdParty_SourceSessionGui/
3. Pháp lý: nội dung public mod thường GPLv2 — đọc license trước khi dùng trong
   sản phẩm thương mại. Không chắc → chỉ tham khảo bố cục và vẽ lại asset riêng.

B) Import trong Unity
---------------------
1. Chọn từng texture trong Project.
2. Inspector → Texture Type: Sprite (2D and UI).
3. Sprite Mode:
   - Single: nếu mỗi file là một hình độc lập.
   - Multiple: nếu một sheet chứa nhiều vùng — mở Sprite Editor, Slice (tự động
     hoặc theo lưới), Apply.
4. Pixels Per Unit: thử 100 trước; chỉnh nếu kích thước trên Canvas quá to/nhỏ.
5. (Tuỳ chọn) Tạo Sprite Atlas để gom draw call: menu Window → 2D → Sprite Atlas.

C) Gán vào HUD (prefab bạn import từ khóa / package)
---------------------------------------------------
1. Mở prefab HUD runtime trong Project (ví dụ prefab export từ course) — Prefab Mode.
2. Chọn từng Image cần skin (ví dụ Supplies Background, Resource Icon, Border,
   Minimap, …).
3. Inspector → Image → Source Image: kéo sprite đã import vào.
4. Image Type: Simple hoặc Sliced (9-slice) nếu panel có viền — với Sliced cần
   border đúng trong Sprite Editor.
5. Chỉnh Color nếu muốn nhạt/đậm; tắt Raycast Target trên ảnh trang trí để không
   chặn click xuống game (giữ Raycast chỉ trên nút sau này).

D) Safe area (notch / thanh home điện thoại) — không cần code nếu dùng gói có sẵn
---------------------------------------------------------------------------------
- Có thể thêm component “Safe Area” từ asset miễn phí trên Asset Store, hoặc
  chỉnh tay RectTransform lề trên/dưới theo tỉ lệ màn hình trong Game view.

E) Kiểm tra
-----------
1. Đặt prefab vào scene test hoặc scene chơi; có EventSystem trong scene.
2. Game view: kiểm tra panel không méo, 9-slice không vỡ góc.
3. Build target khác độ phân giải: Canvas Scaler trên root HUD (Scale With
   Screen Size) đã có trong prefab — chỉnh Reference Resolution nếu cần.

Thư mục này chỉ chứa asset nguồn + README; không bắt buộc script C# để “có ảnh”.
