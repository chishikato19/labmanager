# Design: Dọn dẹp & Khởi tạo ổ đĩa học sinh

## 1. Kiến trúc tổng thể (Architecture Overview)

Tính năng tuân thủ kiến trúc Client-Server (Teacher - Student) hiện có của LabManager:

```
[Teacher UI Dialog] 
       │ (User inputs folder list & confirms)
       ▼
[DashboardViewModel.cs]
       │ (Sends packet via Teacher Network Client)
       ▼
[TCP Network Broadcast/Multicast/Direct]
       │ (Packet: CleanAndInitDrivePacket)
       ▼
[StudentCoordinator.cs]
       │ (Route packet by CommandType.CleanAndInitDrive)
       ▼
[CleanAndInitDriveHandler.cs]
       │
       ├─► 1. Safety Guardrail Check (Block C:\, Validate DriveLetter)
       ├─► 2. FileSystem Cleanup (Delete D:\* excluding system items)
       └─► 3. Folder Creation (Create validated folders)
```

## 2. Chi tiết mô hình dữ liệu (Data Contracts)

### `CommandType.cs` (`LabManager.Protocol`)
Thêm giá trị:
```csharp
public enum CommandType : byte
{
    // ...
    CleanAndInitDrive = 27,
    // ...
}
```

### `CleanAndInitDrivePacket.cs` (`LabManager.Protocol.Packets`)
```csharp
namespace LabManager.Protocol.Packets;

public class CleanAndInitDrivePacket
{
    public string DriveLetter { get; set; } = @"D:\";
    public List<string> FolderNames { get; set; } = new();
}
```

## 3. Thành phần xử lý tại máy học sinh (`LabManager.Student.Core`)

### `CleanAndInitDriveHandler.cs`
- **Dependency:** Cần tham chiếu đến `LabManager.Protocol.Packets` và `LabManager.Core.Logging` (nếu có).
- **Quy trình thực thi:**
  1. `string targetDrive = string.IsNullOrWhiteSpace(packet.DriveLetter) ? @"D:\" : packet.DriveLetter;`
  2. Chuẩn hóa đường dẫn: `Path.GetPathRoot(targetDrive)`.
  3. Kiểm tra an toàn:
     - So sánh với `Path.GetPathRoot(Environment.SystemDirectory)` (thường là `C:\`).
     - Nếu trùng với ổ OS: Bỏ qua và trả về `false`.
     - Kiểm tra sự tồn tại của ổ: `Directory.Exists(targetDrive)`. Nếu không tồn tại: Bỏ qua.
  4. Quét và dọn dẹp các thư mục con:
     - Danh sách bỏ qua: `new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "$RECYCLE.BIN", "System Volume Information" }`.
     - Lấy danh sách directory: `Directory.GetDirectories(targetDrive)`.
       - Với mỗi thư mục: nếu tên nằm trong danh sách bỏ qua -> continue.
       - Ngược lại: `try { Directory.Delete(dir, true); } catch { ... }`
     - Lấy danh sách file: `Directory.GetFiles(targetDrive)`.
       - Với mỗi file: `try { File.Delete(file); } catch { ... }`
  5. Tạo thư mục mới:
     - Lấy ký tự không hợp lệ: `char[] invalidChars = Path.GetInvalidFileNameChars();`
     - Với mỗi tên trong `packet.FolderNames`:
       - Loại bỏ ký tự cấm, trim whitespace.
       - Nếu không rỗng: `Directory.CreateDirectory(Path.Combine(targetDrive, cleanName))`.

## 4. Giao diện Giáo viên (`LabManager.Teacher.App`)

- **View:** `CleanAndInitDriveWindow.xaml` (Modal dialog)
  - Layout gọn gàng, hiện đại theo chuẩn WPF của dự án.
  - Vùng dán danh sách tên (mỗi dòng 1 lớp/học sinh).
  - Checkbox cảnh báo an toàn màu đỏ/cam.
  - Nút "Thực hiện" và "Hủy bỏ".
- **ViewModel / Command:**
  - Tích hợp vào `DashboardViewModel.cs`.
  - Hỗ trợ gửi lệnh tới danh sách máy đang được chọn trong `SelectedStudents` hoặc toàn bộ phòng máy nếu không chọn máy nào.
