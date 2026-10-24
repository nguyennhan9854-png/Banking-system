using System.Globalization;
using Microsoft.Extensions.Logging.Abstractions;
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
    public const long MinTransfer = 10_000;
    public const int MaxNoteLength = 100;
    public const string BankName = "Hệ thống Ngân hàng";

    /// <summary>Lãi suất theo kỳ hạn (tháng → %/năm)</summary>
    public static readonly IReadOnlyDictionary<int, double> SavingRates = new Dictionary<int, double>
    {
        [1] = 4.5, [3] = 5.2, [6] = 5.8, [12] = 6.3
    };

    private static readonly long[] AllowedOverdraftLimits = { 0, 5_000_000, 10_000_000 };

    /// <summary>Thông báo khi một thao tác bị lỗi giữa chừng và đã được hoàn tác.</summary>
    public const string RolledBackMessage =
        "Giao dịch không thành công do sự cố hệ thống. Mọi thay đổi đã được hoàn tác, số dư của bạn giữ nguyên.";

    public const string InterestRolledBackMessage =
        "Đợt cộng lãi bị lỗi giữa chừng nên đã được hoàn tác, chưa kỳ nào được cộng. Hệ thống sẽ thử lại ở lần kiểm tra sau.";

    private readonly BankRepository _repo;
    private readonly IFaultInjector _faults;
    private readonly ILogger<BankService> _logger;

    /// <param name="repo">Kho dữ liệu (file JSON).</param>
    /// <param name="faults">Điểm cắm lỗi giả lập cho unit test rollback; chạy thật dùng NoFaults.</param>
    /// <param name="logger">Ghi log khi có sự cố (unit test có thể bỏ trống).</param>
    public BankService(BankRepository repo, IFaultInjector? faults = null, ILogger<BankService>? logger = null)
    {
        _repo = repo;
        _faults = faults ?? NoFaults.Instance;
        _logger = logger ?? NullLogger<BankService>.Instance;
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

    public ServiceResult<DepositWithdrawResult> Deposit(string accountId, long amount) => Atomic<DepositWithdrawResult>((d, op) =>
    {
        var acc = d.Accounts.FirstOrDefault(a => a.Id == accountId);
        if (acc == null) return (ServiceResult<DepositWithdrawResult>.Missing("Không tìm thấy tài khoản."), false);
        if (acc.Status != "active") return (ServiceResult<DepositWithdrawResult>.Fail("Tài khoản đang bị khóa."), false);
        if (amount <= 0) return (ServiceResult<DepositWithdrawResult>.Fail("Vui lòng nhập số tiền lớn hơn 0."), false);
        if (amount > MaxAmount)
            return (ServiceResult<DepositWithdrawResult>.Fail("Số tiền tối đa mỗi lần nạp là " + Vnd(MaxAmount) + "."), false);

        op.Describe(acc.Id, "deposit", amount, "Nạp tiền vào tài khoản", acc.Name,
            "Nạp " + Vnd(amount) + " vào " + acc.Name + " thất bại – đã hoàn tác, số dư giữ nguyên");
        acc.Balance += amount;
        _faults.Hit(FaultPoints.DepositAfterCredit);
        var tx = AddTransaction(d, acc, "deposit", amount, "Nạp tiền vào tài khoản", acc.Name);
        AddNotification(d, "Nạp " + Vnd(amount) + " vào " + acc.Name + " thành công");
        return (ServiceResult<DepositWithdrawResult>.Success(Result(d, acc, tx, false)), true);
    });

    public ServiceResult<DepositWithdrawResult> Withdraw(string accountId, long amount) => Atomic<DepositWithdrawResult>((d, op) =>
    {
        var acc = d.Accounts.FirstOrDefault(a => a.Id == accountId);
        if (acc == null) return (ServiceResult<DepositWithdrawResult>.Missing("Không tìm thấy tài khoản."), false);
        if (acc.Status != "active") return (ServiceResult<DepositWithdrawResult>.Fail("Tài khoản đang bị khóa."), false);
        if (amount <= 0) return (ServiceResult<DepositWithdrawResult>.Fail("Vui lòng nhập số tiền lớn hơn 0."), false);
        if (amount > MaxAmount)
            return (ServiceResult<DepositWithdrawResult>.Fail("Số tiền tối đa mỗi lần rút là " + Vnd(MaxAmount) + "."), false);

        // Thanh toán: được dùng thấu chi -> khả dụng = số dư + hạn mức.
        // Tiết kiệm: không được rút quá số dư.
        var spendError = CheckSpend(acc, amount, "rút");
        if (spendError != null) return (ServiceResult<DepositWithdrawResult>.Fail(spendError), false);

        var usesOverdraft = acc.Type == "payment" && amount > acc.Balance;
        op.Describe(acc.Id, "withdraw", amount, "Rút tiền từ tài khoản", acc.Name,
            "Rút " + Vnd(amount) + " từ " + acc.Name + " thất bại – đã hoàn tác, số dư giữ nguyên");
        acc.Balance -= amount;
        _faults.Hit(FaultPoints.WithdrawAfterDebit);
        var tx = AddTransaction(d, acc, "withdraw", amount, "Rút tiền từ tài khoản", acc.Name);
        AddNotification(d, "Rút " + Vnd(amount) + " từ " + acc.Name + " thành công");
        return (ServiceResult<DepositWithdrawResult>.Success(Result(d, acc, tx, usesOverdraft)), true);
    });

    /// <summary>
    /// Chuyển tiền.
    /// - Chuyển nội bộ: giữa 2 tài khoản của người dùng (ToAccountId, hoặc STK trùng với tài khoản của mình).
    /// - Chuyển ra ngoài: tới người nhận ở ngân hàng khác (cần STK, tên người nhận, ngân hàng).
    /// Kiểm tra: số tiền hợp lệ, không chuyển cho chính tài khoản nguồn,
    /// tài khoản tiết kiệm không được âm, tài khoản thanh toán không vượt hạn mức thấu chi.
    /// Nếu không hợp lệ thì KHÔNG thay đổi số dư và KHÔNG ghi giao dịch.
    /// Trừ nguồn + cộng đích + 2 giao dịch là MỘT khối: lỗi ở bất kỳ bước nào thì hoàn tác tất cả (rollback)
    /// và ghi lại 1 giao dịch trạng thái "failed".
    /// </summary>
    public ServiceResult<TransferResult> Transfer(TransferRequest req) => Atomic<TransferResult>((d, op) =>
    {
        if (req == null) return (ServiceResult<TransferResult>.Fail("Thiếu thông tin chuyển tiền."), false);

        // 1. Tài khoản nguồn
        var source = d.Accounts.FirstOrDefault(a => a.Id == req.FromAccountId);
        if (source == null) return (ServiceResult<TransferResult>.Missing("Không tìm thấy tài khoản nguồn."), false);
        if (source.Status != "active") return (ServiceResult<TransferResult>.Fail("Tài khoản nguồn đang bị khóa."), false);

        // 2. Số tiền
        var amount = req.Amount;
        if (amount <= 0) return (ServiceResult<TransferResult>.Fail("Vui lòng nhập số tiền lớn hơn 0."), false);
        if (amount < MinTransfer)
            return (ServiceResult<TransferResult>.Fail("Số tiền chuyển tối thiểu là " + Vnd(MinTransfer) + "."), false);
        if (amount > MaxAmount)
            return (ServiceResult<TransferResult>.Fail("Số tiền tối đa mỗi lần chuyển là " + Vnd(MaxAmount) + "."), false);

        // 3. Người nhận
        Account? to = null;
        string recipientName;
        string recipientBank;
        string recipientNumber;
        if (!string.IsNullOrWhiteSpace(req.ToAccountId))
        {
            to = d.Accounts.FirstOrDefault(a => a.Id == req.ToAccountId);
            if (to == null) return (ServiceResult<TransferResult>.Missing("Không tìm thấy tài khoản nhận."), false);
            recipientName = to.Name;
            recipientBank = BankName;
            recipientNumber = to.AccountNumber;
        }
        else
        {
            var digits = OnlyDigits(req.ToAccountNumber);
            if (digits.Length < 6 || digits.Length > 19)
                return (ServiceResult<TransferResult>.Fail("Số tài khoản nhận phải gồm 6 đến 19 chữ số."), false);

            // STK nhập vào trùng với tài khoản của chính mình -> coi là chuyển nội bộ
            to = d.Accounts.FirstOrDefault(a => OnlyDigits(a.AccountNumber) == digits);
            if (to != null)
            {
                recipientName = to.Name;
                recipientBank = BankName;
                recipientNumber = to.AccountNumber;
            }
            else
            {
                recipientName = (req.ToName ?? "").Trim();
                recipientBank = (req.ToBank ?? "").Trim();
                if (recipientName.Length == 0) return (ServiceResult<TransferResult>.Fail("Vui lòng nhập tên người nhận."), false);
                if (recipientName.Length > 50) return (ServiceResult<TransferResult>.Fail("Tên người nhận tối đa 50 ký tự."), false);
                if (recipientBank.Length == 0) return (ServiceResult<TransferResult>.Fail("Vui lòng chọn ngân hàng nhận."), false);
                recipientNumber = digits;
            }
        }
        if (to != null && to.Id == source.Id)
            return (ServiceResult<TransferResult>.Fail("Không thể chuyển tiền vào chính tài khoản nguồn."), false);
        if (to != null && to.Status != "active")
            return (ServiceResult<TransferResult>.Fail("Tài khoản nhận đang bị khóa."), false);

        // 4. Nội dung chuyển khoản
        var note = (req.Note ?? "").Trim();
        if (note.Length > MaxNoteLength)
            return (ServiceResult<TransferResult>.Fail("Nội dung chuyển khoản tối đa " + MaxNoteLength + " ký tự."), false);
        if (note.Length == 0) note = d.User.Name + " chuyển tiền";

        // 5. Kiểm tra số dư / hạn mức thấu chi
        var spendError = CheckSpend(source, amount, "chuyển");
        if (spendError != null) return (ServiceResult<TransferResult>.Fail(spendError), false);

        // 6. Ghi nhận: trừ nguồn, cộng đích (nếu nội bộ), lưu 2 giao dịch
        var usesOverdraft = source.Type == "payment" && amount > source.Balance;
        var outSubtitle = recipientBank + " · " + recipientNumber + " · " + note;
        op.Describe(source.Id, "transfer_out", amount, "Chuyển tiền đến " + recipientName, outSubtitle,
            "Chuyển " + Vnd(amount) + " đến " + recipientName + " thất bại – đã hoàn tác, số dư giữ nguyên");
        source.Balance -= amount;
        var outTx = AddTransaction(d, source, "transfer_out", amount, "Chuyển tiền đến " + recipientName, outSubtitle);
        _faults.Hit(FaultPoints.TransferAfterDebit);

        Transaction? inTx = null;
        if (to != null)
        {
            to.Balance += amount;
            inTx = AddTransaction(d, to, "transfer_in", amount,
                "Nhận tiền từ " + source.Name,
                BankName + " · " + source.AccountNumber + " · " + note);
        }
        _faults.Hit(FaultPoints.TransferAfterCredit);

        AddNotification(d, "Chuyển " + Vnd(amount) + " từ " + source.Name + " đến " + recipientName + " thành công");

        var list = BuildTransactions(d);
        var result = new TransferResult(
            ToDto(d, source, DateTime.Now),
            to == null ? null : ToDto(d, to, DateTime.Now),
            list.First(t => t.Id == outTx.Id),
            inTx == null ? null : list.First(t => t.Id == inTx.Id),
            usesOverdraft);
        return (ServiceResult<TransferResult>.Success(result), true);
    });

    // ============================================================
    //  CỘNG LÃI TIẾT KIỆM
    //  - Lãi tính theo số dư cuối mỗi ngày: lãi ngày = số dư × lãi suất / 100 / 365.
    //  - Mỗi tháng (tính từ ngày cộng lãi gần nhất) hệ thống TỰ ĐỘNG cộng lãi một lần
    //    (InterestBackgroundService gọi ApplyDueInterest mỗi giờ và khi khởi động).
    //  - Mỗi kỳ chỉ được cộng một lần (LastInterestDate dời sang kỳ sau).
    // ============================================================

    /// <summary>
    /// Cộng lãi cho mọi kỳ đã đến hạn tính đến thời điểm <paramref name="asOf"/> (mặc định: bây giờ).
    /// Gọi nhiều lần cũng không cộng trùng.
    /// </summary>
    public InterestRunResult ApplyDueInterest(DateTime? asOf = null)
    {
        try
        {
            return _repo.Write(d =>
            {
                var now = asOf ?? DateTime.Now;
                var posted = new List<Transaction>();
                var changed = false;
                foreach (var acc in d.Accounts.Where(EarnsInterest).ToList())
                {
                    changed |= PostDuePeriods(d, acc, now, posted);
                }
                return (InterestResult(d, posted), changed);
            });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // BankRepository đã khôi phục dữ liệu: không kỳ nào được cộng dở dang
            _logger.LogError(ex, "Cộng lãi bị lỗi giữa chừng, đã hoàn tác");
            return new InterestRunResult(new List<TransactionDto>(), 0, InterestRolledBackMessage);
        }
    }

    /// <summary>
    /// Nhận lãi ngay cho một tài khoản tiết kiệm: cộng các kỳ đã đến hạn (nếu có)
    /// và phần lãi tạm tính từ đầu kỳ hiện tại đến hết ngày hôm qua. Kỳ mới bắt đầu từ hôm nay.
    /// </summary>
    public ServiceResult<InterestRunResult> ApplyAccruedInterest(string accountId, DateTime? asOf = null) => Atomic<InterestRunResult>((d, _) =>
    {
        var now = asOf ?? DateTime.Now;
        var acc = d.Accounts.FirstOrDefault(a => a.Id == accountId);
        if (acc == null) return (ServiceResult<InterestRunResult>.Missing("Không tìm thấy tài khoản."), false);
        if (!EarnsInterest(acc))
            return (ServiceResult<InterestRunResult>.Fail("Chỉ tài khoản tiết kiệm đang hoạt động mới được cộng lãi."), false);

        var posted = new List<Transaction>();
        var changed = PostDuePeriods(d, acc, now, posted);

        var start = InterestStart(d, acc);
        var accrued = AccrueInterest(d, acc, start, now.Date);
        if (accrued > 0)
        {
            acc.Balance += accrued;
            var tx = AddTransaction(d, acc, "interest", accrued, "Cộng lãi (nhận sớm)",
                acc.Name + " · " + start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + " – " +
                now.Date.AddDays(-1).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
            tx.Date = now;
            posted.Add(tx);
            changed = true;
        }
        if (now.Date > start)
        {
            acc.LastInterestDate = now.Date;
            changed = true;
        }

        if (posted.Count == 0)
            return (ServiceResult<InterestRunResult>.Fail(
                "Chưa có tiền lãi để cộng. Lãi được tính theo số dư cuối mỗi ngày, hãy quay lại vào ngày mai."), changed);

        AddNotification(d, acc.Name + " được cộng lãi " + Vnd(posted.Sum(t => t.Amount)));
        return (ServiceResult<InterestRunResult>.Success(InterestResult(d, posted)), true);
    });

    public ServiceResult<AccountDto> OpenAccount(OpenAccountRequest req) => Atomic<AccountDto>((d, _) =>
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
            Branch = "Chi nhánh Thủ Đức",
            LastInterestDate = type == "saving" ? DateTime.Today : null
        };
        d.Accounts.Add(acc);
        _faults.Hit(FaultPoints.OpenAfterAdd);

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

    /// <summary>Khóa / mở khóa tài khoản. Tài khoản bị khóa không nạp, rút, chuyển hay nhận tiền được.</summary>
    public ServiceResult<AccountDto> SetLocked(string accountId, bool locked) => Atomic<AccountDto>((d, _) =>
    {
        var acc = d.Accounts.FirstOrDefault(a => a.Id == accountId);
        if (acc == null) return (ServiceResult<AccountDto>.Missing("Không tìm thấy tài khoản."), false);
        var status = locked ? "locked" : "active";
        if (acc.Status == status) return (ServiceResult<AccountDto>.Success(ToDto(d, acc, DateTime.Now)), false);
        acc.Status = status;
        AddNotification(d, (locked ? "Đã khóa " : "Đã mở khóa ") + acc.Name + " – STK " + acc.AccountNumber);
        return (ServiceResult<AccountDto>.Success(ToDto(d, acc, DateTime.Now)), true);
    });

    public void Reset() => _repo.Reset();

    // ============================================================
    //  ROLLBACK: THAO TÁC "TẤT CẢ HOẶC KHÔNG GÌ CẢ"
    // ============================================================

    /// <summary>
    /// Mô tả thao tác đang làm dở (điền NGAY TRƯỚC khi đổi số dư).
    /// Nếu thao tác bị lỗi, thông tin này dùng để ghi một giao dịch trạng thái "failed".
    /// Đối tượng này nằm ngoài dữ liệu nên không bị mất khi dữ liệu được hoàn tác.
    /// </summary>
    private sealed class PendingOperation
    {
        public string? AccountId { get; private set; }
        public string Type { get; private set; } = "";
        public long Amount { get; private set; }
        public string Title { get; private set; } = "";
        public string Subtitle { get; private set; } = "";
        public string FailureNotice { get; private set; } = "";

        public void Describe(string accountId, string type, long amount, string title, string subtitle, string failureNotice)
        {
            AccountId = accountId;
            Type = type;
            Amount = amount;
            Title = title;
            Subtitle = subtitle;
            FailureNotice = failureNotice;
        }
    }

    /// <summary>
    /// Chạy một thao tác ghi như một khối duy nhất.
    /// - Thành công: lưu tất cả.
    /// - Lỗi giữa chừng (ngoại lệ ở bất kỳ bước nào, kể cả lúc ghi file): BankRepository khôi phục dữ liệu
    ///   về bản chụp trước thao tác; ở đây ghi thêm giao dịch "failed" + thông báo rồi trả về lỗi thân thiện.
    /// </summary>
    private ServiceResult<T> Atomic<T>(Func<BankData, PendingOperation, (ServiceResult<T> result, bool saveChanges)> work)
    {
        var op = new PendingOperation();
        try
        {
            return _repo.Write(d => work(d, op));
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Thao tác {Type} bị lỗi giữa chừng, đã hoàn tác", op.Type);
            RecordFailure(op);
            return ServiceResult<T>.Reverted(RolledBackMessage);
        }
    }

    /// <summary>
    /// Ghi lại thao tác thất bại: 1 giao dịch Status = "failed" (không làm đổi số dư) và 1 thông báo.
    /// Nếu ngay cả bước này cũng lỗi (ổ đĩa hỏng hẳn) thì bỏ qua – dữ liệu đã được khôi phục an toàn.
    /// </summary>
    private void RecordFailure(PendingOperation op)
    {
        if (op.AccountId == null) return;
        try
        {
            _repo.Write(d =>
            {
                var acc = d.Accounts.FirstOrDefault(a => a.Id == op.AccountId);
                if (acc == null) return (false, false);
                var tx = AddTransaction(d, acc, op.Type, op.Amount, op.Title, op.Subtitle + " · Lỗi hệ thống – đã hoàn tác");
                tx.Status = "failed";
                AddNotification(d, op.FailureNotice);
                return (true, true);
            });
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Không ghi được giao dịch thất bại");
        }
    }

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
    /// Kiểm tra có được trừ <paramref name="amount"/> khỏi tài khoản không (dùng cho rút và chuyển tiền).
    /// Trả về null nếu hợp lệ, ngược lại trả về thông báo lỗi.
    /// </summary>
    private static string? CheckSpend(Account acc, long amount, string verb)
    {
        var available = Available(acc);
        if (amount <= available) return null;
        return acc.Type == "payment"
            ? "Số tiền " + verb + " vượt quá hạn mức khả dụng."
            : "Số dư không đủ. Số tiền " + verb + " tối đa là " + Vnd(available) + ".";
    }

    private static string OnlyDigits(string? s) =>
        new string((s ?? "").Where(char.IsDigit).ToArray());

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

    private static bool EarnsInterest(Account a) =>
        a.Type == "saving" && a.Status == "active" && a.InterestRate > 0;

    /// <summary>Ngày bắt đầu kỳ tính lãi hiện tại.</summary>
    private static DateTime InterestStart(BankData d, Account acc)
    {
        if (acc.LastInterestDate.HasValue) return acc.LastInterestDate.Value.Date;
        var lastInterest = d.Transactions
            .Where(t => t.AccountId == acc.Id && t.Type == "interest" && t.Status == "success")
            .Select(t => (DateTime?)t.Date)
            .Max();
        if (lastInterest.HasValue) return lastInterest.Value.Date;
        return DateTime.TryParseExact(acc.OpenedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var opened)
            ? opened.Date
            : DateTime.Today;
    }

    /// <summary>
    /// Tiền lãi phát sinh trong các ngày [from, to): cộng lãi của từng ngày theo số dư cuối ngày đó.
    /// Số dư âm hoặc bằng 0 thì ngày đó không có lãi.
    /// </summary>
    private static long AccrueInterest(BankData d, Account acc, DateTime from, DateTime to)
    {
        decimal total = 0;
        var ids = new[] { acc.Id };
        var dailyRate = (decimal)acc.InterestRate / 100m / 365m;
        for (var day = from.Date; day < to.Date; day = day.AddDays(1))
        {
            var balance = Math.Max(BalanceAt(d, ids, EndOfDay(day)), 0);
            total += balance * dailyRate;
        }
        return (long)Math.Round(total, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Cộng lãi cho từng kỳ 1 tháng đã kết thúc trước hoặc đúng ngày <paramref name="now"/>.
    /// Trả về true nếu dữ liệu có thay đổi.
    /// </summary>
    private bool PostDuePeriods(BankData d, Account acc, DateTime now, List<Transaction> posted)
    {
        var changed = false;
        var start = InterestStart(d, acc);
        if (!acc.LastInterestDate.HasValue)
        {
            acc.LastInterestDate = start;
            changed = true;
        }

        var guard = 0;
        while (start.AddMonths(1) <= now.Date && guard++ < 600)
        {
            var end = start.AddMonths(1);
            var amount = AccrueInterest(d, acc, start, end);
            if (amount > 0)
            {
                acc.Balance += amount;
                var tx = AddTransaction(d, acc, "interest", amount, "Cộng lãi định kỳ",
                    acc.Name + " · " + start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) + " – " +
                    end.AddDays(-1).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));
                var postedAt = end.AddHours(8);
                tx.Date = postedAt <= now ? postedAt : now;
                posted.Add(tx);
                AddNotification(d, acc.Name + " được cộng lãi " + Vnd(amount));
            }
            acc.LastInterestDate = end;
            start = end;
            changed = true;
            _faults.Hit(FaultPoints.InterestAfterPeriod);
        }
        return changed;
    }

    /// <summary>Thông tin kỳ lãi hiện tại để hiển thị (null nếu tài khoản không hưởng lãi).</summary>
    private static InterestInfo? InterestOf(BankData d, Account acc, DateTime now)
    {
        if (!EarnsInterest(acc)) return null;
        var start = InterestStart(d, acc);
        var next = start.AddMonths(1);
        var today = now.Date;
        var accrued = AccrueInterest(d, acc, start, today < next ? today : next);
        var remainingDays = Math.Max((next - (today > start ? today : start)).Days, 0);
        var projected = accrued + (long)Math.Round(
            Math.Max(acc.Balance, 0) * (decimal)acc.InterestRate / 100m / 365m * remainingDays,
            MidpointRounding.AwayFromZero);
        var received = d.Transactions
            .Where(t => t.AccountId == acc.Id && t.Type == "interest" && t.Status == "success")
            .Sum(t => t.Amount);
        return new InterestInfo(start, next, Math.Max((today - start).Days, 0), (next - start).Days,
            accrued, projected, received);
    }

    private static InterestRunResult InterestResult(BankData d, List<Transaction> posted)
    {
        var ids = posted.Select(t => t.Id).ToHashSet();
        var list = BuildTransactions(d).Where(t => ids.Contains(t.Id)).ToList();
        return new InterestRunResult(list, posted.Sum(t => t.Amount));
    }

    private static AccountDto ToDto(BankData d, Account a, DateTime now) => new(
        a.Id, a.Type, a.Name, a.AccountNumber, a.Balance, a.Status, a.OpenedDate, a.ProductName,
        a.InterestRate, a.TermMonths, a.OverdraftLimit, a.Branch,
        Available(a), Growth(d, a, now), SavingTerm(a), InterestOf(d, a, now));

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
