# Proposal: Dọn dẹp & Khởi tạo ổ đĩa học sinh (Clean and Initialize Student Drive)

## 1. Bối cảnh & Lý do (Why)
Trong các phòng máy trường học sử dụng **LabManager**, sau mỗi năm học hoặc mỗi đợt thi/kỳ học, ổ đĩa `D:\` của các máy trạm học sinh chứa rất nhiều bài tập cũ, tài liệu rác do học sinh khóa trước để lại.
Hiện tại, giáo viên hoặc cán bộ quản lý phòng máy phải đi từng máy tính hoặc thao tác thủ công rất mất thời gian để dọn dẹp và tạo lại danh sách thư mục mới (ví dụ danh sách các lớp: `10A1`, `10A2`, `11A1`,...).

## 2. Mục tiêu (What)
Xây dựng tính năng cho phép giáo viên:
1. Chọn một hoặc toàn bộ máy trạm học sinh từ giao diện điều khiển `Teacher.App`.
2. Mở hộp thoại nhập/dán danh sách tên các thư mục cần tạo mới (mỗi dòng 1 tên).
3. Xác nhận cảnh báo an toàn và gửi lệnh đồng loạt xuống các máy con qua mạng.
4. Máy trạm học sinh (`Student.Core` / `StudentCoordinator`) tự động:
   - Quét và xoá toàn bộ dữ liệu bài làm cũ trên ổ `D:\` (bỏ qua các thư mục hệ thống như `$RECYCLE.BIN`, `System Volume Information`).
   - Khởi tạo danh sách các thư mục mới theo danh sách giáo viên đã cung cấp.

## 3. Phạm vi (Scope)
- **Trong phạm vi (In Scope):**
  - Chỉ tác động trên ổ đĩa học sinh (`D:\`).
  - Xử lý bỏ qua thư mục ẩn/hệ thống và bắt lỗi từng file/folder đang bị khóa (in-use) mà không làm dừng cả quá trình.
  - Hỗ trợ nhập danh sách tên lớp/thư mục qua multiline text trên `Teacher.App`.
  - Cơ chế cảnh báo xác nhận bắt buộc trước khi thực thi lệnh xoá.
- **Ngoài phạm vi (Out of Scope):**
  - Không format ổ đĩa vật lý (chỉ xóa nội dung cấp con).
  - Không hỗ trợ xóa trên ổ hệ điều hành (`C:\`).
