# Hệ thống Ngân hàng

Website ngân hàng: **backend C# ASP.NET Core Web API** (.NET 8), giao diện HTML/CSS/JavaScript.
Đã có các trang **Trang chủ**, **Tài khoản của tôi**, **Chuyển tiền**, **Cộng lãi** và **Giao dịch** (bản cơ bản).

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
| POST | `/api/transfers` | Chuyển tiền (nội bộ: `toAccountId`; ra ngoài: `toAccountNumber`, `toName`, `toBank`) |
| POST | `/api/interest/run` | Chạy ngay đợt cộng lãi tự động (chỉ cộng kỳ đã đến hạn) |
| POST | `/api/interest/{accountId}/apply` | Nhận ngay tiền lãi tạm tính của tài khoản tiết kiệm |
| GET | `/api/transactions?accountId=&limit=` | Lịch sử giao dịch (có số dư sau GD) |
| GET | `/api/balance-history?accountIds=&from=&to=&mode=` | Dữ liệu biểu đồ (`monthly`/`weekly`/`daily`) |
| POST | `/api/notifications/{id}/read` | Đánh dấu đã đọc |
| PUT | `/api/user/otp` | Bật/tắt OTP |
| POST | `/api/system/reset` | Khôi phục dữ liệu mẫu |

Lỗi nghiệp vụ trả về mã `400` kèm `{ "error": "thông báo tiếng Việt" }`; không tìm thấy trả `404`.

## Chạy unit test

```bash
cd NganHang.Tests
dotnet test
```

Các test nằm trong thư mục `NganHang.Tests/` (xUnit). Mỗi test dùng một file dữ liệu tạm riêng nên không ảnh hưởng dữ liệu thật.

| File | Kiểm thử |
|---|---|
| `WithdrawTests.cs` | Rút tiền: số dư âm, hạn mức thấu chi, số tiền không hợp lệ |
| `TransferTests.cs` | Chuyển tiền: nội bộ, ra ngoài, chuyển vượt hạn mức, số dư âm, lịch sử giao dịch, lưu file |
| `InterestTests.cs` | Cộng lãi: đúng công thức, theo lãi suất kỳ hạn, tự động theo tháng, không cộng trùng, lãi kép nhiều kỳ, số dư 0, tài khoản thanh toán không có lãi |

## Quy tắc nghiệp vụ (trong `BankService.cs`)

- **Số dư sau giao dịch:** tính ngược từ số dư hiện tại, riêng cho từng tài khoản.
- **Tiền lãi dự kiến:** số dư × lãi suất / 100 × số ngày kỳ hạn / 365. Sổ tự tái tục khi đến hạn.
- **Rút tiền tiết kiệm:** không vượt số dư.
- **Rút tiền thanh toán:** được dùng thấu chi, khả dụng = số dư + hạn mức thấu chi.
- **Mở tài khoản tiết kiệm:** tối thiểu 1.000.000 đ; lãi suất theo kỳ hạn 1/3/6/12 tháng = 4,5 / 5,2 / 5,8 / 6,3 %/năm.
- **Mở tài khoản thanh toán:** hạn mức thấu chi 0 / 5.000.000 / 10.000.000 đ.
- **Mỗi lần nạp/rút/chuyển:** tối đa 1.000.000.000 đ.
- **Cộng lãi tiết kiệm:** lãi mỗi ngày = số dư cuối ngày × lãi suất / 100 / 365; đủ 1 tháng kể từ lần cộng trước thì **tự động cộng** (dịch vụ chạy nền `InterestBackgroundService` kiểm tra khi khởi động và mỗi giờ); mỗi kỳ chỉ cộng một lần; số dư âm/0 không có lãi; tài khoản thanh toán không có lãi.
- **Chuyển tiền:** tối thiểu 10.000 đ; dùng chung quy tắc số dư/thấu chi với rút tiền; không chuyển vào chính tài khoản nguồn; nhập STK trùng tài khoản của mình thì tự xử lý như chuyển nội bộ; chuyển nội bộ tạo 2 giao dịch (chuyển đi + nhận tiền).
- **Kiểm tra hai lớp:** server luôn kiểm tra lại, kể cả khi giao diện đã kiểm tra.

## Nhật ký chỉnh sửa

| Ngày | Nội dung | File |
|---|---|---|
| | Trang chủ | `wwwroot/index.html`, `wwwroot/js/pages/home.js`, `wwwroot/css/pages/home.css` |
| | Tài khoản của tôi | `wwwroot/tai-khoan.html`, `wwwroot/js/pages/accounts.js`, `wwwroot/css/pages/accounts.css` |
| | Chuyển sang backend C# | `Services/BankService.cs`, `Controllers/`, `wwwroot/js/store.js` |
| Tuần 2 | Chuyển tiền (kiểm tra số dư, hạn mức thấu chi) | `Services/BankService.cs`, `Controllers/TransfersController.cs`, `Models/Dtos.cs` |
| Tuần 2 | Trang Chuyển tiền, đồng bộ Trang chủ | `wwwroot/chuyen-tien.html`, `wwwroot/js/pages/transfer.js`, `wwwroot/css/pages/transfer.css`, `wwwroot/js/store.js` |
| Tuần 2 | Unit test chuyển/rút tiền | `NganHang.Tests/TransferTests.cs`, `NganHang.Tests/WithdrawTests.cs` |
| Tuần 2 | Cộng lãi tự động | `Services/BankService.cs`, `Services/InterestBackgroundService.cs`, `Controllers/InterestController.cs`, `Models/Entities.cs` |
| Tuần 2 | Trang Cộng lãi, chi tiết lãi trong Tài khoản của tôi | `wwwroot/cong-lai.html`, `wwwroot/js/pages/interest.js`, `wwwroot/css/pages/interest.css`, `wwwroot/js/pages/accounts.js` |
| Tuần 2 | Unit test cộng lãi | `NganHang.Tests/InterestTests.cs` |
