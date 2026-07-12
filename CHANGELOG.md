# Changelog

## LoipvRemote 2.0.29.0 (Internal)

- Language dùng ComboBox chuẩn để tránh font bị vẽ lại khi hover; PropertyGrid đồng bộ editor native trong lúc focus.

## LoipvRemote 2.0.28.0 (Internal)

- Đồng bộ font mặc định của nhãn tùy biến với textbox và dropdown, giữ cùng cỡ chữ cả khi hover/focus.

## LoipvRemote 2.0.27.0 (Internal)

- Đồng bộ cỡ chữ và chiều cao item của dropdown; sửa textbox IP/Port Scan co giãn theo font và xóa dòng preview kích thước giao diện dư thừa.

## LoipvRemote 2.0.26.0 (Internal)

- Khóa font của textbox, dropdown và editor native sau mọi thay đổi do hover/focus; căn giữa lại dấu tích và nhãn checkbox.

## LoipvRemote 2.0.25.0 (Internal)

- PropertyGrid giữ nguyên cỡ chữ đã cấu hình cho input và dropdown cả sau khi editor native nhận focus.

## LoipvRemote 2.0.24.0 (Internal)

- Giữ đủ bốn menu chính, gồm `Công cụ`, khi top menu dùng chiều cao rút gọn và ngôn ngữ tiếng Việt.

## LoipvRemote 2.0.23.0 (Internal)

- Giảm 20% chiều cao top menu, giữ nguyên font, icon và chiều rộng nút điều khiển cửa sổ.

## LoipvRemote 2.0.22.0 (Internal)

- Luôn lưu trạng thái mở/đóng folder của Connections khi người dùng thay đổi, không còn phụ thuộc thiết lập backup/save connection.

## LoipvRemote 2.0.21.0 (Internal)

- Thêm nút `Copy` có icon trong popup `Show Password`; password chỉ được sao chép vào clipboard khi người dùng bấm nút này.

## LoipvRemote 2.0.20.0 (Internal)

- Thêm menu `Show Password` sau `Tools` trong menu chuột phải của connection; chỉ khả dụng khi connection có password và hiển thị trong hộp thoại chỉ-đọc.

## LoipvRemote 2.0.19.0 (Internal)

- Làm đậm riêng phần giá trị của CPU, RAM, Disk, In, Out và Uptime trên thanh monitor SSH.

## LoipvRemote 2.0.18.0 (Internal)

- Sửa cách cộng lưu lượng mạng Linux: `In` và `Out` lấy tổng RX/TX của mọi interface, trừ loopback; TX không còn bị đọc thành `0` do sai dấu phân cách `awk`.

## LoipvRemote 2.0.17.0 (Internal)

- Alert chỉ hiển thị Warning và Error; Information chỉ được ghi log.

## LoipvRemote 2.0.16.0 (Internal)

- Gom các yêu cầu autosave phát sinh từ cùng một thay đổi cấu hình; chỉ ghi một lần và chỉ hiển thị một thông báo thành công.
- Thông báo tiến trình lưu chỉ ghi log, không hiện alert gây nhiễu.

## LoipvRemote 2.0.15.0 (Internal)

- Sửa PropertyGrid: ô nhập và dropdown giữ nguyên font cấu hình khi focus hoặc bắt đầu chỉnh sửa.

## LoipvRemote 2.0.14.0 (Internal)

- Đồng bộ toàn bộ icon của connection thành biểu tượng LoipvRemote, gồm cả các icon legacy và custom đã lưu.

## LoipvRemote 2.0.13.0 (Internal)

- Tên connection đang mở đổi sang màu xanh; tự khôi phục màu mặc định sau khi ngắt kết nối.

## LoipvRemote 2.0.12.0 (Internal)

- Kênh giám sát SSH ưu tiên thuật toán host key đã được PuTTYNG tin cậy, hỗ trợ cache RSA (`rsa2`) cho các máy như Mega.

## LoipvRemote 2.0.11.0 (Internal)

- Bỏ tab Notifications khỏi sidebar và không tải panel cũ từ layout đã lưu.
- Thông tin, cảnh báo và lỗi hiển thị qua alert; debug tiếp tục chỉ ghi log.

## LoipvRemote 2.0.10.0 (Internal)

- Disk hiển thị dung lượng đã dùng/tổng và phần trăm; RX/TX đổi thành In/Out.

## LoipvRemote 2.0.9.0 (Internal)

- Các ô số liệu SSH tự giãn theo chiều ngang khi cửa sổ rộng; khi hẹp vẫn giữ toàn bộ chỉ số qua thanh cuộn ngang.

## LoipvRemote 2.0.8.0 (Internal)

- Hiển thị cố định CPU, RAM, Disk, RX, TX và Uptime; không còn ẩn Uptime theo độ rộng tab.
- Mọi chỉ số có nhãn và đơn vị hiển thị rõ ràng.

## LoipvRemote 2.0.7.0 (Internal)

- Sửa lệnh lấy lưu lượng mạng Linux trên SSH: quoting awk trước đó làm mẫu tài nguyên bị hủy toàn bộ.
- Bỏ qua các dòng banner an toàn, chỉ phân tích các khóa số liệu xác định.

## LoipvRemote 2.0.6.0 (Internal)

- Bỏ nút xác minh trên thanh tài nguyên SSH. Thanh tự đối chiếu host key hiện tại với cache tin cậy của PuTTYNG trước khi giám sát.
- Đồng bộ chuỗi thanh tài nguyên với ngôn ngữ ứng dụng (English/Vietnamese).

## LoipvRemote 2.0.5.0 (Internal)

- Chỉnh thanh tài nguyên SSH tương thích vùng tab hẹp: ưu tiên hiển thị nút tin cậy, không còn che chữ.
- Việt hóa các trạng thái giám sát và nhãn thời gian hoạt động; sửa nhãn uptime hiển thị sai biểu tượng tải lên.

## LoipvRemote 2.0.4.0 (Internal)

- Thêm thanh tài nguyên cho tab SSH2: CPU, RAM, ổ `/`, tốc độ nhận/gửi và uptime Linux.
- Thanh chỉ lấy mẫu khi tab SSH đang active; host key phải được người dùng tin cậy cho phiên hiện tại.
- Sửa phục hồi focus/input của terminal SSH sau khi chuyển sang ứng dụng khác rồi quay lại bằng chuột hoặc Alt+Tab.
- Đồng bộ nhận diện LoipvRemote trong bộ cài: banner, ảnh cạnh và icon title bar.
- Bản phân phối nội bộ, chưa được code-sign.

## LoipvRemote 1.78.2.3606

- Hoàn thiện nhận diện thương hiệu LoipvRemote, biểu tượng ứng dụng và giao diện tiếng Việt.
- Đồng bộ phông chữ, kích thước biểu tượng, thanh menu, sidebar và các tab làm việc.
- Cập nhật PuTTYNG 0.84 x64 dùng cho kết nối SSH và cải thiện cơ chế nhúng cửa sổ SSH.
- Cải thiện bố cục tab kết nối, cấu hình và thông báo; loại bỏ thanh tìm kiếm trong sidebar.

## Nguồn gốc

LoipvRemote được phát triển từ mRemoteNG. Xem `COPYING.TXT` để biết điều khoản cấp phép và `README.md` để biết thêm thông tin.
