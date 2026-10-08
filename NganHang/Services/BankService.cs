using System.Globalization;
using NganHang.Data;
using NganHang.Models;

namespace NganHang.Services;

/// <summary>
/// Toàn bộ nghiệp vụ ngân hàng nằm ở đây:
/// số dư, số dư sau giao dịch, lịch sử số dư, lãi dự kiến, nạp/rút, thấu chi, mở tài khoản.
/// </summary>
public class BankService
{
    public const long MaxAmount = 1_000_000_000;
    public const long MinSavingDeposit = 1_000_000;

    /// <summary>Lãi suất theo kỳ hạn (tháng → %/năm)</summary>
    public static readonly IReadOnlyDictionary<int, double> SavingRates = new Dictionary<int, double>
    {
        [1] = 4.5, [3] = 5.2, [6] = 5.8, [12] = 6.3
    };

    private static readonly long[] AllowedOverdraftLimits = { 0, 5_000_000, 10_000_000 };

    private readonly BankRepository _repo;

    public BankService(BankRepository repo)
    {
        _repo = repo;
    }

    // ============================================================
    //  ĐỌC DỮ LIỆU
    // ============================================================

    /// <summary>Toàn bộ trạng thái cho giao diện.</summary>
    public StateDto GetState() => _repo.Read(d => new StateDto(
        d.User,
        d.Accounts.Select(a => ToDto(d, a, DateTime.Now)).ToList(),
        BuildTransactions(d),
        d.Notifications.OrderByDescending(n => n.Date).ToList(),
        SavingRates.ToDictionary(kv => kv.Key.ToString(CultureInfo.InvariantCulture), kv => kv.Value)));

    public List<AccountDto> GetAccounts() =>
        _repo.Read(d => d.Accounts.Select(a => ToDto(d, a, DateTime.Now)).ToList());

    public AccountDto? GetAccount(string id) => _repo.Read(d =>
    {
        var acc = d.Accounts.FirstOrDefault(a => a.Id == id);
        return acc == null ? null : ToDto(d, acc, DateTime.Now);
    });

    public List<TransactionDto> GetTransactions(string? accountId, int? limit) => _repo.Read(d =>
    {
        IEnumerable<TransactionDto> list = BuildTransactions(d);
        if (!string.IsNullOrEmpty(accountId)) list = list.Where(t => t.AccountId == accountId);
        if (limit is > 0) list = list.Take(limit.Value);
        return list.ToList();
    });

    /// <summary>
    /// Chuỗi số dư theo thời gian cho biểu đồ.
    /// mode: monthly (cuối mỗi tháng) | weekly | daily. Điểm cuối không vượt quá hiện tại.
    /// </summary>
    public ServiceResult<List<BalancePointDto>> GetBalanceHistory(IReadOnlyCollection<string> accountIds, DateTime from, DateTime to, string mode)
    {
        if (accountIds.Count == 0) return ServiceResult<List<BalancePointDto>>.Fail("Vui lòng chọn tài khoản.");
        if (mode != "monthly" && mode != "weekly" && mode != "daily")
            return ServiceResult<List<BalancePointDto>>.Fail("Kiểu thống kê không hợp lệ.");
        from = from.Date;
        to = to.Date;
        if (from > to) return ServiceResult<List<BalancePointDto>>.Fail("Từ ngày phải trước hoặc bằng đến ngày.");
        if ((to - from).TotalDays > 800) return ServiceResult<List<BalancePointDto>>.Fail("Chỉ xem tối đa 2 năm mỗi lần.");

        return _repo.Read(d =>
        {
            var missing = accountIds.FirstOrDefault(id => d.Accounts.All(a => a.Id != id));
            if (missing != null) return ServiceResult<List<BalancePointDto>>.Missing("Không tìm thấy tài khoản " + missing + ".");

            var now = DateTime.Now;
            var end = EndOfDay(to);
            var points = new List<BalancePointDto>();

            if (mode == "monthly")
            {
                var cursor = new DateTime(from.Year, from.Month, 1);
                while (cursor <= end)
                {
                    var pointDate = EndOfDay(new DateTime(cursor.Year, cursor.Month, DateTime.DaysInMonth(cursor.Year, cursor.Month)));
                    if (pointDate > end) pointDate = end;
                    if (pointDate > now) pointDate = now;
                    points.Add(new BalancePointDto(pointDate, BalanceAt(d, accountIds, pointDate), "Tháng " + cursor.Month));
                    cursor = cursor.AddMonths(1);
                }
            }
            else
            {
                var step = mode == "weekly" ? 7 : 1;
                var dates = new List<DateTime>();
                for (var c = from; c <= to; c = c.AddDays(step)) dates.Add(c);
                if (dates.Count == 0 || dates[^1] != to) dates.Add(to); // luôn có điểm cuối = ngày kết thúc
                foreach (var day in dates)
                {
                    var pointDate = EndOfDay(day);
                    if (pointDate > now) pointDate = now;
                    points.Add(new BalancePointDto(pointDate, BalanceAt(d, accountIds, pointDate),
                        day.ToString("dd/MM", CultureInfo.InvariantCulture)));
                }
            }
            return ServiceResult<List<BalancePointDto>>.Success(points);
        });
    }

    // ============================================================
    //  GHI DỮ LIỆU
    // ============================================================

    public ServiceResult<DepositWithdrawResult> Deposit(string accountId, long amount) => _repo.Write(d =>
    {
        var acc = d.Accounts.FirstOrDefault(a => a.Id == accountId);
        if (acc == null) return (ServiceResult<DepositWithdrawResult>.Missing("Không tìm thấy tài khoản."), false);
        if (amount <= 0) return (ServiceResult<DepositWithdrawResult>.Fail("Vui lòng nhập số tiền lớn hơn 0."), false);
        if (amount > MaxAmount)
            return (ServiceResult<DepositWithdrawResult>.Fail("Số tiền tối đa mỗi lần nạp là " + Vnd(MaxAmount) + "."), false);

        acc.Balance += amount;
        var tx = AddTransaction(d, acc, "deposit", amount, "Nạp tiền vào tài khoản", acc.Name);
        AddNotification(d, "Nạp " + Vnd(amount) + " vào " + acc.Name + " thành công");
        return (ServiceResult<DepositWithdrawResult>.Success(Result(d, acc, tx, false)), true);
    });

    public ServiceResult<DepositWithdrawResult> Withdraw(string accountId, long amount) => _repo.Write(d =>
    {
        var acc = d.Accounts.FirstOrDefault(a => a.Id == accountId);
        if (acc == null) return (ServiceResult<DepositWithdrawResult>.Missing("Không tìm thấy tài khoản."), false);
        if (amount <= 0) return (ServiceResult<DepositWithdrawResult>.Fail("Vui lòng nhập số tiền lớn hơn 0."), false);
        if (amount > MaxAmount)
            return (ServiceResult<DepositWithdrawResult>.Fail("Số tiền tối đa mỗi lần rút là " + Vnd(MaxAmount) + "."), false);

        // Thanh toán: được dùng thấu chi -> khả dụng = số dư + hạn mức.
        // Tiết kiệm: không được rút quá số dư.
        var available = Available(acc);
        if (amount > available)
        {
            var msg = acc.Type == "payment"
                ? "Số tiền rút vượt quá hạn mức khả dụng."
                : "Số dư không đủ. Số tiền rút tối đa là " + Vnd(available) + ".";
            return (ServiceResult<DepositWithdrawResult>.Fail(msg), false);
        }

        var usesOverdraft = acc.Type == "payment" && amount > acc.Balance;
        acc.Balance -= amount;
        var tx = AddTransaction(d, acc, "withdraw", amount, "Rút tiền từ tài khoản", acc.Name);
        AddNotification(d, "Rút " + Vnd(amount) + " từ " + acc.Name + " thành công");
        return (ServiceResult<DepositWithdrawResult>.Success(Result(d, acc, tx, usesOverdraft)), true);
    });

    public ServiceResult<AccountDto> OpenAccount(OpenAccountRequest req) => _repo.Write(d =>
    {
        var type = req.Type;
        if (type != "saving" && type != "payment")
            return (ServiceResult<AccountDto>.Fail("Vui lòng chọn loại tài khoản."), false);
        var amount = req.InitialAmount;
        if (amount < 0) return (ServiceResult<AccountDto>.Fail("Số tiền ban đầu không được âm."), false);
        if (amount > MaxAmount) return (ServiceResult<AccountDto>.Fail("Số tiền tối đa là " + Vnd(MaxAmount) + "."), false);

        int term = 0;
        double rate = 0;
        long limit = 0;
        if (type == "saving")
        {
            if (!SavingRates.TryGetValue(req.TermMonths, out rate))
                return (ServiceResult<AccountDto>.Fail("Vui lòng chọn kỳ hạn."), false);
            if (amount < MinSavingDeposit)
                return (ServiceResult<AccountDto>.Fail("Số tiền gửi tối thiểu là " + Vnd(MinSavingDeposit) + "."), false);
            term = req.TermMonths;
        }
        else
        {
            if (!AllowedOverdraftLimits.Contains(req.OverdraftLimit))
                return (ServiceResult<AccountDto>.Fail("Hạn mức thấu chi không hợp lệ."), false);
            limit = req.OverdraftLimit;
        }

        var baseName = type == "saving" ? "Tài khoản tiết kiệm" : "Tài khoản thanh toán";
        var sameType = d.Accounts.Count(a => a.Type == type);
        var acc = new Account
        {
            Id = "acc-" + Guid.NewGuid().ToString("N")[..10],
            Type = type,
            Name = sameType > 0 ? baseName + " " + (sameType + 1) : baseName,
            AccountNumber = NewAccountNumber(d),
            Balance = 0,
            Status = "active",
            OpenedDate = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ProductName = type == "saving" ? "Tiết kiệm có kỳ hạn" : "Thanh toán",
            InterestRate = rate,
            TermMonths = term,
            OverdraftLimit = limit,
            Branch = "Chi nhánh Thủ Đức"
        };
        d.Accounts.Add(acc);

        // Tiền nạp ban đầu được ghi thành một giao dịch để lịch sử số dư khớp
        if (amount > 0)
        {
            acc.Balance = amount;
            AddTransaction(d, acc, "open", amount, "Nạp tiền khi mở tài khoản", acc.Name);
        }
        AddNotification(d, "Mở " + acc.Name + " thành công – STK " + acc.AccountNumber);
        return (ServiceResult<AccountDto>.Success(ToDto(d, acc, DateTime.Now)), true);
    });

    public bool MarkNotificationRead(string id) => _repo.Write(d =>
    {
        var n = d.Notifications.FirstOrDefault(x => x.Id == id);
        if (n == null) return (false, false);
        var changed = !n.Read;
        n.Read = true;
        return (true, changed);
    });

    public void MarkAllNotificationsRead() => _repo.Write(d =>
    {
        foreach (var n in d.Notifications) n.Read = true;
        return (true, true);
    });

    public UserProfile SetOtp(bool enabled) => _repo.Write(d =>
    {
        d.User.OtpEnabled = enabled;
        return (d.User, true);
    });

    public void Reset() => _repo.Reset();

    // ============================================================
    //  HÀM TÍNH TOÁN
    // ============================================================

    /// <summary>Giao dịch thành công mới làm thay đổi số dư.</summary>
    private static long Signed(Transaction t)
    {
        if (t.Status != "success") return 0;
        return TransactionTypes.IsIncoming(t.Type) ? t.Amount : -t.Amount;
    }

    private static long Available(Account acc) =>
        acc.Type == "payment" ? acc.Balance + acc.OverdraftLimit : Math.Max(acc.Balance, 0);

    /// <summary>
    /// Danh sách giao dịch mới nhất trước, kèm số dư sau giao dịch.
    /// Tính NGƯỢC từ số dư hiện tại, riêng cho từng tài khoản (không cộng lẫn).
    /// </summary>
    private static List<TransactionDto> BuildTransactions(BankData d)
    {
        var running = d.Accounts.ToDictionary(a => a.Id, a => a.Balance);
        var result = new List<TransactionDto>();
        foreach (var t in d.Transactions.OrderByDescending(t => t.Date).ThenByDescending(t => t.Seq))
        {
            running.TryGetValue(t.AccountId, out var after);
            var signed = Signed(t);
            running[t.AccountId] = after - signed;
            var display = TransactionTypes.IsIncoming(t.Type) ? t.Amount : -t.Amount;
            result.Add(new TransactionDto(t.Id, t.Seq, t.Code, t.AccountId, t.Type, t.Amount, t.Title, t.Subtitle,
                t.Date, t.Status, after, signed != 0 ? signed : display));
        }
        return result;
    }

    /// <summary>Tổng số dư của các tài khoản tại thời điểm t.</summary>
    private static long BalanceAt(BankData d, IEnumerable<string> accountIds, DateTime t)
    {
        long total = 0;
        foreach (var id in accountIds.Distinct())
        {
            var acc = d.Accounts.FirstOrDefault(a => a.Id == id);
            if (acc == null) continue;
            var later = d.Transactions.Where(x => x.AccountId == id && x.Date > t).Sum(x => Signed(x));
            total += acc.Balance - later;
        }
        return total;
    }

    /// <summary>% thay đổi so với 30 ngày trước; null nếu không so sánh được.</summary>
    private static double? Growth(BankData d, Account acc, DateTime now)
    {
        var ids = new[] { acc.Id };
        var current = BalanceAt(d, ids, now);
        var previous = BalanceAt(d, ids, now.AddDays(-30));
        if (previous <= 0) return null;
        return Math.Round((current - previous) * 100.0 / previous, 2);
    }

    /// <summary>
    /// Kỳ hạn hiện tại của sổ tiết kiệm (tự tái tục khi đến hạn) và tiền lãi dự kiến:
    /// lãi = số dư × (lãi suất / 100) × số ngày / 365
    /// </summary>
    public static TermInfo? SavingTerm(Account acc)
    {
        if (acc.Type != "saving" || acc.TermMonths <= 0) return null;
        if (!DateTime.TryParseExact(acc.OpenedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var start))
            return null;
        var today = DateTime.Today;
        var maturity = start.AddMonths(acc.TermMonths);
        var guard = 0;
        while (maturity <= today && guard < 1000)
        {
            start = maturity;
            maturity = start.AddMonths(acc.TermMonths);
            guard++;
        }
        var days = (int)(maturity - start).TotalDays;
        var interest = (long)Math.Round(Math.Max(acc.Balance, 0) * (acc.InterestRate / 100.0) * days / 365.0,
            MidpointRounding.AwayFromZero);
        return new TermInfo(start, maturity, days, interest);
    }

    private static AccountDto ToDto(BankData d, Account a, DateTime now) => new(
        a.Id, a.Type, a.Name, a.AccountNumber, a.Balance, a.Status, a.OpenedDate, a.ProductName,
        a.InterestRate, a.TermMonths, a.OverdraftLimit, a.Branch,
        Available(a), Growth(d, a, now), SavingTerm(a));

    private static DepositWithdrawResult Result(BankData d, Account acc, Transaction tx, bool usesOverdraft)
    {
        var txDto = BuildTransactions(d).First(t => t.Id == tx.Id);
        return new DepositWithdrawResult(ToDto(d, acc, DateTime.Now), txDto, usesOverdraft);
    }

    private static Transaction AddTransaction(BankData d, Account acc, string type, long amount, string title, string subtitle)
    {
        d.Seq = Math.Max(d.Seq, d.Transactions.Count) + 1;
        var tx = new Transaction
        {
            Id = "tx-" + d.Seq + "-" + Guid.NewGuid().ToString("N")[..6],
            Seq = d.Seq,
            Code = "GD" + (1245700 + d.Seq),
            AccountId = acc.Id,
            Type = type,
            Amount = amount,
            Title = title,
            Subtitle = subtitle,
            Date = DateTime.Now,
            Status = "success"
        };
        d.Transactions.Add(tx);
        return tx;
    }

    private static void AddNotification(BankData d, string title)
    {
        d.Notifications.Insert(0, new Notification
        {
            Id = "n" + Guid.NewGuid().ToString("N")[..8],
            Title = title,
            Date = DateTime.Now,
            Read = false
        });
        if (d.Notifications.Count > 20) d.Notifications.RemoveRange(20, d.Notifications.Count - 20);
    }

    /// <summary>Tạo số tài khoản dạng "0123 4567 xxxx" không trùng với tài khoản đã có.</summary>
    private static string NewAccountNumber(BankData d)
    {
        var used = d.Accounts.Select(a => a.AccountNumber.Replace(" ", "")).ToHashSet();
        for (var i = 0; i < 20000; i++)
        {
            var tail = Random.Shared.Next(0, 10000).ToString("D4", CultureInfo.InvariantCulture);
            if (!used.Contains("01234567" + tail)) return "0123 4567 " + tail;
        }
        // Dự phòng cực hiếm khi đã dùng hết 10.000 số
        var digits = DateTime.Now.Ticks.ToString(CultureInfo.InvariantCulture);
        return "0987 " + digits[^8..^4] + " " + digits[^4..];
    }

    private static DateTime EndOfDay(DateTime day) => day.Date.AddDays(1).AddTicks(-1);

    /// <summary>12580000 → "12.580.000 đ"</summary>
    public static string Vnd(long value) =>
        (value < 0 ? "-" : "") + Math.Abs(value).ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".") + " đ";
}
