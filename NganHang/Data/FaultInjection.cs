namespace NganHang.Data;

/// <summary>
/// Tên các "điểm lỗi" nằm GIỮA một thao tác ghi dữ liệu.
/// Unit test dùng chúng để giả lập sự cố (mất điện, lỗi ổ đĩa...) đúng lúc tiền đã bị trừ
/// nhưng chưa kịp cộng, rồi kiểm tra hệ thống có hoàn tác (rollback) đúng không.
/// </summary>
public static class FaultPoints
{
    /// <summary>Nạp tiền: đã cộng số dư, chưa lưu giao dịch.</summary>
    public const string DepositAfterCredit = "deposit:after-credit";

    /// <summary>Rút tiền: đã trừ số dư, chưa lưu giao dịch.</summary>
    public const string WithdrawAfterDebit = "withdraw:after-debit";

    /// <summary>Chuyển tiền: đã trừ tài khoản nguồn, CHƯA cộng tài khoản nhận.</summary>
    public const string TransferAfterDebit = "transfer:after-debit";

    /// <summary>Chuyển tiền: đã trừ nguồn và cộng đích, chưa ghi thông báo / chưa lưu file.</summary>
    public const string TransferAfterCredit = "transfer:after-credit";

    /// <summary>Cộng lãi: vừa cộng xong một kỳ, còn các kỳ sau chưa cộng.</summary>
    public const string InterestAfterPeriod = "interest:after-period";

    /// <summary>Mở tài khoản: đã thêm tài khoản, chưa ghi tiền nạp ban đầu.</summary>
    public const string OpenAfterAdd = "open:after-add";

    /// <summary>Ghi file dữ liệu xuống ổ đĩa (lỗi ổ đầy, file bị khóa...).</summary>
    public const string SaveFile = "repository:save-file";
}

/// <summary>
/// Chỗ "cắm" lỗi giả lập. Khi chạy thật dùng <see cref="NoFaults"/> (không làm gì).
/// Unit test thay bằng bản giả để ném ngoại lệ tại một điểm lỗi.
/// </summary>
public interface IFaultInjector
{
    /// <summary>Được gọi khi thao tác đi qua <paramref name="point"/>; ném ngoại lệ nếu muốn giả lập lỗi.</summary>
    void Hit(string point);
}

/// <summary>Bản dùng khi chạy thật: không bao giờ gây lỗi.</summary>
public sealed class NoFaults : IFaultInjector
{
    public static readonly NoFaults Instance = new();
    public void Hit(string point) { }
}
