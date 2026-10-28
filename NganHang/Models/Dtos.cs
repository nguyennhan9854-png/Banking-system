namespace NganHang.Models;

// ===================== DỮ LIỆU TRẢ VỀ CHO GIAO DIỆN =====================

/// <summary>Thông tin kỳ hạn hiện tại của sổ tiết kiệm (tự tái tục khi đến hạn).</summary>
public record TermInfo(DateTime Start, DateTime Maturity, int Days, long EstimatedInterest);

/// <summary>Tài khoản kèm các số liệu server đã tính sẵn.</summary>
public record AccountDto(
    string Id,
    string Type,
    string Name,
    string AccountNumber,
    long Balance,
    string Status,
    string OpenedDate,
    string ProductName,
    double InterestRate,
    int TermMonths,
    long OverdraftLimit,
    string Branch,
    long AvailableBalance,
    double? GrowthPercent,
    TermInfo? Term,
    InterestInfo? Interest,
    DateTime? LockedAt = null);

/// <summary>
/// Thông tin cộng lãi hằng tháng của tài khoản tiết kiệm.
/// Lãi tính trên số dư cuối mỗi ngày: lãi ngày = số dư × lãi suất / 100 / 365.
/// </summary>
public record InterestInfo(
    DateTime PeriodStart,
    DateTime NextInterestDate,
    int DaysElapsed,
    int DaysInPeriod,
    long AccruedInterest,
    long ProjectedInterest,
    long TotalReceived);

/// <summary>
/// Kết quả một lần cộng lãi: các giao dịch "Cộng lãi" vừa tạo và tổng tiền lãi.
/// Error khác null nghĩa là đợt cộng lãi bị lỗi giữa chừng và đã được hoàn tác.
/// </summary>
public record InterestRunResult(List<TransactionDto> Transactions, long TotalAmount, string? Error = null);

/// <summary>Giao dịch kèm số dư sau giao dịch (tính riêng cho từng tài khoản).</summary>
public record TransactionDto(
    string Id,
    int Seq,
    string Code,
    string AccountId,
    string Type,
    long Amount,
    string Title,
    string Subtitle,
    DateTime Date,
    string Status,
    long BalanceAfter,
    long SignedAmount);

/// <summary>Toàn bộ trạng thái để giao diện hiển thị (GET /api/state).</summary>
public record StateDto(
    UserProfile User,
    List<AccountDto> Accounts,
    List<TransactionDto> Transactions,
    List<Notification> Notifications,
    Dictionary<string, double> SavingRates);

/// <summary>Một điểm trên biểu đồ biến động số dư.</summary>
public record BalancePointDto(DateTime Date, long Value, string Label);

// ===================== THỐNG KÊ THEO KỲ =====================

/// <summary>Số lượng và tổng tiền của một loại giao dịch trong kỳ.</summary>
public record TypeStatDto(int Count, long Amount);

/// <summary>
/// Số liệu một kỳ (tháng / quý / ngày). Luôn đúng: ClosingBalance = OpeningBalance + TotalIn - TotalOut.
/// Chỉ giao dịch THÀNH CÔNG mới được tính tiền; giao dịch thất bại chỉ được đếm.
/// </summary>
public record PeriodStatDto(
    string Label,
    DateTime Start,
    DateTime End,
    long OpeningBalance,
    long ClosingBalance,
    long TotalIn,
    long TotalOut,
    long Interest,
    int Count);

/// <summary>Số liệu của từng tài khoản trong cả khoảng thời gian.</summary>
public record AccountStatDto(
    string AccountId,
    string Name,
    string AccountNumber,
    string Type,
    long OpeningBalance,
    long ClosingBalance,
    long TotalIn,
    long TotalOut,
    long Interest,
    int Count);

/// <summary>Tổng hợp cả khoảng thời gian.</summary>
public record StatisticsSummaryDto(
    long OpeningBalance,
    long ClosingBalance,
    long TotalIn,
    long TotalOut,
    long Net,
    long InterestReceived,
    int TransactionCount,
    int FailedCount,
    Dictionary<string, TypeStatDto> ByType);

/// <summary>Kết quả GET /api/statistics.</summary>
public record StatisticsDto(
    DateTime From,
    DateTime To,
    string GroupBy,
    string? AccountId,
    StatisticsSummaryDto Summary,
    List<PeriodStatDto> Periods,
    List<AccountStatDto> Accounts);

public record DepositWithdrawResult(AccountDto Account, TransactionDto Transaction, bool UsesOverdraft);

/// <summary>Kết quả chuyển tiền: tài khoản nguồn/đích sau khi chuyển và 2 giao dịch được tạo.</summary>
public record TransferResult(
    AccountDto From,
    AccountDto? To,
    TransactionDto OutTransaction,
    TransactionDto? InTransaction,
    bool UsesOverdraft);

public record ErrorDto(string Error);

// ===================== DỮ LIỆU GIAO DIỆN GỬI LÊN =====================

public record AmountRequest(long Amount);

public record OpenAccountRequest(string Type, int TermMonths, long InitialAmount, long OverdraftLimit);

public record OtpRequest(bool Enabled);

public record LockRequest(bool Locked);

/// <summary>
/// Yêu cầu chuyển tiền. Chuyển nội bộ: điền ToAccountId.
/// Chuyển ra ngoài: điền ToAccountNumber, ToName, ToBank.
/// </summary>
public record TransferRequest(
    string FromAccountId,
    string? ToAccountId,
    string? ToAccountNumber,
    string? ToName,
    string? ToBank,
    long Amount,
    string? Note);

// ===================== KẾT QUẢ NGHIỆP VỤ =====================

/// <summary>Kết quả của một thao tác: thành công kèm dữ liệu, hoặc thất bại kèm thông báo lỗi tiếng Việt.</summary>
public class ServiceResult<T>
{
    public bool Ok { get; private init; }
    public string? Error { get; private init; }
    public T? Value { get; private init; }
    /// <summary>true nếu lỗi do không tìm thấy (trả 404 thay vì 400)</summary>
    public bool NotFound { get; private init; }
    /// <summary>true nếu thao tác bị lỗi giữa chừng và đã được hoàn tác (trả 409 thay vì 400)</summary>
    public bool RolledBack { get; private init; }

    public static ServiceResult<T> Success(T value) => new() { Ok = true, Value = value };
    public static ServiceResult<T> Fail(string error) => new() { Ok = false, Error = error };
    public static ServiceResult<T> Missing(string error) => new() { Ok = false, Error = error, NotFound = true };
    public static ServiceResult<T> Reverted(string error) => new() { Ok = false, Error = error, RolledBack = true };
}
