# Self-Explanation: các nguyên lý hướng đối tượng trong Hệ thống Ngân hàng

Nhóm: Nhân (Đóng gói, Trừu tượng) – Luân (Kế thừa, Đa hình, SOLID). Tuần 3.

Mỗi mục gồm: nguyên lý là gì (nói theo cách hiểu của nhóm), áp dụng ở đâu trong code (kèm link GitHub tới đúng dòng), và vì sao làm vậy.
Link trỏ vào nhánh `main`: https://github.com/nguyennhan9854-png/Banking-system

---

## 1. Đóng gói (Encapsulation) – Nhân

**Hiểu đơn giản:** dữ liệu quan trọng được "giấu" bên trong lớp, bên ngoài chỉ được thay đổi qua các hàm do lớp cho phép, nên không ai sửa sai được.

**Áp dụng:**

- Số dư tài khoản chỉ đọc được từ bên ngoài, không gán trực tiếp được: `Balance { get; internal set; }`
  → [Models/Accounts.cs#L37-L38](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Models/Accounts.cs#L37-L38)
- Muốn đổi số dư phải gọi `Credit` (cộng) hoặc `Debit` (trừ). `Debit` tự chặn nếu trừ quá số tiền khả dụng, kể cả khi nơi gọi quên kiểm tra
  → [Models/Accounts.cs#L69-L82](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Models/Accounts.cs#L69-L82)
- Toàn bộ dữ liệu `_data` là `private` trong `BankRepository`; bên ngoài chỉ được dùng `Read` / `Write`, cả hai đều đi qua `lock` nên nhiều request cùng lúc không làm hỏng dữ liệu
  → [Data/BankRepository.cs#L17](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Data/BankRepository.cs#L17),
  [Data/BankRepository.cs#L45-L76](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Data/BankRepository.cs#L45-L76)
- Mọi nghiệp vụ làm thay đổi số dư (nạp, rút, chuyển, cộng lãi) đều nằm trong `BankService`; controller không tự sửa số dư
  → [Services/BankService.cs#L298-L321](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Services/BankService.cs#L298-L321) (rút tiền),
  [Services/BankService.cs#L332-L430](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Services/BankService.cs#L332-L430) (chuyển tiền)
- Lớp `PendingOperation` (ghi nhớ thao tác đang làm dở để ghi giao dịch thất bại) là `private` bên trong `BankService`, chỉ dùng cho rollback
  → [Services/BankService.cs#L606-L626](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Services/BankService.cs#L606-L626)

**Vì sao:** với ngân hàng, số dư bị sửa sai chỗ là lỗi nặng nhất. Khi chỉ có một "cửa" để thay đổi số dư, nhóm kiểm thử một chỗ là đủ, và rollback chỉ cần bao quanh `Write`.
Test chứng minh: `OOP05`, `OOP06` trong [NganHang.Tests/AccountModelTests.cs](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang.Tests/AccountModelTests.cs).

---

## 2. Trừu tượng (Abstraction) – Nhân

**Hiểu đơn giản:** mỗi tầng chỉ cần biết tầng dưới "làm được gì", không cần biết "làm thế nào".

**Áp dụng – chia 3 tầng Controller → Service → Repository:**

| Tầng | Biết gì | Không cần biết |
|---|---|---|
| Controller (vd. `TransfersController`) | nhận HTTP, gọi `_bank.Transfer(req)`, đổi kết quả thành mã 200/400/404/409 | quy tắc thấu chi, cách lưu file |
| Service (`BankService`) | toàn bộ nghiệp vụ | dữ liệu nằm trong file JSON hay database |
| Repository (`BankRepository`) | đọc/ghi file, lock, chụp và khôi phục dữ liệu | thế nào là chuyển tiền hợp lệ |

- Controller chỉ có vài dòng → [Controllers/TransfersController.cs#L22-L30](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Controllers/TransfersController.cs#L22-L30),
  [Controllers/StatisticsController.cs#L20-L31](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Controllers/StatisticsController.cs#L20-L31)
- Lớp trừu tượng `Account`: `BankService` làm việc với "một tài khoản" chung, không cần biết là tiết kiệm hay thanh toán
  → [Models/Accounts.cs#L27](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Models/Accounts.cs#L27)
- Interface `IFaultInjector`: `BankService` chỉ biết "có một điểm có thể gây lỗi", không biết đó là bản chạy thật (`NoFaults`) hay bản giả trong test (`FakeFaults`)
  → [Data/FaultInjection.cs#L32-L47](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Data/FaultInjection.cs#L32-L47)

**Vì sao:** tuần 3 nhóm thêm rollback chỉ bằng cách sửa `BankRepository.Write` và thêm `Atomic` trong `BankService`; không phải sửa controller hay giao diện nào. Đó là lợi ích của trừu tượng.

---

## 3. Kế thừa (Inheritance) – Luân

**Hiểu đơn giản:** lớp con nhận lại mọi thứ của lớp cha và chỉ viết thêm phần riêng của mình.

**Áp dụng:**

```
             Account (abstract)
   Id, Name, AccountNumber, Balance, Status, Credit(), Debit(), CheckSpend()
          /                                   \
 SavingAccount                           PaymentAccount
 + InterestRate, TermMonths,              + OverdraftLimit
   LastInterestDate
```

- Lớp cha `Account` → [Models/Accounts.cs#L27-L87](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Models/Accounts.cs#L27-L87)
- `SavingAccount : Account` → [Models/Accounts.cs#L90-L111](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Models/Accounts.cs#L90-L111)
- `PaymentAccount : Account` → [Models/Accounts.cs#L114-L128](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Models/Accounts.cs#L114-L128)
- Khi mở tài khoản, tạo đúng lớp con → [Services/BankService.cs#L537-L548](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Services/BankService.cs#L537-L548)
- Đọc file JSON tạo lại đúng lớp con dựa vào trường `"type"` → [Models/Accounts.cs#L134-L159](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Models/Accounts.cs#L134-L159)

**Trước tuần 3:** một lớp `Account` chung chứa cả `InterestRate` lẫn `OverdraftLimit`; tài khoản thanh toán vẫn có lãi suất = 0, tài khoản tiết kiệm vẫn có hạn mức thấu chi = 0 – thừa và dễ nhầm.
**Sau tuần 3:** mỗi lớp chỉ có thuộc tính của mình; file dữ liệu cũ của tuần 2 vẫn đọc được (test `OOP09`).

---

## 4. Đa hình (Polymorphism) – Luân

**Hiểu đơn giản:** cùng một lời gọi, mỗi đối tượng trả lời theo cách riêng của nó.

**Áp dụng – các thành phần được lớp con ghi đè (`override`):**

| Thành phần trong `Account` | `SavingAccount` | `PaymentAccount` |
|---|---|---|
| `AvailableBalance` (số tiền được chi) | số dư, không âm | số dư + hạn mức thấu chi |
| `EarnsInterest` (có được cộng lãi) | có, nếu lãi suất > 0 (tuần 4: kể cả khi đang bị khóa) | không |
| `UsesOverdraft(amount)` | luôn không | có, nếu chi vượt số dư |
| `SpendError(verb)` (thông báo khi chi vượt) | "Số dư không đủ…" | "…vượt quá hạn mức khả dụng" |

- Khai báo ở lớp cha → [Models/Accounts.cs#L51-L67](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Models/Accounts.cs#L51-L67)
- Ghi đè ở `SavingAccount` → [Models/Accounts.cs#L106-L110](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Models/Accounts.cs#L106-L110)
- Ghi đè ở `PaymentAccount` → [Models/Accounts.cs#L121-L127](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Models/Accounts.cs#L121-L127)
- `BankService` chỉ gọi chung, không còn `if (acc.Type == "payment")`:
  rút tiền [Services/BankService.cs#L309](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Services/BankService.cs#L309),
  chuyển tiền [Services/BankService.cs#L398-L406](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Services/BankService.cs#L398-L406),
  cộng lãi chỉ lấy tài khoản có lãi [Services/BankService.cs#L453](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Services/BankService.cs#L453)

**So sánh trước / sau:**

```csharp
// Tuần 2 – kiểm tra theo chuỗi Type, lặp ở nhiều nơi
private static long Available(Account acc) =>
    acc.Type == "payment" ? acc.Balance + acc.OverdraftLimit : Math.Max(acc.Balance, 0);
var usesOverdraft = acc.Type == "payment" && amount > acc.Balance;

// Tuần 3 – đa hình, mỗi lớp tự biết
var spendError = acc.CheckSpend(amount, "rút");
var usesOverdraft = acc.UsesOverdraft(amount);
```

**Vì sao:** muốn thêm loại "tài khoản tín dụng" chỉ cần viết lớp `CreditAccount : Account` và ghi đè 4 thành phần trên; `BankService` gần như không phải sửa.
Test chứng minh: `OOP01` – `OOP04` (cùng một danh sách `List<Account>` cho ra kết quả khác nhau theo lớp con).

---

## 5. SOLID – Luân

| Nguyên lý | Hiểu đơn giản | Trong project |
|---|---|---|
| **S** – Single Responsibility | Mỗi lớp chỉ có một lý do để sửa | `BankRepository` chỉ lo lưu trữ + rollback; `BankService` chỉ lo nghiệp vụ; `StatisticsController` chỉ nhận request thống kê; `InterestBackgroundService` chỉ hẹn giờ gọi cộng lãi |
| **O** – Open/Closed | Thêm tính năng bằng cách mở rộng, ít sửa code cũ | Thêm loại tài khoản mới = thêm lớp con (mục 4); thêm điểm lỗi mới = thêm hằng trong `FaultPoints` |
| **L** – Liskov Substitution | Lớp con dùng thay lớp cha được mà không sai | Mọi chỗ nhận `Account` đều chạy đúng với cả `SavingAccount` lẫn `PaymentAccount` (test `OOP01`, toàn bộ test chuyển/rút cũ vẫn qua) |
| **I** – Interface Segregation | Interface nhỏ, đúng việc | `IFaultInjector` chỉ có đúng 1 hàm `Hit(point)` |
| **D** – Dependency Inversion / Dependency Injection | Lớp không tự `new` thứ mình phụ thuộc mà nhận từ ngoài vào | `BankService` nhận `BankRepository`, `IFaultInjector`, `ILogger` qua constructor; `Program.cs` đăng ký chúng vào DI; unit test truyền `FakeFaults` vào để giả lập lỗi |

Link:
- Constructor nhận phụ thuộc → [Services/BankService.cs#L42-L47](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Services/BankService.cs#L42-L47)
- Đăng ký DI → [Program.cs#L9-L17](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Program.cs#L9-L17)
- Test thay bản giả vào → [NganHang.Tests/TestBank.cs](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang.Tests/TestBank.cs)

**Vì sao DI quan trọng với tuần 3:** nhờ truyền `IFaultInjector` từ ngoài vào, nhóm viết được 17 kịch bản rollback (gây lỗi đúng lúc đã trừ tiền nhưng chưa cộng) mà không phải sửa code nghiệp vụ để test.

---

## 6. Rollback dùng các nguyên lý trên như thế nào (Nhân)

1. `BankRepository.Write` (đóng gói) chụp bản sao dữ liệu trước khi sửa; lỗi ở bất kỳ bước nào thì trả lại bản sao → [Data/BankRepository.cs#L58-L76](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Data/BankRepository.cs#L58-L76)
2. Ghi file qua file tạm, lỗi thì xóa file tạm, file chính giữ nguyên → [Data/BankRepository.cs#L139-L155](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Data/BankRepository.cs#L139-L155)
3. `BankService.Atomic` bắt lỗi, ghi 1 giao dịch `failed` + thông báo, trả về thông báo "đã hoàn tác" → [Services/BankService.cs#L632-L670](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang/Services/BankService.cs#L632-L672)
4. Kịch bản test được viết TRƯỚC khi có code (commit riêng) → [NganHang.Tests/RollbackTests.cs](https://github.com/nguyennhan9854-png/Banking-system/blob/main/NganHang.Tests/RollbackTests.cs)
