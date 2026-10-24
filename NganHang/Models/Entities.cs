namespace NganHang.Models;

/// <summary>Tài khoản ngân hàng (tiết kiệm hoặc thanh toán). Tiền lưu bằng long (đơn vị: đồng).</summary>
public class Account
{
    public string Id { get; set; } = "";
    /// <summary>"saving" = tiết kiệm, "payment" = thanh toán</summary>
    public string Type { get; set; } = "saving";
    public string Name { get; set; } = "";
    public string AccountNumber { get; set; } = "";
    public long Balance { get; set; }
    public string Status { get; set; } = "active";
    /// <summary>Ngày mở dạng yyyy-MM-dd</summary>
    public string OpenedDate { get; set; } = "";
    public string ProductName { get; set; } = "";
    /// <summary>Lãi suất %/năm (chỉ tài khoản tiết kiệm)</summary>
    public double InterestRate { get; set; }
    /// <summary>Kỳ hạn (tháng) – chỉ tài khoản tiết kiệm</summary>
    public int TermMonths { get; set; }
    /// <summary>Hạn mức thấu chi – chỉ tài khoản thanh toán</summary>
    public long OverdraftLimit { get; set; }
    public string Branch { get; set; } = "";
    /// <summary>
    /// Ngày bắt đầu kỳ tính lãi hiện tại (= ngày cộng lãi gần nhất). Chỉ dùng cho tài khoản tiết kiệm.
    /// Để trống thì hệ thống tự suy ra từ giao dịch cộng lãi gần nhất hoặc ngày mở tài khoản.
    /// </summary>
    public DateTime? LastInterestDate { get; set; }
}

/// <summary>Một giao dịch. Số dư sau giao dịch KHÔNG lưu mà được tính lại khi đọc.</summary>
public class Transaction
{
    public string Id { get; set; } = "";
    public int Seq { get; set; }
    public string Code { get; set; } = "";
    public string AccountId { get; set; } = "";
    /// <summary>deposit, open, withdraw, transfer_in, transfer_out, interest, bill, shopping</summary>
    public string Type { get; set; } = "";
    /// <summary>Số tiền luôn dương; chiều tiền vào/ra suy ra từ Type</summary>
    public long Amount { get; set; }
    public string Title { get; set; } = "";
    public string Subtitle { get; set; } = "";
    public DateTime Date { get; set; }
    /// <summary>success, pending, failed</summary>
    public string Status { get; set; } = "success";
}

public class Notification
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public DateTime Date { get; set; }
    public bool Read { get; set; }
}

public class UserProfile
{
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public DateTime LastLogin { get; set; }
    public bool OtpEnabled { get; set; } = true;
}

/// <summary>Toàn bộ dữ liệu được lưu trong App_Data/bank-data.json</summary>
public class BankData
{
    public int Version { get; set; } = 1;
    public DateTime SeededAt { get; set; }
    public int Seq { get; set; }
    public UserProfile User { get; set; } = new();
    public List<Account> Accounts { get; set; } = new();
    public List<Transaction> Transactions { get; set; } = new();
    public List<Notification> Notifications { get; set; } = new();
}

/// <summary>Bảng loại giao dịch: tiền vào hay tiền ra.</summary>
public static class TransactionTypes
{
    private static readonly HashSet<string> IncomingTypes = new() { "deposit", "open", "transfer_in", "interest" };
    private static readonly HashSet<string> AllTypes = new()
    {
        "deposit", "open", "withdraw", "transfer_in", "transfer_out", "interest", "bill", "shopping"
    };

    public static bool IsIncoming(string type) => IncomingTypes.Contains(type);
    public static bool IsKnown(string type) => AllTypes.Contains(type);

    /// <summary>Danh sách mọi loại giao dịch (dùng cho thống kê).</summary>
    public static IReadOnlyCollection<string> All => AllTypes;
}
