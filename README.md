# Hệ thống Ngân hàng

Website ngân hàng: **backend C# ASP.NET Core Web API** (.NET 8), giao diện HTML/CSS/JavaScript.
Hiện đã có trang **Trang chủ**, **Tài khoản của tôi** và **Giao dịch** (bản cơ bản). Trang Chuyển tiền và Cộng lãi đang làm.

## Cách chạy

Cần cài [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). Kiểm tra bằng lệnh `dotnet --version`.

```bash
cd NganHang
dotnet run
```

Mở trình duyệt vào **http://localhost:5000**. Tắt server bằng `Ctrl + C`.

Lần chạy đầu, server tự tạo dữ liệu mẫu ở `NganHang/App_Data/bank-data.json`. Muốn về dữ liệu ban đầu: bấm avatar → Hồ sơ cá nhân → Khôi phục dữ liệu mẫu, hoặc xóa file đó rồi chạy lại.

## Cấu trúc

```
NganHang/
├── Program.cs                     cấu hình server, phục vụ wwwroot + API
├── Models/
│   ├── Entities.cs                Account, Transaction, Notification, UserProfile
│   └── Dtos.cs                    dữ liệu gửi/nhận qua API
├── Data/
│   ├── BankRepository.cs          đọc/ghi file JSON (có lock)
│   └── SeedData.cs                dữ liệu mẫu
├── Services/
│   └── BankService.cs             TOÀN BỘ NGHIỆP VỤ
├── Controllers/                   các API /api/...
├── NganHang.http                  thử nhanh các API
└── wwwroot/                       giao diện (HTML, CSS, JS, ảnh)
    └── js/store.js                gọi API từ trình duyệt
```

## API

| Phương thức | Đường dẫn | Mô tả |
|---|---|---|
| GET | `/api/state` | Người dùng, tài khoản, giao dịch, thông báo |
| GET | `/api/accounts` | Danh sách tài khoản |
| GET | `/api/accounts/{id}` | Một tài khoản |
| POST | `/api/accounts` | Mở tài khoản mới |
| POST | `/api/accounts/{id}/deposit` | Nạp tiền `{ "amount": 1000000 }` |
| POST | `/api/accounts/{id}/withdraw` | Rút tiền `{ "amount": 5000000 }` |
| GET | `/api/transactions?accountId=&limit=` | Lịch sử giao dịch (có số dư sau GD) |
| GET | `/api/balance-history?accountIds=&from=&to=&mode=` | Dữ liệu biểu đồ (`monthly`/`weekly`/`daily`) |
| POST | `/api/notifications/{id}/read` | Đánh dấu đã đọc |
| PUT | `/api/user/otp` | Bật/tắt OTP |
| POST | `/api/system/reset` | Khôi phục dữ liệu mẫu |

Lỗi nghiệp vụ trả về mã `400` kèm `{ "error": "thông báo tiếng Việt" }`; không tìm thấy trả `404`.

## Quy tắc nghiệp vụ (trong `BankService.cs`)

- **Số dư sau giao dịch:** tính ngược từ số dư hiện tại, riêng cho từng tài khoản.
- **Tiền lãi dự kiến:** số dư × lãi suất / 100 × số ngày kỳ hạn / 365. Sổ tự tái tục khi đến hạn.
- **Rút tiền tiết kiệm:** không vượt số dư.
- **Rút tiền thanh toán:** được dùng thấu chi, khả dụng = số dư + hạn mức thấu chi.
- **Mở tài khoản tiết kiệm:** tối thiểu 1.000.000 đ; lãi suất theo kỳ hạn 1/3/6/12 tháng = 4,5 / 5,2 / 5,8 / 6,3 %/năm.
- **Mở tài khoản thanh toán:** hạn mức thấu chi 0 / 5.000.000 / 10.000.000 đ.
- **Mỗi lần nạp/rút:** tối đa 1.000.000.000 đ.
- **Kiểm tra hai lớp:** server luôn kiểm tra lại, kể cả khi giao diện đã kiểm tra.

## Nhật ký chỉnh sửa

| Ngày | Nội dung | File |
|---|---|---|
| | Trang chủ | `wwwroot/index.html`, `wwwroot/js/pages/home.js`, `wwwroot/css/pages/home.css` |
| | Tài khoản của tôi | `wwwroot/tai-khoan.html`, `wwwroot/js/pages/accounts.js`, `wwwroot/css/pages/accounts.css` |
| | Chuyển sang backend C# | `Services/BankService.cs`, `Controllers/`, `wwwroot/js/store.js` |
