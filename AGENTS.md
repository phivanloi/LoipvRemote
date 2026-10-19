# Hướng dẫn AI: LoipvRemote

Áp dụng [quy tắc workspace](../AGENTS.md) và đọc [README](README.md) trước khi sửa. File này chỉ giữ quy tắc riêng của repo. Khi clone độc lập ngoài workspace, cần được cung cấp quy tắc chung; không suy ra quyền push, publish hoặc deploy từ tài liệu.

## Nhánh phát triển và Production

- `main` là nhánh gốc duy nhất để phát triển; tạo mọi nhánh feature/fix từ `main` mới nhất, không phát triển trực tiếp trên nhánh phát hành.
- Mọi thay đổi, cấu hình và artifact dự kiến lên Production phải được merge và push lên `main` trước. Sau đó chỉ đồng bộ commit đã có trên `main` sang nhánh `production`, `release` hoặc nhánh workflow mà repo quy định; không deploy nội dung chỉ tồn tại trên nhánh phát hành.
- Nếu nhánh phát hành có commit riêng chưa có trên `main`, phải bảo toàn và hợp nhất về `main` trước khi phát hành; không reset hoặc ghi đè lịch sử tùy tiện.

## Quy trình release

Chỉ phát hành khi người dùng yêu cầu rõ; push `main` không phải yêu cầu release.

- Mọi commit cần release phải được commit và push thành công lên nhánh `main` trước.
- Chỉ sau khi `main` đã chứa commit cần release mới thực hiện quy trình release và push theo workflow đã commit.
- Không phát hành trực tiếp từ thay đổi chưa có trên `main`.
