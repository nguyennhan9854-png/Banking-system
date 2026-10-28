using NganHang.Data;

namespace NganHang.Tests;

/// <summary>
/// TUẦN 4 – KHÓA / MỞ KHÓA TÀI KHOẢN (Nhân). Viết TRƯỚC khi có code.
/// Quy tắc:
///   - Khóa: không nạp, rút, chuyển đi, nhận tiền; số dư và lịch sử giữ nguyên; ghi lại thời điểm khóa.
///   - Tài khoản bị khóa có số tiền khả dụng = 0 (giao diện hiện đúng là không chi được).
///   - Mở khóa: dùng lại bình thường, xóa thời điểm khóa.
///   - Khóa / mở khóa cũng là thao tác ghi nên có rollback khi lỗi.
/// </summary>
public class LockTests : IDisposable
{
    private readonly TestBank _bank = new();
    public void Dispose() => _bank.Dispose();

    private int Notifications(string text) =>
        _bank.Service.GetState().Notifications.Count(n => n.Title.Contains(text));

    [Fact(DisplayName = "LK01 Khóa tài khoản: trạng thái 'locked', ghi thời điểm khóa, số dư và lịch sử giữ nguyên, có thông báo")]
    public void Lock_SetsStatusAndTime_KeepsMoney()
    {
        var txBefore = _bank.TransactionCount();
        var before = DateTime.Now.AddSeconds(-1);

        var result = _bank.Service.SetLocked(TestBank.Payment, true);

        Assert.True(result.Ok, result.Error);
        var acc = _bank.Service.GetAccount(TestBank.Payment)!;
        Assert.Equal("locked", acc.Status);
        Assert.NotNull(acc.LockedAt);
        Assert.InRange(acc.LockedAt!.Value, before, DateTime.Now.AddSeconds(1));
        Assert.Equal(3_250_000, acc.Balance);
        Assert.Equal(txBefore, _bank.TransactionCount());
        Assert.Equal(1, Notifications("Đã khóa"));
    }

    [Fact(DisplayName = "LK02 Mở khóa: trạng thái 'active', xóa thời điểm khóa, có thông báo")]
    public void Unlock_ClearsLockTime()
    {
        _bank.Service.SetLocked(TestBank.Payment, true);

        var result = _bank.Service.SetLocked(TestBank.Payment, false);

        Assert.True(result.Ok, result.Error);
        Assert.Equal("active", result.Value!.Status);
        Assert.Null(result.Value.LockedAt);
        Assert.Equal(1, Notifications("Đã mở khóa"));
    }

    [Fact(DisplayName = "LK03 Bấm khóa 2 lần: giữ thời điểm khóa đầu tiên, chỉ 1 thông báo")]
    public void LockTwice_KeepsFirstTime_OneNotification()
    {
        var first = _bank.Service.SetLocked(TestBank.Saving, true).Value!.LockedAt;

        var second = _bank.Service.SetLocked(TestBank.Saving, true);

        Assert.True(second.Ok);
        Assert.NotNull(first);
        Assert.Equal(first, second.Value!.LockedAt);
        Assert.Equal(1, Notifications("Đã khóa"));
    }

    [Fact(DisplayName = "LK04 Mở khóa tài khoản đang hoạt động: không đổi gì, không thông báo")]
    public void UnlockActive_NoChange()
    {
        var result = _bank.Service.SetLocked(TestBank.Payment, false);

        Assert.True(result.Ok);
        Assert.Equal("active", result.Value!.Status);
        Assert.Equal(0, Notifications("mở khóa"));
    }

    [Theory(DisplayName = "LK05 Tài khoản bị khóa có số tiền khả dụng = 0 (tiết kiệm và thanh toán)")]
    [InlineData(TestBank.Saving)]
    [InlineData(TestBank.Payment)]
    public void Locked_AvailableIsZero(string id)
    {
        _bank.Service.SetLocked(id, true);

        Assert.Equal(0, _bank.Service.GetAccount(id)!.AvailableBalance);
    }

    [Fact(DisplayName = "LK06 Mở khóa xong, số tiền khả dụng trở lại = số dư + hạn mức thấu chi")]
    public void Unlock_AvailableRestored()
    {
        _bank.Service.SetLocked(TestBank.Payment, true);
        _bank.Service.SetLocked(TestBank.Payment, false);

        Assert.Equal(13_250_000, _bank.Service.GetAccount(TestBank.Payment)!.AvailableBalance);
    }

    [Fact(DisplayName = "LK07 Trạng thái khóa và thời điểm khóa được lưu vào file (khởi động lại vẫn còn)")]
    public void Lock_IsSavedToFile()
    {
        var lockedAt = _bank.Service.SetLocked(TestBank.Payment, true).Value!.LockedAt;

        var reopened = _bank.Reopen().GetAccount(TestBank.Payment)!;

        Assert.Equal("locked", reopened.Status);
        Assert.NotNull(reopened.LockedAt);
        Assert.Equal(lockedAt, reopened.LockedAt);
    }

    [Fact(DisplayName = "LK08 Lỗi ghi file khi khóa: hoàn tác, tài khoản vẫn hoạt động")]
    public void Lock_SaveFails_RolledBack()
    {
        _bank.Faults.FailAt(FaultPoints.SaveFile);

        var result = _bank.Service.SetLocked(TestBank.Payment, true);

        Assert.False(result.Ok);
        Assert.Contains("hoàn tác", result.Error);
        var acc = _bank.Service.GetAccount(TestBank.Payment)!;
        Assert.Equal("active", acc.Status);
        Assert.Null(acc.LockedAt);
        Assert.Equal("active", _bank.Reopen().GetAccount(TestBank.Payment)!.Status);
    }

    [Fact(DisplayName = "LK09 Khóa tài khoản thanh toán không ảnh hưởng tài khoản tiết kiệm")]
    public void LockOne_OtherStillWorks()
    {
        _bank.Service.SetLocked(TestBank.Payment, true);

        Assert.True(_bank.Service.Withdraw(TestBank.Saving, 1_000_000).Ok);
        Assert.True(_bank.Service.Deposit(TestBank.Saving, 500_000).Ok);
        Assert.Equal(12_080_000, _bank.Balance(TestBank.Saving));
        Assert.Equal(0, _bank.Service.GetAccount(TestBank.Payment)!.AvailableBalance);
    }

    [Fact(DisplayName = "LK10 Tài khoản bị khóa vẫn xem được lịch sử giao dịch và thống kê")]
    public void Locked_StillReadable()
    {
        var history = _bank.Service.GetTransactions(TestBank.Payment, null).Count;
        _bank.Service.SetLocked(TestBank.Payment, true);

        Assert.Equal(history, _bank.Service.GetTransactions(TestBank.Payment, null).Count);
        var stats = _bank.Service.GetStatistics(DateTime.Today.AddDays(-30), DateTime.Today, "month", TestBank.Payment);
        Assert.True(stats.Ok, stats.Error);
        Assert.Equal(3_250_000, stats.Value!.Summary.ClosingBalance);
    }
}
