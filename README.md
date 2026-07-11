# LoipvRemote

LoipvRemote là ứng dụng Windows quản lý kết nối từ xa đa giao thức, bao gồm RDP, VNC, SSH, Telnet, HTTP/HTTPS, rlogin, Raw Socket, PowerShell remoting và AnyDesk.

## Nguồn gốc và mục đích sử dụng

Mã nguồn này được sao chép từ dự án [mRemoteNG](https://github.com/mRemoteNG/mRemoteNG) và đã được Phí Văn Lời chỉnh sửa để phù hợp với nhu cầu sử dụng riêng.

LoipvRemote không được phát triển hoặc phân phối với mục đích lợi nhuận. Dự án vẫn tuân theo giấy phép GPL được cung cấp trong [COPYING.txt](COPYING.txt). Các thông báo bản quyền và điều khoản giấy phép của các thành phần bên thứ ba phải được giữ nguyên.

## Build

Yêu cầu Windows, .NET Desktop Runtime 10 và Visual Studio/MSBuild có thành phần Text Templating.

```powershell
& 'C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe' LoipvRemote.sln -p:Configuration=Release -p:Platform=x64
```

Output chính là `LoipvRemote\bin\x64\Release\LoipvRemote.exe`.
