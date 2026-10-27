# Hướng dẫn AI: LoipvRemote

Áp dụng [quy tắc workspace](../AGENTS.md) và đọc [README](README.md) trước khi sửa. File này chỉ giữ quy tắc riêng của repo. Khi clone độc lập ngoài workspace, cần được cung cấp quy tắc chung; không suy ra quyền push, publish hoặc deploy từ tài liệu.

## Cấu hình theo môi trường

- Theo quyết định của chủ workspace, cấu hình runtime được điền trực tiếp vào appsettings/Compose/source theo môi trường, kể cả credential đã được cho phép phục hồi. Không tự thay giá trị bằng placeholder hoặc bắt buộc nạp GitHub Secrets/registry để chạy Dev/Test.
- Source là nguồn cấu hình hiệu lực; khi chuyển giá trị vào source phải gỡ env override legacy tương ứng. Đối chiếu registry khi cần, không đoán giá trị hoặc dùng hạ tầng Production cho Dev/Test. Credential có thể được lưu trong Git history/image khi commit và phát hành; không in chúng ra chat/log.
- Appsettings sở hữu cấu hình ứng dụng; Compose chỉ giữ cấu hình triển khai và override theo host cần thiết, dùng YAML anchor cho giá trị lặp lại. Không lưu cấu hình trong file env/GitHub Secrets sau khi bản thay thế đã triển khai và kiểm chứng; CI dùng `github.token` tự cấp cho quyền package/checkout. Chỉ push, publish hoặc deploy trong phạm vi người dùng đã cho phép.

## Nhánh phát triển và Production

- `main` là nhánh gốc duy nhất để phát triển; tạo mọi nhánh feature/fix từ `main` mới nhất, không phát triển trực tiếp trên nhánh phát hành.
- Mọi thay đổi, cấu hình và artifact dự kiến lên Production phải được merge và push lên `main` trước. Sau đó chỉ đồng bộ commit đã có trên `main` sang nhánh `production`, `release` hoặc nhánh workflow mà repo quy định; không deploy nội dung chỉ tồn tại trên nhánh phát hành.
- Nếu nhánh phát hành có commit riêng chưa có trên `main`, phải bảo toàn và hợp nhất về `main` trước khi phát hành; không reset hoặc ghi đè lịch sử tùy tiện.

## Quy trình release

Chỉ phát hành khi người dùng yêu cầu rõ; push `main` không phải yêu cầu release.

- Mọi commit cần release phải được commit và push thành công lên nhánh `main` trước.
- Chỉ sau khi `main` đã chứa commit cần release mới thực hiện quy trình release và push theo workflow đã commit.
- Không phát hành trực tiếp từ thay đổi chưa có trên `main`.

## Solution

- Chỉ cập nhật `.slnx` khi thay đổi project hoặc solution item cần dùng ngoài project; không sao chép cây source/test hay thêm lại file project đã quản lý. Giữ nguyên project, build/platform mapping và source; bỏ folder rỗng, artifact benchmark/test, log, cache/generated/vendor. File gốc cần hiển thị đặt trong `/items/`; đối chiếu project trước/sau và build bằng toolchain hiện hữu.
