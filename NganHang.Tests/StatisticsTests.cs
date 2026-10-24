using NganHang.Data;
using NganHang.Models;
using NganHang.Services;

namespace NganHang.Tests;

/// <summary>
/// Kiểm thử BÁO CÁO THỐNG KÊ THEO KỲ (Luân – tuần 3):
/// cuối kỳ = đầu kỳ + tiền vào - tiền ra, lãi từng kỳ khớp lịch sử, chia kỳ tháng/quý/ngày đúng,
/// giao dịch thất bại không tính tiền, kiểm tra dữ liệu đầu vào.
/// </summary>
public class StatisticsTests : IDisposable
{
    private readonly TestBank _bank = new();
    public void Dispose() => _bank.Dispose();

    private static DateTime Today => DateTime.Today;

    private StatisticsDto Stats(DateTime from, DateTime to, string groupBy = "month", string? accountId = null)
    {
        var result = _bank.Service.GetStatistics(from, to, groupBy, accountId);
        Assert.True(result.Ok, result.Error);
        return result.Value!;
    }

    /// <summary>6 tháng gần nhất: chứa toàn bộ 31 giao dịch của dữ liệu mẫu.</summary>
    private StatisticsDto SixMonths(string groupBy = "month", string? accountId = null) =>
        Stats(Today.AddDays(-180), Today, groupBy, accountId);

    // ================= CÔNG THỨC CÂN ĐỐI =================

    [Theory(DisplayName = "ST01 Mọi kỳ: số dư cuối kỳ = đầu kỳ + tiền vào - tiền ra (tháng, quý, ngày)")]
    [InlineData("month")]
    [InlineData("quarter")]
    [InlineData("day")]
    public void EveryPeriod_ClosingEqualsOpeningPlusInMinusOut(string groupBy)
    {
        var stats = groupBy == "day" ? Stats(Today.AddDays(-60), Today, "day") : SixMonths(groupBy);

        Assert.NotEmpty(stats.Periods);
        foreach (var p in stats.Periods)
        {
            Assert.Equal(p.OpeningBalance + p.TotalIn - p.TotalOut, p.ClosingBalance);
        }
        Assert.Equal(stats.Summary.OpeningBalance + stats.Summary.TotalIn - stats.Summary.TotalOut, stats.Summary.ClosingBalance);
    }

    [Fact(DisplayName = "ST02 Từng tài khoản: cuối kỳ = đầu kỳ + vào - ra; cuối kỳ hôm nay = số dư hiện tại")]
    public void EveryAccount_BalancesAndCurrentBalance()
    {
        var stats = SixMonths();

        Assert.Equal(2, stats.Accounts.Count);
        foreach (var a in stats.Accounts)
        {
            Assert.Equal(a.OpeningBalance + a.TotalIn - a.TotalOut, a.ClosingBalance);
            Assert.Equal(_bank.Balance(a.AccountId), a.ClosingBalance);
        }
        Assert.Equal(12_580_000 + 3_250_000, stats.Summary.ClosingBalance);
    }

    [Fact(DisplayName = "ST03 Các kỳ nối tiếp nhau: cuối kỳ trước = đầu kỳ sau; tổng các kỳ = tổng cả khoảng")]
    public void Periods_AreContiguousAndSumToSummary()
    {
        var stats = SixMonths();

        for (var i = 0; i < stats.Periods.Count - 1; i++)
        {
            Assert.Equal(stats.Periods[i].ClosingBalance, stats.Periods[i + 1].OpeningBalance);
            Assert.Equal(stats.Periods[i].End.AddDays(1), stats.Periods[i + 1].Start);
        }
        Assert.Equal(stats.Summary.OpeningBalance, stats.Periods[0].OpeningBalance);
        Assert.Equal(stats.Summary.ClosingBalance, stats.Periods[^1].ClosingBalance);
        Assert.Equal(stats.Summary.TotalIn, stats.Periods.Sum(p => p.TotalIn));
        Assert.Equal(stats.Summary.TotalOut, stats.Periods.Sum(p => p.TotalOut));
        Assert.Equal(stats.Summary.TransactionCount, stats.Periods.Sum(p => p.Count));
    }

    // ================= TIỀN LÃI =================

    [Fact(DisplayName = "ST04 Tiền lãi từng tháng khớp đúng các giao dịch cộng lãi trong lịch sử")]
    public void InterestPerPeriod_MatchesHistory()
    {
        var stats = SixMonths(accountId: TestBank.Saving);
        var history = _bank.Service.GetTransactions(TestBank.Saving, null).Where(t => t.Type == "interest").ToList();

        foreach (var p in stats.Periods)
        {
            var expected = history.Where(t => t.Date >= p.Start && t.Date < p.End.AddDays(1)).Sum(t => t.Amount);
            Assert.Equal(expected, p.Interest);
        }
        Assert.Equal(history.Sum(t => t.Amount), stats.Summary.InterestReceived);
    }

    [Fact(DisplayName = "ST05 Dữ liệu mẫu 6 tháng: 31 giao dịch, 6 lần cộng lãi tổng 226.000 đ")]
    public void SeedData_KnownTotals()
    {
        var stats = SixMonths();

        Assert.Equal(31, stats.Summary.TransactionCount);
        Assert.Equal(226_000, stats.Summary.InterestReceived);
        Assert.Equal(6, stats.Summary.ByType["interest"].Count);
        Assert.Equal(226_000, stats.Summary.ByType["interest"].Amount);
        Assert.Equal(6, stats.Summary.ByType["deposit"].Count);
        Assert.Equal(0, stats.Summary.ByType["open"].Count);           // loại chưa phát sinh vẫn có, bằng 0
        Assert.Equal(stats.Summary.TotalIn - stats.Summary.TotalOut, stats.Summary.Net);
    }

    [Fact(DisplayName = "ST06 Mở sổ tiết kiệm hôm nay: kỳ hôm nay có đầu kỳ 0, tiền vào 10 triệu, chưa có lãi")]
    public void NewSavingToday_DayStatistics()
    {
        var acc = _bank.Service.OpenAccount(new OpenAccountRequest("saving", 12, 10_000_000, 0)).Value!;
        var run = _bank.Service.ApplyDueInterest(DateTime.Now);           // chưa đến hạn – không có gì
        Assert.DoesNotContain(run.Transactions, t => t.AccountId == acc.Id);

        var accrued = _bank.Service.ApplyAccruedInterest(acc.Id, DateTime.Now);
        // mở hôm nay nên chưa có lãi để nhận
        Assert.False(accrued.Ok);

        var stats = Stats(Today, Today, "day", acc.Id);
        var only = Assert.Single(stats.Periods);
        Assert.Equal(10_000_000, only.TotalIn);
        Assert.Equal(0, only.Interest);
        Assert.Equal(0, only.OpeningBalance);
        Assert.Equal(10_000_000, only.ClosingBalance);
    }

    // ================= CHIA KỲ =================

    [Fact(DisplayName = "ST07 Chia theo tháng có tháng 2 năm nhuận: 15/01–31/01, 01/02–29/02, 01/03–10/03")]
    public void SplitByMonth_LeapFebruary()
    {
        var periods = BankService.SplitPeriods(new DateTime(2024, 1, 15), new DateTime(2024, 3, 10), "month");

        Assert.Equal(3, periods.Count);
        Assert.Equal(("T1/2024", new DateTime(2024, 1, 15), new DateTime(2024, 1, 31)), periods[0]);
        Assert.Equal(("T2/2024", new DateTime(2024, 2, 1), new DateTime(2024, 2, 29)), periods[1]);
        Assert.Equal(("T3/2024", new DateTime(2024, 3, 1), new DateTime(2024, 3, 10)), periods[2]);
    }

    [Fact(DisplayName = "ST08 Chia theo quý qua năm mới: Q4/2023 (từ 20/11), Q1/2024, Q2/2024 (đến 02/04)")]
    public void SplitByQuarter_AcrossYear()
    {
        var periods = BankService.SplitPeriods(new DateTime(2023, 11, 20), new DateTime(2024, 4, 2), "quarter");

        Assert.Equal(new[] { "Q4/2023", "Q1/2024", "Q2/2024" }, periods.Select(p => p.Label).ToArray());
        Assert.Equal(new DateTime(2023, 11, 20), periods[0].Start);
        Assert.Equal(new DateTime(2023, 12, 31), periods[0].End);
        Assert.Equal(new DateTime(2024, 3, 31), periods[1].End);
        Assert.Equal(new DateTime(2024, 4, 2), periods[2].End);
    }

    [Fact(DisplayName = "ST09 Theo quý và theo tháng cho cùng tổng tiền vào/ra/lãi")]
    public void QuarterAndMonth_SameTotals()
    {
        var month = SixMonths("month");
        var quarter = SixMonths("quarter");

        Assert.Equal(month.Summary.TotalIn, quarter.Summary.TotalIn);
        Assert.Equal(month.Periods.Sum(p => p.TotalOut), quarter.Periods.Sum(p => p.TotalOut));
        Assert.Equal(month.Periods.Sum(p => p.Interest), quarter.Periods.Sum(p => p.Interest));
        Assert.True(quarter.Periods.Count < month.Periods.Count);
    }

    [Fact(DisplayName = "ST10 Một ngày: giao dịch nạp hôm nay nằm đúng kỳ hôm nay")]
    public void DayGrouping_TodayDepositInTodayPeriod()
    {
        Assert.True(_bank.Service.Deposit(TestBank.Payment, 700_000).Ok);

        var stats = Stats(Today.AddDays(-2), Today, "day", TestBank.Payment);

        Assert.Equal(3, stats.Periods.Count);
        var today = stats.Periods[^1];
        Assert.Equal(Today, today.Start);
        Assert.True(today.TotalIn >= 700_000);
        Assert.Equal(_bank.Balance(TestBank.Payment), today.ClosingBalance);
    }

    // ================= GIAO DỊCH ĐẶC BIỆT =================

    [Fact(DisplayName = "ST11 Giao dịch thất bại (đã hoàn tác) được đếm riêng, không tính vào tiền vào/ra")]
    public void FailedTransactions_CountedButNotSummed()
    {
        var before = Stats(Today, Today, "day");
        _bank.Faults.FailAt(FaultPoints.TransferAfterDebit);
        _bank.Service.Transfer(new TransferRequest(TestBank.Payment, TestBank.Saving, null, null, null, 1_000_000, null));

        var after = Stats(Today, Today, "day");

        Assert.Equal(before.Summary.FailedCount + 1, after.Summary.FailedCount);
        Assert.Equal(before.Summary.TotalOut, after.Summary.TotalOut);
        Assert.Equal(before.Summary.TotalIn, after.Summary.TotalIn);
        Assert.Equal(before.Summary.TransactionCount, after.Summary.TransactionCount);
    }

    [Fact(DisplayName = "ST12 Chuyển nội bộ khi xem tất cả TK: vào và ra cùng tăng, chênh lệch không đổi")]
    public void InternalTransfer_AllAccounts_NetUnchanged()
    {
        var before = Stats(Today, Today, "day");
        _bank.Service.Transfer(new TransferRequest(TestBank.Payment, TestBank.Saving, null, null, null, 2_000_000, null));

        var after = Stats(Today, Today, "day");

        Assert.Equal(before.Summary.TotalIn + 2_000_000, after.Summary.TotalIn);
        Assert.Equal(before.Summary.TotalOut + 2_000_000, after.Summary.TotalOut);
        Assert.Equal(before.Summary.Net, after.Summary.Net);
        Assert.Equal(before.Summary.ClosingBalance, after.Summary.ClosingBalance);
    }

    [Fact(DisplayName = "ST13 Lọc 1 tài khoản: chỉ tính giao dịch của tài khoản đó")]
    public void SingleAccount_OnlyItsTransactions()
    {
        var saving = SixMonths(accountId: TestBank.Saving);
        var payment = SixMonths(accountId: TestBank.Payment);
        var all = SixMonths();

        Assert.Single(saving.Accounts);
        Assert.Equal(all.Summary.TransactionCount, saving.Summary.TransactionCount + payment.Summary.TransactionCount);
        Assert.Equal(all.Summary.TotalIn, saving.Summary.TotalIn + payment.Summary.TotalIn);
        Assert.Equal(0, payment.Summary.InterestReceived);
    }

    // ================= KIỂM TRA ĐẦU VÀO =================

    [Fact(DisplayName = "ST14 Ngày kết thúc trong tương lai được cắt về hôm nay")]
    public void FutureEnd_IsClampedToToday()
    {
        var stats = Stats(Today.AddDays(-5), Today.AddDays(30));

        Assert.Equal(Today, stats.To);
        Assert.Equal(Today, stats.Periods[^1].End);
    }

    [Fact(DisplayName = "ST15 Đầu vào sai bị từ chối: từ > đến, kiểu kỳ lạ, quá 3 năm, theo ngày quá 92 ngày, từ ngày tương lai")]
    public void InvalidInput_IsRejected()
    {
        Assert.False(_bank.Service.GetStatistics(Today, Today.AddDays(-1), "month", null).Ok);
        Assert.False(_bank.Service.GetStatistics(Today.AddDays(-10), Today, "week", null).Ok);
        Assert.False(_bank.Service.GetStatistics(Today.AddYears(-4), Today, "month", null).Ok);
        Assert.False(_bank.Service.GetStatistics(Today.AddDays(-100), Today, "day", null).Ok);
        Assert.False(_bank.Service.GetStatistics(Today.AddDays(3), Today.AddDays(5), "month", null).Ok);
        Assert.False(_bank.Service.GetStatistics(default, default, "month", null).Ok);
    }

    [Fact(DisplayName = "ST16 Tài khoản không tồn tại trả về NotFound")]
    public void UnknownAccount_NotFound()
    {
        var result = _bank.Service.GetStatistics(Today.AddDays(-10), Today, "month", "acc-khong-co");

        Assert.False(result.Ok);
        Assert.True(result.NotFound);
    }
}
