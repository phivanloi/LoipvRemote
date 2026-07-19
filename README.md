# Loipv Remote

**Loipv Remote** là ứng dụng Windows để quản lý và mở nhiều phiên SSH/RDP trong cùng một cửa sổ. Ứng dụng phù hợp khi cần sắp xếp danh sách máy chủ theo thư mục, mở nhanh từng máy trong tab riêng và không phải ghi nhớ lại thông tin kết nối mỗi lần sử dụng.

## Tính năng chính

- Quản lý kết nối theo cây thư mục; có thể tạo, sửa, nhân bản, di chuyển và xóa thư mục hoặc connection.
- Hỗ trợ **RDP** và **SSH** trong từng tab riêng, giúp chuyển đổi giữa nhiều máy chủ mà không mất phiên đang mở.
- **Quick connect** để kết nối nhanh mà không cần lưu cấu hình trước.
- Menu chuột phải cho connection và thư mục để thao tác gọn hơn.
- RDP tự điều chỉnh độ phân giải theo vùng hiển thị và DPI của cửa sổ; SmartSizing được dùng làm phương án dự phòng khi máy chủ không hỗ trợ dynamic resolution.
- SSH nhúng trong ứng dụng, có xử lý focus/bàn phím khi đổi tab hoặc thay đổi kích thước cửa sổ.
- Thanh theo dõi SSH hiển thị CPU, RAM, dung lượng ổ đĩa, lưu lượng vào/ra và uptime khi phiên từ xa cung cấp được số liệu.
- Import/Export cây connection bằng XML để chuyển cấu hình sang máy khác.

## Yêu cầu hệ thống

- Windows 10 2004 trở lên hoặc Windows 11.
- Chọn đúng gói theo kiến trúc máy:
  - **x64**: đa số máy Windows dùng CPU Intel hoặc AMD.
  - **ARM64**: máy Windows on ARM.

## Cài đặt

1. Mở trang [GitHub Releases](../../releases).
2. Tải một trong hai file cài đặt:
   - `LoipvRemote-Installer-x64.msi` cho máy Intel/AMD.
   - `LoipvRemote-Installer-arm64.msi` cho máy Windows on ARM.
3. Chạy file `.msi` vừa tải và làm theo trình cài đặt.
4. Mở **Loipv Remote** từ Start Menu.

Mỗi release cũng có gói chạy trực tiếp:

- `LoipvRemote-Portable-x64.zip`
- `LoipvRemote-Portable-arm64.zip`

Giải nén đúng gói theo kiến trúc máy, sau đó chạy `LoipvRemote.exe`. Bản portable không cần cài đặt.

## Hướng dẫn sử dụng

### Tạo và lưu connection

1. Chọn **New connection** trên title bar.
2. Chọn giao thức **RDP** hoặc **SSH**.
3. Nhập tên hiển thị, địa chỉ máy chủ, cổng, tài khoản và mật khẩu hoặc cấu hình xác thực cần thiết.
4. Chọn thư mục đích rồi lưu.
5. Nhấn vào connection trong sidebar để mở tab và kết nối.

Nhấn chuột trái vào tên thư mục để mở hoặc thu gọn. Nhấn chuột phải vào connection/thư mục để mở menu thao tác như sửa, nhân bản, di chuyển hoặc xóa.

### Kết nối nhanh

Chọn **Quick connect** trong menu dấu ba chấm khi chỉ cần kết nối một lần. Connection này không cần phải được lưu vào cây thư mục.

### Làm việc với tab

- Mỗi connection mở trong một tab có icon giao thức và tên connection.
- Chọn tab để chuyển phiên đang làm việc.
- Nhấn nút `×` trên tab để đóng riêng phiên đó.
- Nội dung Welcome tự ẩn khi đang hiển thị một phiên connection.

### RDP

Loipv Remote yêu cầu vùng desktop RDP phù hợp với tab hiện tại. Khi kéo thay đổi kích thước hoặc maximize/restore cửa sổ, ứng dụng sẽ cập nhật kích thước desktop từ xa sau khi giao diện ổn định để tránh nhấp nháy. Kích thước được giới hạn trong khoảng thực tế từ **1024 × 768** đến **3840 × 2160**.

Nếu máy chủ RDP không hỗ trợ thay đổi độ phân giải động, phiên vẫn được hiển thị bằng SmartSizing để không làm mất vùng desktop đang làm việc.

Khi tab RDP được chọn, thanh cuối cửa sổ sẽ thử lấy CPU, RAM, ổ đĩa, băng thông và uptime qua kênh CIM/WMI riêng bằng thông tin xác thực của connection. Ứng dụng ưu tiên WinRM và tự fallback sang WMI/DCOM (hữu ích với connection dùng địa chỉ IP nhưng máy client chưa cấu hình `TrustedHosts`). Performance counter CPU và network dùng thêm classic WMI fallback vì một số máy Windows phản hồi các class này chậm hoặc không ổn định qua CIM. Ứng dụng không tự bật dịch vụ quản trị hoặc thay đổi firewall trên máy đích. Nếu cả WinRM và WMI/DCOM đều bị tắt, bị chặn, thiếu quyền hoặc máy chỉ truy cập được qua RDP Gateway, các chỉ số sẽ hiển thị `--` và phiên RDP vẫn hoạt động bình thường.

Clipboard và local-drive redirection được bật mặc định cho connection RDP cũ hoặc imported không có tùy chọn redirection, hỗ trợ copy/paste nội dung và file hai chiều. Giá trị cấu hình tường minh `RedirectClipboard=false` hoặc `RedirectDiskDrives=None` vẫn được tôn trọng.

### SSH và thanh theo dõi tài nguyên

Với tab SSH đang được chọn, thanh cuối cửa sổ có thể hiển thị CPU, RAM, ổ đĩa, băng thông và uptime. Các chỉ số không khả dụng sẽ hiển thị `--`; điều này phụ thuộc vào quyền và môi trường của máy chủ từ xa.

## Import và Export connection

### Import XML

1. Mở menu dấu ba chấm trên title bar.
2. Chọn **Import XML**.
3. Chọn file XML đã export.

Danh sách import được đưa vào một thư mục riêng để tránh ghi đè cấu trúc connection hiện có. Thông tin xác thực trong file portable được bảo vệ lại theo tài khoản Windows đang dùng sau khi import.

### Export XML

1. Mở menu dấu ba chấm trên title bar.
2. Chọn **Export XML**.
3. Xác nhận cảnh báo rồi chọn nơi lưu file.

> **Cảnh báo bảo mật:** File XML export portable có đầy đủ cấu hình, tên đăng nhập và mật khẩu ở dạng có thể đọc được để máy khác import xong dùng ngay. Xem file này như một secret: chỉ lưu ở nơi an toàn, chỉ chia sẻ qua kênh được phê duyệt và xóa khi không còn cần thiết.

Dữ liệu connection lưu cục bộ trong ứng dụng được bảo vệ theo tài khoản Windows hiện tại. Cơ chế này không áp dụng cho XML portable đã export.

## Build từ mã nguồn

Cần cài .NET SDK theo phiên bản ghi trong [global.json](global.json). WiX sẽ được khôi phục tự động qua project installer.

```powershell
dotnet restore LoipvRemote.slnx
dotnet restore LoipvRemoteInstaller/Installer/Installer.wixproj

dotnet test LoipvRemote.WinUI.Tests/LoipvRemote.WinUI.Tests.csproj `
  -c Release -p:Platform=x64

dotnet build LoipvRemoteInstaller/Installer/Installer.wixproj `
  -c Release -p:Platform=x64
```

MSI x64 được tạo tại `LoipvRemoteInstaller/Installer/bin/x64/Release/`. Thay `x64` bằng `arm64` khi build trên môi trường phù hợp cho Windows on ARM.

## Báo lỗi và đóng góp

Vui lòng tạo issue hoặc pull request nếu gặp lỗi. Không đính kèm hostname thật, IP nội bộ, tài khoản, mật khẩu, file XML export hoặc ảnh chụp có thông tin connection trong issue, log hay tài liệu công khai.

## Giấy phép

Dự án được phát hành theo [GNU GPL v2](COPYING.txt).
