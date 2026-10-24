using NganHang.Data;
using NganHang.Models;
using NganHang.Services;

namespace NganHang.Tests;

/// <summary>
/// KỊCH BẢN TEST ROLLBACK (viết TRƯỚC khi có code rollback, theo gợi ý của thầy).
///
/// Ý tưởng: dùng FakeFaults để "cắm" lỗi vào giữa một thao tác ghi, ví dụ đã trừ tiền tài khoản nguồn
/// nhưng chưa kịp cộng tài khoản nhận, hoặc ổ đĩa lỗi lúc lưu file. Sau sự cố, hệ thống phải:
///   1. Không ném lỗi ra ngoài, trả về thông báo "đã hoàn tác".
///   2. Số dư, lịch sử, kỳ lãi giữ NGUYÊN như trước thao tác (cả trong bộ nhớ lẫn trong file).
///   3. Ghi lại 1 giao dịch trạng thái "failed" (không làm đổi số dư) + 1 thông báo thất bại.
///   4. Chạy lại bình thường khi hết lỗi, không cộng trùng hay bỏ sót.
/// </summary>
public class RollbackTests : IDisposable
{
    private readonly TestBank _bank = new();
    public void Dispose() => _bank.Dispose();

    private static TransferRequest Internal(string from, string to, long amount) =>
        new(from, to, null, null, null, amount, null);

    private static TransferRequest External(string from, long amount) =>
        new(from, null, "0987 654 321", "NGUYEN VAN A", "Vietcombank", amount, null);

    /// <summary>
    /// "Ảnh chụp" những gì KHÔNG được thay đổi khi thao tác bị hoàn tác:
    /// số dư + kỳ lãi của từng tài khoản, các giao dịch thành công kèm số dư sau giao dịch.
    /// </summary>
    private string Snapshot(BankService? service = null)
    {
        var s = service ?? _bank.Service;
        var accounts = s.GetAccounts()
            .Select(a => a.Id + "=" + a.Balance + "@" + a.Interest?.PeriodStart.ToString("yyyy-MM-dd"));
        var txs = s.GetTransactions(null, null)
            .Where(t => t.Status == "success")
            .Select(t => t.Id + ":" + t.Type + ":" + t.Amount + ":" + t.BalanceAfter);
        return string.Join("|", accounts) + " # " + string.Join("|", txs);
    }

    private int SuccessNotifications() =>
        _bank.Service.GetState().Notifications.Count(n => n.Title.Contains("thành công"));

    private List<TransactionDto> Failed() =>
        _bank.Service.GetTransactions(null, null).Where(t => t.Status == "failed").ToList();

    private static void AssertRolledBack<T>(ServiceResult<T> result)
    {
        Assert.False(result.Ok);
        Assert.False(result.NotFound);
        Assert.Contains("hoàn tác", result.Error);
    }

    // ================= CHUYỂN TIỀN =================

    [Fact(DisplayName = "RB01 Chuyển nội bộ lỗi SAU KHI trừ nguồn, CHƯA cộng đích: hoàn tác, 2 tài khoản giữ nguyên")]
    public void Transfer_FailAfterDebit_RollsBackBothAccounts()
    {
        var before = Snapshot();
        _bank.Faults.FailAt(FaultPoints.TransferAfterDebit);

        var result = _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 1_000_000));

        Assert.True(_bank.Faults.WasHit(FaultPoints.TransferAfterDebit)); // lỗi xảy ra đúng lúc đã trừ tiền
        AssertRolledBack(result);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
        Assert.Equal(12_580_000, _bank.Balance(TestBank.Saving));
        Assert.Equal(before, Snapshot());
    }

    [Fact(DisplayName = "RB02 Chuyển nội bộ lỗi SAU KHI đã trừ và cộng (chưa lưu): vẫn hoàn tác cả 2 vế")]
    public void Transfer_FailAfterCredit_RollsBackBothAccounts()
    {
        var before = Snapshot();
        _bank.Faults.FailAt(FaultPoints.TransferAfterCredit);

        var result = _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 1_000_000));

        AssertRolledBack(result);
        Assert.Equal(before, Snapshot());
        Assert.DoesNotContain(_bank.Service.GetTransactions(null, null),
            t => t.Status == "success" && (t.Type == "transfer_in" || t.Type == "transfer_out") && t.Date > DateTime.Now.AddMinutes(-1));
    }

    [Fact(DisplayName = "RB03 Ổ đĩa lỗi khi lưu file lúc chuyển tiền: bộ nhớ và file đều giữ nguyên số dư cũ")]
    public void Transfer_SaveFails_MemoryAndFileUnchanged()
    {
        var before = Snapshot();
        _bank.Faults.FailAt(FaultPoints.SaveFile);

        var result = _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 2_000_000));

        AssertRolledBack(result);
        Assert.Equal(before, Snapshot());
        Assert.Equal(before, Snapshot(_bank.Reopen()));     // khởi động lại server: file cũng không bị ghi dở
    }

    [Fact(DisplayName = "RB04 Chuyển ra ngân hàng khác lỗi sau khi trừ tiền: tài khoản nguồn được hoàn lại")]
    public void ExternalTransfer_FailAfterDebit_RestoresSource()
    {
        var before = Snapshot();
        _bank.Faults.FailAt(FaultPoints.TransferAfterDebit);

        var result = _bank.Service.Transfer(External(TestBank.Payment, 5_000_000));

        AssertRolledBack(result);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
        Assert.Equal(before, Snapshot());
    }

    [Fact(DisplayName = "RB05 Giao dịch bị hoàn tác được ghi lại với trạng thái 'failed' và không làm đổi số dư")]
    public void FailedTransfer_IsRecordedAsFailed()
    {
        var countBefore = _bank.TransactionCount();
        _bank.Faults.FailAt(FaultPoints.TransferAfterDebit);

        _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 1_000_000));

        var failed = Assert.Single(Failed());
        Assert.Equal("transfer_out", failed.Type);
        Assert.Equal(TestBank.Payment, failed.AccountId);
        Assert.Equal(1_000_000, failed.Amount);
        Assert.Equal(countBefore + 1, _bank.TransactionCount());

        // Giao dịch thất bại không tham gia vào số dư: số dư sau giao dịch vẫn nối tiếp đúng
        var list = _bank.Service.GetTransactions(TestBank.Payment, null);
        Assert.Equal(3_250_000, list[0].BalanceAfter);
        for (var i = 0; i < list.Count - 1; i++)
        {
            var change = list[i].Status == "success" ? list[i].SignedAmount : 0;
            Assert.Equal(list[i + 1].BalanceAfter, list[i].BalanceAfter - change);
        }
    }

    [Fact(DisplayName = "RB06 Hoàn tác thì có thông báo 'thất bại', không có thông báo 'thành công'")]
    public void FailedTransfer_NotifiesFailure()
    {
        var successBefore = SuccessNotifications();
        _bank.Faults.FailAt(FaultPoints.TransferAfterCredit);

        _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 1_000_000));

        var newest = _bank.Service.GetState().Notifications.First();
        Assert.Contains("thất bại", newest.Title);
        Assert.Equal(successBefore, SuccessNotifications());
    }

    [Fact(DisplayName = "RB07 Hết lỗi thì chuyển lại bình thường, số dư đúng, không bị 'kẹt'")]
    public void AfterFailure_NextTransferWorks()
    {
        _bank.Faults.FailAt(FaultPoints.TransferAfterDebit);
        _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 1_000_000));

        var again = _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 1_000_000));

        Assert.True(again.Ok, again.Error);
        Assert.Equal(2_250_000, _bank.Balance(TestBank.Payment));
        Assert.Equal(13_580_000, _bank.Balance(TestBank.Saving));
    }

    [Fact(DisplayName = "RB08 Ổ đĩa lỗi liên tục (cả lúc ghi giao dịch thất bại): không ném lỗi, bộ nhớ và file giữ nguyên")]
    public void SaveAlwaysFails_NoExceptionAndNothingChanged()
    {
        var before = Snapshot();
        var countBefore = _bank.TransactionCount();
        _bank.Faults.FailAlways(FaultPoints.SaveFile);

        var result = _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 1_000_000));

        AssertRolledBack(result);
        Assert.Equal(before, Snapshot());
        Assert.Equal(countBefore, _bank.TransactionCount());  // không lưu được cả bản ghi thất bại -> cũng hoàn tác
        Assert.Equal(before, Snapshot(_bank.Reopen()));

        _bank.Faults.Clear();
        Assert.True(_bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 1_000_000)).Ok);
    }

    [Fact(DisplayName = "RB09 Lỗi lưu file không để lại file tạm .tmp")]
    public void SaveFails_NoTempFileLeft()
    {
        _bank.Faults.FailAlways(FaultPoints.SaveFile);

        _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 1_000_000));

        Assert.False(File.Exists(_bank.FilePath + ".tmp"));
        Assert.True(File.Exists(_bank.FilePath));
    }

    [Fact(DisplayName = "RB10 20 lệnh chuyển song song, 5 lệnh gặp lỗi: 15 thành công, tổng tiền 2 tài khoản không đổi")]
    public void ParallelTransfers_WithFailures_MoneyIsConserved()
    {
        var total = _bank.Balance(TestBank.Payment) + _bank.Balance(TestBank.Saving);
        _bank.Faults.FailAt(FaultPoints.TransferAfterDebit, times: 5);

        var results = new ServiceResult<TransferResult>[20];
        Parallel.For(0, 20, i =>
            results[i] = _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 100_000)));

        Assert.Equal(15, results.Count(r => r.Ok));
        Assert.Equal(5, Failed().Count);
        Assert.Equal(3_250_000 - 15 * 100_000, _bank.Balance(TestBank.Payment));
        Assert.Equal(total, _bank.Balance(TestBank.Payment) + _bank.Balance(TestBank.Saving));
    }

    // ================= NẠP / RÚT / MỞ TÀI KHOẢN =================

    [Fact(DisplayName = "RB11 Rút tiền lỗi sau khi trừ số dư: hoàn tác, số dư giữ nguyên")]
    public void Withdraw_FailAfterDebit_RollsBack()
    {
        var before = Snapshot();
        _bank.Faults.FailAt(FaultPoints.WithdrawAfterDebit);

        var result = _bank.Service.Withdraw(TestBank.Saving, 2_000_000);

        AssertRolledBack(result);
        Assert.Equal(12_580_000, _bank.Balance(TestBank.Saving));
        Assert.Equal(before, Snapshot());
        Assert.Equal("withdraw", Assert.Single(Failed()).Type);
    }

    [Fact(DisplayName = "RB12 Nạp tiền lỗi khi lưu file: số dư trong bộ nhớ và trong file giữ nguyên")]
    public void Deposit_SaveFails_RollsBack()
    {
        var before = Snapshot();
        _bank.Faults.FailAt(FaultPoints.SaveFile);

        var result = _bank.Service.Deposit(TestBank.Payment, 500_000);

        AssertRolledBack(result);
        Assert.Equal(3_250_000, _bank.Balance(TestBank.Payment));
        Assert.Equal(before, Snapshot(_bank.Reopen()));
    }

    [Fact(DisplayName = "RB13 Mở tài khoản lỗi giữa chừng: không sinh ra tài khoản 'mồ côi'")]
    public void OpenAccount_FailAfterAdd_NoOrphanAccount()
    {
        _bank.Faults.FailAt(FaultPoints.OpenAfterAdd);

        var result = _bank.Service.OpenAccount(new OpenAccountRequest("saving", 6, 5_000_000, 0));

        AssertRolledBack(result);
        Assert.Equal(2, _bank.Service.GetAccounts().Count);
        Assert.Equal(2, _bank.Reopen().GetAccounts().Count);
    }

    // ================= CỘNG LÃI =================

    [Fact(DisplayName = "RB14 Cộng lãi 3 kỳ bị lỗi sau kỳ 1: không giữ lại kỳ nào, kỳ lãi và số dư như cũ")]
    public void Interest_FailAfterFirstPeriod_NothingPosted()
    {
        var acc = _bank.Service.OpenAccount(new OpenAccountRequest("saving", 12, 10_000_000, 0)).Value!;
        var before = Snapshot();
        _bank.Faults.FailAt(FaultPoints.InterestAfterPeriod);

        var result = _bank.Service.ApplyDueInterest(DateTime.Today.AddMonths(3));

        Assert.NotNull(result.Error);
        Assert.Contains("hoàn tác", result.Error);
        Assert.Empty(result.Transactions);
        Assert.Equal(before, Snapshot());
        Assert.Equal(DateTime.Today, _bank.Service.GetAccount(acc.Id)!.Interest!.PeriodStart);
    }

    [Fact(DisplayName = "RB15 Sau lỗi cộng lãi, lần chạy kế tiếp cộng đủ 3 kỳ, không trùng, không thiếu")]
    public void Interest_AfterFailure_RetryPostsAllPeriodsOnce()
    {
        var acc = _bank.Service.OpenAccount(new OpenAccountRequest("saving", 12, 10_000_000, 0)).Value!;
        _bank.Faults.FailAt(FaultPoints.InterestAfterPeriod);
        _bank.Service.ApplyDueInterest(DateTime.Today.AddMonths(3));

        var retry = _bank.Service.ApplyDueInterest(DateTime.Today.AddMonths(3));

        Assert.Null(retry.Error);
        Assert.Equal(3, retry.Transactions.Count(t => t.AccountId == acc.Id));
        Assert.Equal(3, _bank.Service.GetTransactions(acc.Id, null).Count(t => t.Type == "interest"));
        var thirdEnd = DateTime.Today.AddMonths(1).AddMonths(1).AddMonths(1);   // mỗi kỳ 1 tháng, nối tiếp nhau
        Assert.Equal(thirdEnd, _bank.Service.GetAccount(acc.Id)!.Interest!.PeriodStart);
    }

    [Fact(DisplayName = "RB16 Nhận lãi ngay bị lỗi: hoàn tác, không cộng tiền lãi")]
    public void ApplyAccrued_Fails_RollsBack()
    {
        var acc = _bank.Service.OpenAccount(new OpenAccountRequest("saving", 12, 10_000_000, 0)).Value!;
        var before = Snapshot();
        _bank.Faults.FailAt(FaultPoints.InterestAfterPeriod);

        var result = _bank.Service.ApplyAccruedInterest(acc.Id, DateTime.Today.AddMonths(1).AddDays(5));

        AssertRolledBack(result);
        Assert.Equal(10_000_000, _bank.Balance(acc.Id));
        Assert.Equal(before, Snapshot());
    }

    // ================= ĐỐI CHỨNG =================

    [Fact(DisplayName = "RB17 Đối chứng: không cắm lỗi thì chuyển tiền chạy bình thường, không có giao dịch thất bại")]
    public void NoFault_TransferWorksNormally()
    {
        var result = _bank.Service.Transfer(Internal(TestBank.Payment, TestBank.Saving, 1_000_000));

        Assert.True(result.Ok, result.Error);
        Assert.True(_bank.Faults.WasHit(FaultPoints.TransferAfterDebit));
        Assert.True(_bank.Faults.WasHit(FaultPoints.SaveFile));
        Assert.Empty(Failed());
    }
}
