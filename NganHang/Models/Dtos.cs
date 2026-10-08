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
    TermInfo? Term);

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

public record DepositWithdrawResult(AccountDto Account, TransactionDto Transaction, bool UsesOverdraft);

public record ErrorDto(string Error);

// ===================== DỮ LIỆU GIAO DIỆN GỬI LÊN =====================

public record AmountRequest(long Amount);

public record OpenAccountRequest(string Type, int TermMonths, long InitialAmount, long OverdraftLimit);

public record OtpRequest(bool Enabled);

// ===================== KẾT QUẢ NGHIỆP VỤ =====================

/// <summary>Kết quả của một thao tác: thành công kèm dữ liệu, hoặc thất bại kèm thông báo lỗi tiếng Việt.</summary>
public class ServiceResult<T>
{
    public bool Ok { get; private init; }
    public string? Error { get; private init; }
    public T? Value { get; private init; }
    /// <summary>true nếu lỗi do không tìm thấy (trả 404 thay vì 400)</summary>
    public bool NotFound { get; private init; }

    public static ServiceResult<T> Success(T value) => new() { Ok = true, Value = value };
    public static ServiceResult<T> Fail(string error) => new() { Ok = false, Error = error };
    public static ServiceResult<T> Missing(string error) => new() { Ok = false, Error = error, NotFound = true };
}
