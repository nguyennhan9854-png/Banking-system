using System.Globalization;
using NganHang.Models;

namespace NganHang.Tests;

/// <summary>
/// TEST BIÊN CỘNG LÃI (Luân – tuần 3): tháng 28/29/30/31 ngày, năm nhuận, mở sổ ngày 31,
/// số dư về 0 giữa kỳ rồi nạp lại, rút hết trong kỳ, server tắt cả năm (qua năm mới).
/// Dùng các ngày CỐ ĐỊNH trong quá khứ (2023–2024) nên kết quả không phụ thuộc ngày chạy test.
/// </summary>
public class InterestEdgeTests : IDisposable
{
    private const double Rate = 6.0;
    private readonly TestBank _bank = new();
    public void Dispose() => _bank.Dispose();

    private static long Expected(long balance, double rate, int days) =>
        (long)Math.Round(balance * (decimal)rate / 100m / 365m * days, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Thêm thẳng vào dữ liệu một sổ tiết kiệm mở ngày <paramref name="start"/> kèm lịch sử giao dịch ở các ngày cho trước.
    /// Số dư thay đổi qua Credit/Debit giống nghiệp vụ thật.
    /// </summary>
    private string AddSaving(DateTime start, params (DateTime Date, string Type, long Amount)[] history)
    {
        var acc = new SavingAccount
        {
            Id = "acc-edge-" + Guid.NewGuid().ToString("N")[..8],
            Name = "Sổ kiểm thử",
            AccountNumber = "9999 0000 " + Random.Shared.Next(1000, 9999).ToString(CultureInfo.InvariantCulture),
            Status = "active",
            OpenedDate = start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ProductName = "Tiết kiệm có kỳ hạn",
            InterestRate = Rate,
            TermMonths = 12,
            Branch = "Kiểm thử",
            LastInterestDate = start.Date
        };
        _bank.Repository.Write(d =>
        {
            d.Accounts.Add(acc);
            foreach (var h in history)
            {
                if (TransactionTypes.IsIncoming(h.Type)) acc.Credit(h.Amount);
                else acc.Debit(h.Amount);
                d.Seq = Math.Max(d.Seq, d.Transactions.Count) + 1;
                d.Transactions.Add(new Transaction
                {
                    Id = "tx-edge-" + d.Seq, Seq = d.Seq, Code = "GD" + d.Seq, AccountId = acc.Id,
                    Type = h.Type, Amount = h.Amount, Title = "Kiểm thử", Subtitle = "", Date = h.Date, Status = "success"
                });
            }
            return (true, true);
        });
        return acc.Id;
    }

    private List<TransactionDto> Interests(string id) =>
        _bank.Service.GetTransactions(id, null).Where(t => t.Type == "interest").OrderBy(t => t.Date).ToList();

    [Theory(DisplayName = "IE01 Kỳ lãi theo đúng số ngày của tháng: 28 (2/2023), 29 (2/2024 nhuận), 30 (4/2024), 31 (1/2024)")]
    [InlineData(2023, 2, 28)]
    [InlineData(2024, 2, 29)]
    [InlineData(2024, 4, 30)]
    [InlineData(2024, 1, 31)]
    public void MonthLength_DaysCountedExactly(int year, int month, int days)
    {
        var start = new DateTime(year, month, 1);
        var id = AddSaving(start, (start.AddHours(9), "open", 10_000_000));

        _bank.Service.ApplyDueInterest(start.AddMonths(1).AddHours(9));

        var tx = Assert.Single(Interests(id));
        Assert.Equal(days, DateTime.DaysInMonth(year, month));
        Assert.Equal(Expected(10_000_000, Rate, days), tx.Amount);
        Assert.Contains(start.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture), tx.Subtitle);
    }

    [Fact(DisplayName = "IE02 Cả năm nhuận 2024: 12 kỳ liền nhau, tổng 366 ngày, lãi kép đúng từng kỳ")]
    public void LeapYear_TwelvePeriods_366Days()
    {
        var start = new DateTime(2024, 1, 1);
        var id = AddSaving(start, (start.AddHours(9), "open", 10_000_000));

        _bank.Service.ApplyDueInterest(new DateTime(2025, 1, 1, 9, 0, 0));

        var list = Interests(id);
        Assert.Equal(12, list.Count);
        long balance = 10_000_000;
        var totalDays = 0;
        for (var m = 0; m < 12; m++)
        {
            var days = DateTime.DaysInMonth(2024, m + 1);
            totalDays += days;
            var expected = Expected(balance, Rate, days);
            Assert.Equal(expected, list[m].Amount);
            balance += expected;
        }
        Assert.Equal(366, totalDays);
        Assert.Equal(balance, _bank.Balance(id));
        Assert.Equal(new DateTime(2025, 1, 1), _bank.Service.GetAccount(id)!.Interest!.PeriodStart);
    }

    [Fact(DisplayName = "IE03 Mở sổ ngày 31/01/2024: kỳ sau rơi vào 29/02, các kỳ nối tiếp không trùng không hở")]
    public void OpenedOn31st_PeriodsAreContiguous()
    {
        var start = new DateTime(2024, 1, 31);
        var id = AddSaving(start, (start.AddHours(9), "open", 10_000_000));

        _bank.Service.ApplyDueInterest(new DateTime(2024, 4, 30, 9, 0, 0));

        var list = Interests(id);
        // 31/01 → 29/02 (29 ngày), 29/02 → 29/03 (29 ngày), 29/03 → 29/04 (31 ngày)
        Assert.Equal(3, list.Count);
        Assert.Contains("31/01/2024 – 28/02/2024", list[0].Subtitle);
        Assert.Contains("29/02/2024 – 28/03/2024", list[1].Subtitle);
        Assert.Contains("29/03/2024 – 28/04/2024", list[2].Subtitle);
        Assert.Equal(Expected(10_000_000, Rate, 29), list[0].Amount);
        Assert.Equal(new DateTime(2024, 4, 29), _bank.Service.GetAccount(id)!.Interest!.PeriodStart);
    }

    [Fact(DisplayName = "IE04 Số dư về 0 giữa kỳ rồi nạp lại: chỉ tính lãi những ngày có tiền (10 + 11 = 21 ngày)")]
    public void BalanceZeroMidPeriod_ThenDepositAgain()
    {
        var start = new DateTime(2024, 3, 1);
        var id = AddSaving(start,
            (start.AddHours(9), "open", 10_000_000),
            (new DateTime(2024, 3, 11, 10, 0, 0), "withdraw", 10_000_000),   // cuối ngày 11/03 số dư = 0
            (new DateTime(2024, 3, 21, 10, 0, 0), "deposit", 10_000_000));   // từ 21/03 có tiền lại

        _bank.Service.ApplyDueInterest(new DateTime(2024, 4, 1, 9, 0, 0));

        var tx = Assert.Single(Interests(id));
        Assert.Equal(Expected(10_000_000, Rate, 10 + 11), tx.Amount);
    }

    [Fact(DisplayName = "IE05 Rút hết tiền ngày thứ 6 của kỳ: chỉ có lãi 5 ngày đầu")]
    public void WithdrawAllInPeriod_OnlyEarlierDaysEarn()
    {
        var start = new DateTime(2024, 3, 1);
        var id = AddSaving(start,
            (start.AddHours(9), "open", 10_000_000),
            (new DateTime(2024, 3, 6, 15, 0, 0), "withdraw", 10_000_000));

        _bank.Service.ApplyDueInterest(new DateTime(2024, 4, 1, 9, 0, 0));

        var tx = Assert.Single(Interests(id));
        Assert.Equal(Expected(10_000_000, Rate, 5), tx.Amount);
    }

    [Fact(DisplayName = "IE06 Rút hết ngay ngày mở: không có lãi, không tạo giao dịch, nhưng kỳ vẫn dời sang tháng sau")]
    public void WithdrawAllSameDay_NoInterestButPeriodMoves()
    {
        var start = new DateTime(2024, 3, 1);
        var id = AddSaving(start,
            (start.AddHours(9), "open", 10_000_000),
            (start.AddHours(16), "withdraw", 10_000_000));

        _bank.Service.ApplyDueInterest(new DateTime(2024, 4, 1, 9, 0, 0));

        Assert.Empty(Interests(id));
        Assert.Equal(0, _bank.Balance(id));
        Assert.Equal(new DateTime(2024, 4, 1), _bank.Service.GetAccount(id)!.Interest!.PeriodStart);
    }

    [Fact(DisplayName = "IE07 Server tắt 12 tháng qua năm mới (15/07/2023 → 15/07/2024): cộng đủ 12 kỳ, chạy lại không trùng")]
    public void ServerOffAYear_AcrossNewYear()
    {
        var start = new DateTime(2023, 7, 15);
        var id = AddSaving(start, (start.AddHours(9), "open", 10_000_000));
        var asOf = new DateTime(2024, 7, 15, 9, 0, 0);

        _bank.Service.ApplyDueInterest(asOf);
        var again = _bank.Service.ApplyDueInterest(asOf.AddDays(3));

        var list = Interests(id);
        Assert.Equal(12, list.Count);
        Assert.DoesNotContain(again.Transactions, t => t.AccountId == id);
        // Kỳ 15/01 → 15/02/2024 có 31 ngày, kỳ 15/02 → 15/03/2024 có 29 ngày (năm nhuận)
        var feb = list.Single(t => t.Subtitle.Contains("15/02/2024 – 14/03/2024"));
        var jan = list.Single(t => t.Subtitle.Contains("15/01/2024 – 14/02/2024"));
        Assert.True(feb.Amount < jan.Amount);
        Assert.Equal(new DateTime(2024, 7, 15), _bank.Service.GetAccount(id)!.Interest!.PeriodStart);
    }

    [Fact(DisplayName = "IE08 Thanh toán âm rồi dương trong cùng ngày: không phát sinh lãi, thống kê ngày vẫn cân đối")]
    public void PaymentNegativeThenPositive_NoInterestAndBalanced()
    {
        Assert.True(_bank.Service.Withdraw(TestBank.Payment, 13_000_000).Ok);   // 3.250.000 → -9.750.000
        Assert.True(_bank.Service.Deposit(TestBank.Payment, 20_000_000).Ok);    // → 10.250.000

        var run = _bank.Service.ApplyDueInterest(DateTime.Now.AddMonths(2));
        var stats = _bank.Service.GetStatistics(DateTime.Today, DateTime.Today, "day", TestBank.Payment).Value!;

        Assert.DoesNotContain(run.Transactions, t => t.AccountId == TestBank.Payment);
        Assert.Equal(10_250_000, _bank.Balance(TestBank.Payment));
        var p = Assert.Single(stats.Periods);
        Assert.Equal(p.OpeningBalance + p.TotalIn - p.TotalOut, p.ClosingBalance);
    }
}
