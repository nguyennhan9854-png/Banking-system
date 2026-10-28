using NganHang.Models;

namespace NganHang.Tests;

/// <summary>
/// TUẦN 4 – CỘNG LÃI KHI SỔ TIẾT KIỆM BỊ KHÓA (Luân). Viết TRƯỚC khi sửa code.
///
/// Lỗi phát hiện khi rà soát trang Cộng lãi: khóa sổ tiết kiệm thì sổ biến mất khỏi trang Cộng lãi
/// và hệ thống ngừng cộng lãi; tới lúc mở khóa mới cộng dồn một lần. Người dùng khóa tạm thời
/// (ví dụ nghi lộ thông tin) không nên bị mất hay chậm tiền lãi.
///
/// Quy tắc mới:
///   - Sổ bị khóa VẪN được cộng lãi tự động đúng kỳ và vẫn hiện thông tin kỳ lãi.
///   - "Nhận lãi ngay" là thao tác của người dùng nên bị chặn khi sổ đang khóa.
///   - Tài khoản thanh toán thì khóa hay không cũng không có lãi.
/// </summary>
public class LockedInterestTests : IDisposable
{
    private readonly TestBank _bank = new();
    public void Dispose() => _bank.Dispose();

    private AccountDto OpenSaving(long amount)
    {
        var r = _bank.Service.OpenAccount(new OpenAccountRequest("saving", 12, amount, 0));
        Assert.True(r.Ok, r.Error);
        return r.Value!;
    }

    private static long Expected(long balance, double rate, int days) =>
        (long)Math.Round(balance * (decimal)rate / 100m / 365m * days, MidpointRounding.AwayFromZero);

    private int InterestCount(string id) =>
        _bank.Service.GetTransactions(id, null).Count(t => t.Type == "interest");

    [Fact(DisplayName = "LI01 Sổ tiết kiệm bị khóa vẫn được cộng lãi tự động đúng kỳ")]
    public void LockedSaving_StillGetsMonthlyInterest()
    {
        var acc = OpenSaving(10_000_000);
        _bank.Service.SetLocked(acc.Id, true);
        var end = DateTime.Today.AddMonths(1);

        var run = _bank.Service.ApplyDueInterest(end);

        var tx = Assert.Single(run.Transactions, t => t.AccountId == acc.Id);
        Assert.Equal(Expected(10_000_000, 6.3, (end - DateTime.Today).Days), tx.Amount);
        Assert.Equal(10_000_000 + tx.Amount, _bank.Balance(acc.Id));
        Assert.Equal("locked", _bank.Service.GetAccount(acc.Id)!.Status);   // vẫn khóa
    }

    [Fact(DisplayName = "LI02 Sổ bị khóa vẫn hiện thông tin kỳ lãi (để trang Cộng lãi không làm mất sổ)")]
    public void LockedSaving_StillHasInterestInfo()
    {
        _bank.Service.SetLocked(TestBank.Saving, true);

        var info = _bank.Service.GetAccount(TestBank.Saving)!.Interest;

        Assert.NotNull(info);
        Assert.Equal(DateTime.Today.AddDays(-4), info!.PeriodStart);
        Assert.Equal(226_000, info.TotalReceived);
    }

    [Fact(DisplayName = "LI03 Sổ bị khóa: 'Nhận lãi ngay' bị chặn, báo đang khóa, số dư không đổi")]
    public void LockedSaving_ApplyAccruedBlocked()
    {
        var acc = OpenSaving(10_000_000);
        _bank.Service.SetLocked(acc.Id, true);

        var result = _bank.Service.ApplyAccruedInterest(acc.Id, DateTime.Today.AddDays(10).AddHours(9));

        Assert.False(result.Ok);
        Assert.Contains("khóa", result.Error);
        Assert.Equal(10_000_000, _bank.Balance(acc.Id));
        Assert.Equal(0, InterestCount(acc.Id));
    }

    [Fact(DisplayName = "LI04 Khóa 2 tháng rồi mở khóa: mỗi kỳ cộng đúng lúc, tổng 2 kỳ, không cộng trùng")]
    public void LockThenUnlock_NoDelayNoDuplicate()
    {
        var acc = OpenSaving(10_000_000);
        var t1 = DateTime.Today.AddMonths(1);
        var t2 = t1.AddMonths(1);
        _bank.Service.SetLocked(acc.Id, true);

        _bank.Service.ApplyDueInterest(t1);
        Assert.Equal(1, InterestCount(acc.Id));            // kỳ 1 cộng ngay cả khi đang khóa

        _bank.Service.ApplyDueInterest(t2);
        _bank.Service.SetLocked(acc.Id, false);
        _bank.Service.ApplyDueInterest(t2);                  // chạy lại sau khi mở khóa

        Assert.Equal(2, InterestCount(acc.Id));
        Assert.Equal(t2, _bank.Service.GetAccount(acc.Id)!.Interest!.PeriodStart);
    }

    [Fact(DisplayName = "LI05 Tài khoản thanh toán bị khóa vẫn không có lãi")]
    public void LockedPayment_NoInterest()
    {
        _bank.Service.SetLocked(TestBank.Payment, true);

        var run = _bank.Service.ApplyDueInterest(DateTime.Today.AddMonths(3));

        Assert.DoesNotContain(run.Transactions, t => t.AccountId == TestBank.Payment);
        Assert.Null(_bank.Service.GetAccount(TestBank.Payment)!.Interest);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
    }
}
