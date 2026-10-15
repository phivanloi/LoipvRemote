# Hướng dẫn AI: LoipvRemote

Áp dụng [quy tắc workspace](../AGENTS.md) và đọc [README](README.md) trước khi sửa. File này chỉ giữ quy tắc riêng của repo. Khi clone độc lập ngoài workspace, cần được cung cấp quy tắc chung; không suy ra quyền push, publish hoặc deploy từ tài liệu.

## Quy trình release

Chỉ phát hành khi người dùng yêu cầu rõ; push `main` không phải yêu cầu release.

- Mọi commit cần release phải được commit và push thành công lên nhánh `main` trước.
- Chỉ sau khi `main` đã chứa commit cần release mới thực hiện quy trình release và push theo workflow đã commit.
- Không phát hành trực tiếp từ thay đổi chưa có trên `main`.
