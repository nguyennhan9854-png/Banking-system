using Microsoft.Extensions.Logging.Abstractions;
using NganHang.Data;
using NganHang.Models;
using NganHang.Services;

namespace NganHang.Tests;

/// <summary>
/// Kiểm thử CỘNG LÃI tiết kiệm:
/// đúng công thức, tự động theo tháng, không cộng trùng, cộng dồn nhiều kỳ, số dư 0 không có lãi,
/// tài khoản thanh toán không được cộng lãi, lịch sử và lưu file.
/// </summary>
public class InterestTests : IDisposable
{
    private readonly TestBank _bank = new();
    public void Dispose() => _bank.Dispose();

    /// <summary>Mở 1 sổ tiết kiệm mới hôm nay (kỳ lãi bắt đầu từ hôm nay).</summary>
    private AccountDto OpenSaving(long amount, int termMonths = 12)
    {
        var result = _bank.Service.OpenAccount(new OpenAccountRequest("saving", termMonths, amount, 0));
        Assert.True(result.Ok, result.Error);
        return result.Value!;
    }

    /// <summary>Lãi mong đợi khi số dư không đổi trong cả kỳ.</summary>
    private static long ExpectedInterest(long balance, double rate, int days) =>
        (long)Math.Round(balance * (decimal)rate / 100m / 365m * days, MidpointRounding.AwayFromZero);

    private int InterestCount(string accountId) =>
        _bank.Service.GetTransactions(accountId, null).Count(t => t.Type == "interest");

    // ---------------- Công thức ----------------

    [Fact(DisplayName = "Đủ 1 tháng: cộng lãi đúng công thức số dư × lãi suất × số ngày / 365")]
    public void Monthly_Interest_FollowsFormula()
    {
        var acc = OpenSaving(10_000_000);                     // kỳ hạn 12 tháng: 6,3%/năm
        var oneMonth = DateTime.Today.AddMonths(1);
        var days = (oneMonth - DateTime.Today).Days;

        var result = _bank.Service.ApplyDueInterest(oneMonth);

        var expected = ExpectedInterest(10_000_000, 6.3, days);
        var tx = Assert.Single(result.Transactions, t => t.AccountId == acc.Id);
        Assert.Equal(expected, tx.Amount);
        Assert.Equal("interest", tx.Type);
        Assert.Equal(10_000_000 + expected, _bank.Balance(acc.Id));
        Assert.Equal(10_000_000 + expected, tx.BalanceAfter);
    }

    [Fact(DisplayName = "Nạp thêm trong ngày đầu kỳ: lãi tính trên số dư mới của từng ngày")]
    public void Interest_UsesDailyBalance()
    {
        var acc = OpenSaving(10_000_000);
        var today = DateTime.Today;
        var end = today.AddMonths(1);
        var days = (end - today).Days;

        _bank.Service.Deposit(acc.Id, 5_000_000);               // nạp ngay hôm nay -> mỗi ngày trong kỳ đều có 15.000.000
        var result = _bank.Service.ApplyDueInterest(end);

        var tx = Assert.Single(result.Transactions, t => t.AccountId == acc.Id);
        Assert.Equal(ExpectedInterest(15_000_000, 6.3, days), tx.Amount);
    }

    [Theory(DisplayName = "Lãi suất theo kỳ hạn được dùng đúng khi cộng lãi")]
    [InlineData(1, 4.5)]
    [InlineData(3, 5.2)]
    [InlineData(6, 5.8)]
    [InlineData(12, 6.3)]
    public void Interest_UsesRateOfTerm(int termMonths, double rate)
    {
        var acc = OpenSaving(20_000_000, termMonths);
        var end = DateTime.Today.AddMonths(1);

        var result = _bank.Service.ApplyDueInterest(end);

        var tx = Assert.Single(result.Transactions, t => t.AccountId == acc.Id);
        Assert.Equal(ExpectedInterest(20_000_000, rate, (end - DateTime.Today).Days), tx.Amount);
    }

    // ---------------- Tự động theo kỳ, không cộng trùng ----------------

    [Fact(DisplayName = "Chưa đủ 1 tháng thì chưa cộng lãi")]
    public void BeforeMonthEnd_NoInterest()
    {
        var acc = OpenSaving(10_000_000);

        var result = _bank.Service.ApplyDueInterest(DateTime.Today.AddDays(20));

        Assert.DoesNotContain(result.Transactions, t => t.AccountId == acc.Id);
        Assert.Equal(10_000_000, _bank.Balance(acc.Id));
    }

    [Fact(DisplayName = "Chạy cộng lãi nhiều lần trong cùng kỳ không bị cộng trùng")]
    public void RunTwice_SamePeriod_NoDuplicate()
    {
        var acc = OpenSaving(10_000_000);
        var end = DateTime.Today.AddMonths(1);

        _bank.Service.ApplyDueInterest(end);
        var balanceAfterFirst = _bank.Balance(acc.Id);
        var second = _bank.Service.ApplyDueInterest(end);
        var third = _bank.Service.ApplyDueInterest(end.AddDays(5));

        Assert.Empty(second.Transactions);
        Assert.Empty(third.Transactions);
        Assert.Equal(balanceAfterFirst, _bank.Balance(acc.Id));
        Assert.Equal(1, InterestCount(acc.Id));
    }

    [Fact(DisplayName = "Quá 3 tháng chưa chạy: cộng đủ 3 kỳ, kỳ sau tính cả lãi kỳ trước (lãi kép)")]
    public void ThreeMonths_PostsThreePeriods_Compounding()
    {
        var acc = OpenSaving(10_000_000);
        var t0 = DateTime.Today;
        var t1 = t0.AddMonths(1);
        var t2 = t1.AddMonths(1);
        var t3 = t2.AddMonths(1);

        var result = _bank.Service.ApplyDueInterest(t3);

        var mine = result.Transactions.Where(t => t.AccountId == acc.Id).OrderBy(t => t.Date).ToList();
        Assert.Equal(3, mine.Count);
        var i1 = ExpectedInterest(10_000_000, 6.3, (t1 - t0).Days);
        var i2 = ExpectedInterest(10_000_000 + i1, 6.3, (t2 - t1).Days);
        var i3 = ExpectedInterest(10_000_000 + i1 + i2, 6.3, (t3 - t2).Days);
        Assert.Equal(new[] { i1, i2, i3 }, mine.Select(t => t.Amount).ToArray());
        Assert.Equal(10_000_000 + i1 + i2 + i3, _bank.Balance(acc.Id));
    }

    [Fact(DisplayName = "Sau khi cộng lãi, kỳ tiếp theo bắt đầu từ ngày cộng lãi")]
    public void AfterPosting_NextPeriodStarts()
    {
        var acc = OpenSaving(10_000_000);
        var end = DateTime.Today.AddMonths(1);

        _bank.Service.ApplyDueInterest(end);

        var info = _bank.Service.GetAccount(acc.Id)!.Interest!;
        Assert.Equal(end, info.PeriodStart);
        Assert.Equal(end.AddMonths(1), info.NextInterestDate);
    }

    // ---------------- Trường hợp không có lãi ----------------

    [Fact(DisplayName = "Số dư bằng 0 cả kỳ thì không có lãi, không tạo giao dịch")]
    public void ZeroBalance_NoInterest()
    {
        var acc = OpenSaving(1_000_000);
        Assert.True(_bank.Service.Withdraw(acc.Id, 1_000_000).Ok);

        var result = _bank.Service.ApplyDueInterest(DateTime.Today.AddMonths(1));

        Assert.DoesNotContain(result.Transactions, t => t.AccountId == acc.Id);
        Assert.Equal(0, _bank.Balance(acc.Id));
    }

    [Fact(DisplayName = "Tài khoản thanh toán không được cộng lãi")]
    public void PaymentAccount_NeverEarnsInterest()
    {
        var result = _bank.Service.ApplyDueInterest(DateTime.Today.AddMonths(6));

        Assert.DoesNotContain(result.Transactions, t => t.AccountId == TestBank.Payment);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
        Assert.Null(_bank.Service.GetAccount(TestBank.Payment)!.Interest);

        var apply = _bank.Service.ApplyAccruedInterest(TestBank.Payment);
        Assert.False(apply.Ok);
    }

    // ---------------- Dữ liệu mẫu + nhận lãi ngay ----------------

    [Fact(DisplayName = "Dữ liệu mẫu: tài khoản tiết kiệm vừa cộng lãi 4 ngày trước nên hôm nay chưa cộng thêm")]
    public void SeedSaving_NotDueToday()
    {
        var result = _bank.Service.ApplyDueInterest(DateTime.Now);

        Assert.Empty(result.Transactions);
        Assert.Equal(12_580_000, _bank.Balance(TestBank.Saving));
        var info = _bank.Service.GetAccount(TestBank.Saving)!.Interest!;
        Assert.Equal(DateTime.Today.AddDays(-4), info.PeriodStart);
        Assert.Equal(4, info.DaysElapsed);
    }

    [Fact(DisplayName = "Nhận lãi ngay: cộng đúng phần lãi tạm tính và bắt đầu kỳ mới")]
    public void ApplyAccrued_PostsAccruedAndRestartsPeriod()
    {
        var acc = OpenSaving(10_000_000);
        var later = DateTime.Today.AddDays(10).AddHours(9);

        var result = _bank.Service.ApplyAccruedInterest(acc.Id, later);

        Assert.True(result.Ok, result.Error);
        var tx = Assert.Single(result.Value!.Transactions);
        Assert.Equal(ExpectedInterest(10_000_000, 6.3, 10), tx.Amount);
        Assert.Equal(later.Date, _bank.Service.GetAccount(acc.Id)!.Interest!.PeriodStart);
    }

    [Fact(DisplayName = "Nhận lãi ngay 2 lần trong cùng ngày: lần 2 bị từ chối, không cộng trùng")]
    public void ApplyAccrued_TwiceSameDay_SecondRejected()
    {
        var acc = OpenSaving(10_000_000);
        var later = DateTime.Today.AddDays(10).AddHours(9);

        Assert.True(_bank.Service.ApplyAccruedInterest(acc.Id, later).Ok);
        var balance = _bank.Balance(acc.Id);
        var again = _bank.Service.ApplyAccruedInterest(acc.Id, later.AddHours(2));

        Assert.False(again.Ok);
        Assert.Equal(balance, _bank.Balance(acc.Id));
        Assert.Equal(1, InterestCount(acc.Id));
    }

    // ---------------- Lịch sử ----------------

    [Fact(DisplayName = "Giao dịch cộng lãi được lưu vào file và số dư sau giao dịch khớp")]
    public void InterestTransactions_AreSavedAndConsistent()
    {
        var acc = OpenSaving(10_000_000);
        _bank.Service.ApplyDueInterest(DateTime.Today.AddMonths(2));

        var reopened = new BankService(new BankRepository(_bank.FilePath, NullLogger<BankRepository>.Instance));
        var list = reopened.GetTransactions(acc.Id, null);

        Assert.Equal(2, list.Count(t => t.Type == "interest"));
        Assert.Equal(_bank.Balance(acc.Id), reopened.GetAccount(acc.Id)!.Balance);
        Assert.Equal(reopened.GetAccount(acc.Id)!.Balance, list[0].BalanceAfter);
        for (var i = 0; i < list.Count - 1; i++)
        {
            Assert.Equal(list[i + 1].BalanceAfter, list[i].BalanceAfter - list[i].SignedAmount);
        }
    }
}
