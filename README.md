# Hệ thống Ngân hàng

Website ngân hàng: **backend C# ASP.NET Core Web API** (.NET 8), giao diện HTML/CSS/JavaScript.
Đã có các trang **Trang chủ**, **Tài khoản của tôi**, **Giao dịch**, **Chuyển tiền**, **Cộng lãi** và **Thống kê**.
Mọi thao tác ghi đều **tất cả hoặc không gì cả** (rollback khi lỗi giữa chừng).

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
│   ├── Accounts.cs                Account (lớp cha) → SavingAccount, PaymentAccount (kế thừa, đa hình)
│   ├── Entities.cs                Transaction, Notification, UserProfile, BankData
│   └── Dtos.cs                    dữ liệu gửi/nhận qua API
├── Data/
│   ├── BankRepository.cs          đọc/ghi file JSON (có lock, chụp + khôi phục khi lỗi = rollback)
│   ├── FaultInjection.cs          điểm cắm lỗi giả lập cho test rollback
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
| POST | `/api/accounts/{id}/lock` | Khóa / mở khóa tài khoản `{ "locked": true }` (tuần 4: có nút trong Chi tiết tài khoản) |
| GET | `/api/statistics?from=&to=&groupBy=&accountId=` | Thống kê theo kỳ (`month`/`quarter`/`day`): đầu kỳ, vào, ra, lãi, cuối kỳ |
| GET | `/api/transactions?accountId=&limit=` | Lịch sử giao dịch (có số dư sau GD) |
| GET | `/api/balance-history?accountIds=&from=&to=&mode=` | Dữ liệu biểu đồ (`monthly`/`weekly`/`daily`) |
| POST | `/api/notifications/{id}/read` | Đánh dấu đã đọc |
| PUT | `/api/user/otp` | Bật/tắt OTP |
| POST | `/api/system/reset` | Khôi phục dữ liệu mẫu |

Lỗi nghiệp vụ trả về mã `400` kèm `{ "error": "thông báo tiếng Việt" }`; không tìm thấy trả `404`;
lỗi hệ thống giữa chừng (đã hoàn tác, có ghi giao dịch "Thất bại") trả `409`.

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
| `RollbackTests.cs` | Tuần 3 – rollback: lỗi sau khi trừ nguồn/chưa cộng đích, lỗi ghi file, lỗi giữa đợt cộng lãi, giao dịch "failed", chạy song song có lỗi |
| `EdgeCaseTests.cs` | Tuần 3 – biên: số tiền nhỏ nhất/lớn nhất, nhiều lệnh cùng lúc, tài khoản bị khóa, STK có ký tự lạ, file dữ liệu hỏng |
| `StatisticsTests.cs` | Tuần 3 – thống kê: cuối kỳ = đầu kỳ + vào − ra, lãi từng kỳ khớp lịch sử, chia kỳ tháng/quý/ngày |
| `AccountModelTests.cs` | Tuần 3 – OOP: kế thừa, đa hình, đóng gói số dư, đọc/ghi JSON đúng lớp con |
| `LockTests.cs` | Tuần 4 – khóa/mở khóa: thời điểm khóa, khả dụng = 0, khóa 2 lần, lưu file, rollback khi lỗi |
| `LockedInterestTests.cs` | Tuần 4 – sổ bị khóa vẫn được cộng lãi đúng kỳ, chặn "Nhận lãi ngay", không cộng trùng khi mở khóa |
| `InterestEdgeTests.cs` | Tuần 3 – biên cộng lãi: tháng 28/29/30/31 ngày, năm nhuận, mở sổ ngày 31, số dư về 0 giữa kỳ, tắt server cả năm |

Giải thích các nguyên lý hướng đối tượng kèm link code: [`docs/Self-Explanation-OOP.md`](docs/Self-Explanation-OOP.md).

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
- **Rollback:** trước mỗi thao tác ghi, `BankRepository` chụp lại dữ liệu; lỗi ở bất kỳ bước nào (kể cả lỗi ghi file) thì khôi phục bản chụp, file chính không bị ghi dở. `BankService` ghi thêm 1 giao dịch trạng thái `failed` (không làm đổi số dư) và 1 thông báo "thất bại – đã hoàn tác".
- **Tài khoản bị khóa:** không nạp, rút, chuyển hay nhận tiền; số tiền khả dụng = 0; lưu thời điểm khóa. Sổ tiết kiệm bị khóa **vẫn được cộng lãi tự động** nhưng không "Nhận lãi ngay" được.
- **File dữ liệu hỏng khi khởi động:** đổi tên thành `bank-data.json.corrupt-...` để giữ lại, rồi tạo dữ liệu mẫu mới.
- **Thống kê:** chỉ giao dịch thành công mới tính tiền; khi xem tất cả tài khoản, chuyển nội bộ được tính cả vào (TK nhận) lẫn ra (TK nguồn).

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
| Tuần 3 | Nhân – kịch bản test rollback (viết trước code) | `NganHang.Tests/RollbackTests.cs`, `NganHang.Tests/TestBank.cs`, `Data/FaultInjection.cs` |
| Tuần 3 | Nhân – rollback, giao dịch thất bại, khóa tài khoản, sao lưu file hỏng, test biên | `Data/BankRepository.cs`, `Services/BankService.cs`, `wwwroot/js/pages/transactions.js`, `NganHang.Tests/EdgeCaseTests.cs` |
| Tuần 3 | Luân – thống kê theo kỳ + trang Thống kê | `Services/BankService.cs`, `Controllers/StatisticsController.cs`, `wwwroot/thong-ke.html`, `wwwroot/js/pages/statistics.js`, `NganHang.Tests/StatisticsTests.cs` |
| Tuần 3 | Luân – kế thừa / đa hình tài khoản, test biên cộng lãi, Self-Explanation | `Models/Accounts.cs`, `NganHang.Tests/AccountModelTests.cs`, `NganHang.Tests/InterestEdgeTests.cs`, `docs/Self-Explanation-OOP.md` |
| Tuần 4 | Nhân – test khóa/mở khóa (viết trước) | `NganHang.Tests/LockTests.cs`, `Models/Dtos.cs` |
| Tuần 4 | Nhân – khóa/mở khóa: thời điểm khóa, khả dụng = 0, nút Khóa/Mở khóa, nhãn "Đã khóa", chặn nạp/rút/chuyển trên giao diện | `Models/Accounts.cs`, `Services/BankService.cs`, `wwwroot/js/pages/accounts.js`, `wwwroot/js/money.js`, `wwwroot/js/pages/transfer.js`, `wwwroot/js/store.js` |
| Tuần 4 | Luân – test cộng lãi khi sổ bị khóa (viết trước) | `NganHang.Tests/LockedInterestTests.cs`, `NganHang.Tests/AccountModelTests.cs` |
| Tuần 4 | Luân – sổ bị khóa vẫn có lãi; rà soát sửa lỗi hiển thị (số khả dụng, tên tài khoản, trang Cộng lãi) | `Models/Accounts.cs`, `Services/BankService.cs`, `wwwroot/js/pages/interest.js`, `wwwroot/js/pages/accounts.js` |
