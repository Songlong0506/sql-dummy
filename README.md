# SqlDummySeeder (server-first DB picker)

- Bước kết nối tối ưu: nhập **Server Name** + chọn **Auth** → bấm **Load** để lấy danh sách **Database** và chọn từ dropdown.
- Vẫn lưu cấu hình theo **Server Name** (Auth/User/Pass), và tự fill lại khi bạn chọn từ danh sách **Đã lưu**.
- Giữ toàn bộ tính năng seed (FK-aware, Rules JSON, Bogus, fixes nvarchar(max)/varchar(max), Truncate an toàn).

## Run
Yêu cầu [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).

```bash
dotnet restore
dotnet run
```

## Ghi chú
- Endpoint lấy DB: `POST /Home/ListDatabases` (JSON body: serverName, authMode, userName, password). Kết quả `{ ok, data, error }`.
- Mặc định ẩn DB hệ thống (`master`, `tempdb`, `model`, `msdb`). Có thể đổi trong code `GetDatabasesAsync(..., includeSystem: false)`.
