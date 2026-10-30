# Specification: Drive Management & Student Workspace Reset

## 1. Yêu cầu chức năng (Functional Requirements)

### REQ-1: Gói tin mạng điều khiển (Protocol Command)
- Hệ thống phải định nghĩa mã lệnh `CommandType.CleanAndInitDrive` (hoặc tương đương) trong `LabManager.Protocol`.
- Gói tin payload `CleanAndInitDrivePacket` phải bao gồm:
  - `DriveLetter` (string): Ký tự ổ đĩa mục tiêu, mặc định là `D:\`.
  - `FolderNames` (List<string>): Danh sách tên các thư mục cần tạo sau khi dọn dẹp.

### REQ-2: Xác thực & Bảo vệ an toàn phía Client (Student Guardrails)
- Máy học sinh **PHẢI** từ chối thực thi và bỏ qua lệnh nếu `DriveLetter` trỏ tới ổ cài đặt hệ điều hành (ví dụ: `C:\` hoặc `Path.GetPathRoot(Environment.SystemDirectory)`).
- Chỉ cho phép dọn dẹp trên ổ đĩa dữ liệu không chứa Windows.

### REQ-3: Quy tắc dọn dẹp thư mục & tệp tin cũ
- Khi nhận lệnh hợp lệ, phía Student phải duyệt toàn bộ các thư mục và tệp tin cấp 1 trong thư mục gốc của ổ chỉ định.
- **Bỏ qua tuyệt đối** các mục hệ thống:
  - `$RECYCLE.BIN`
  - `System Volume Information`
  - Bất kỳ tệp/thư mục nào có thuộc tính `FileAttributes.System` hoặc `FileAttributes.Hidden` ở cấp gốc nếu cần thiết.
- Với mỗi thư mục / tệp tin của học sinh:
  - Thực hiện xóa đệ quy (`Directory.Delete(path, true)` hoặc `File.Delete(path)`).
  - Sử dụng khối `try...catch` độc lập cho từng mục. Nếu gặp file đang bị lock/chiếm dụng hoặc không có quyền truy cập, ghi log cảnh báo và tiếp tục xóa các mục còn lại, không được để crash hoặc dừng tiến trình.

### REQ-4: Khởi tạo danh sách thư mục mới
- Sau khi quá trình dọn dẹp hoàn tất, hệ thống duyệt danh sách `FolderNames`.
- Loại bỏ khoảng trắng đầu/cuối, bỏ qua các dòng trống.
- Lọc bỏ các ký tự không hợp lệ trên Windows (`\ / : * ? " < > |`).
- Thực hiện `Directory.CreateDirectory(...)` để tạo từng thư mục trên ổ đĩa.

### REQ-5: Giao diện Giáo viên & Xác nhận (Teacher UI)
- `Teacher.App` cung cấp nút / menu hành động: "Dọn dẹp & Khởi tạo ổ học sinh...".
- Hộp thoại (Dialog Window) bao gồm:
  - Hướng dẫn rõ ràng về thao tác xóa dữ liệu bài làm.
  - Vùng nhập văn bản (Multiline TextBox) để nhập hoặc dán danh sách tên thư mục (mỗi dòng 1 tên).
  - Checkbox xác nhận nguy hiểm: *"Tôi xác nhận xoá toàn bộ bài làm cũ trên ổ D của các máy trạm được chọn"*.
  - Nút thực thi chỉ kích hoạt khi checkbox đã được tích chọn.
  - Gửi lệnh tới danh sách các máy trạm đang được chọn (hoặc toàn bộ nếu không chọn riêng).
