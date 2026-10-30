# Tasks: Dọn dẹp & Khởi tạo ổ đĩa học sinh

## Phase 1: Protocol & Network Definition
- [x] 1.1 Thêm `CleanAndInitDrive = 431` vào `LabManager.Core.Enums.CommandType`.
- [x] 1.2 Tạo lớp `CleanAndInitDrivePacket.cs` trong `LabManager.Protocol.Packets`.
- [x] 1.3 `PacketSerializer` generic JSON hỗ trợ tự động không cần type mapping.

## Phase 2: Student Execution Logic
- [x] 2.1 Tạo `CleanAndInitDriveHandler.cs` trong `LabManager.Student.Core.Handlers`.
- [x] 2.2 Triển khai kiểm tra an toàn: ngăn chặn xóa ổ OS/ổ C:.
- [x] 2.3 Triển khai hàm xóa đệ quy có xử lý ngoại lệ cho file locked và bỏ qua `$RECYCLE.BIN`, `System Volume Information`, `Recovery`, `$WinREAgent`.
- [x] 2.4 Triển khai tạo thư mục mới từ danh sách tên kèm làm sạch ký tự không hợp lệ (`Path.GetInvalidFileNameChars()`).
- [x] 2.5 Đăng ký và gọi `CleanAndInitDriveHandler` trong `StudentCoordinator.cs`.

## Phase 3: Teacher UI & Interaction
- [x] 3.1 Thiết kế cửa sổ nhập liệu `CleanAndInitDriveWindow.xaml` trong `LabManager.Teacher.App.Views`.
- [x] 3.2 Tạo `CleanAndInitDriveViewModel.cs` kết nối `ClassroomManager.SendCleanAndInitDriveAllAsync` / `SendCleanAndInitDriveToTargetsAsync`.
- [x] 3.3 Tích hợp `CleanAndInitDriveCommand` và `CleanAndInitStudentDriveCommand` vào `DashboardViewModel.cs`.
- [x] 3.4 Thêm MenuItem trên thanh công cụ Nhóm Tiện ích & Bài tập và ContextMenu máy học sinh trong `MainWindow.xaml`.

## Phase 4: Testing & Verification
- [x] 4.1 Viết Unit Test cho `CleanAndInitDriveTests` (packet serialization/deserialization, reject ổ C:, reject ổ không tồn tại).
- [x] 4.2 Chạy toàn bộ test suite thành công (60/60 tests passed).
