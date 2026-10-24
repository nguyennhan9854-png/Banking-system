using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NganHang.Data;
using NganHang.Models;
using NganHang.Services;

namespace NganHang.Tests;

/// <summary>
/// Kiểm thử MÔ HÌNH HƯỚNG ĐỐI TƯỢNG (Luân – tuần 3):
/// kế thừa Account → SavingAccount / PaymentAccount, đa hình (AvailableBalance, CheckSpend, EarnsInterest),
/// đóng gói số dư (chỉ đổi qua Credit/Debit), đọc/ghi file JSON đúng lớp con.
/// </summary>
public class AccountModelTests : IDisposable
{
    private readonly TestBank _bank = new();
    public void Dispose() => _bank.Dispose();

    private static SavingAccount Saving(long balance, double rate = 6.0)
    {
        var a = new SavingAccount { Id = "s", Name = "Tiết kiệm", InterestRate = rate, TermMonths = 6 };
        if (balance > 0) a.Credit(balance);
        return a;
    }

    private static PaymentAccount Payment(long balance, long overdraft)
    {
        var a = new PaymentAccount { Id = "p", Name = "Thanh toán", OverdraftLimit = overdraft };
        if (balance > 0) a.Credit(balance);
        return a;
    }

    [Fact(DisplayName = "OOP01 Đa hình: cùng gọi Account.AvailableBalance, mỗi lớp con trả kết quả riêng")]
    public void Polymorphism_AvailableBalance()
    {
        var accounts = new List<Account> { Saving(1_000_000), Payment(1_000_000, 5_000_000) };

        var available = accounts.Select(a => a.AvailableBalance).ToArray();

        Assert.Equal(new long[] { 1_000_000, 6_000_000 }, available);
        Assert.Equal(new[] { "saving", "payment" }, accounts.Select(a => a.Type).ToArray());
    }

    [Fact(DisplayName = "OOP02 Đa hình: CheckSpend vượt mức báo lỗi theo từng loại tài khoản")]
    public void Polymorphism_CheckSpendMessages()
    {
        Assert.Null(Saving(1_000_000).CheckSpend(1_000_000, "rút"));
        Assert.Contains("Số dư không đủ", Saving(1_000_000).CheckSpend(1_000_001, "rút"));
        Assert.Null(Payment(0, 5_000_000).CheckSpend(5_000_000, "chuyển"));
        Assert.Contains("vượt quá hạn mức", Payment(0, 5_000_000).CheckSpend(5_000_001, "chuyển"));
    }

    [Fact(DisplayName = "OOP03 Đa hình: chỉ tài khoản tiết kiệm đang hoạt động, có lãi suất mới được cộng lãi")]
    public void Polymorphism_EarnsInterest()
    {
        var locked = Saving(1_000_000);
        locked.Status = "locked";

        Assert.True(Saving(1_000_000).EarnsInterest);
        Assert.False(Saving(1_000_000, rate: 0).EarnsInterest);
        Assert.False(locked.EarnsInterest);
        Assert.False(Payment(1_000_000, 10_000_000).EarnsInterest);
    }

    [Fact(DisplayName = "OOP04 Thấu chi: chỉ tài khoản thanh toán chi vượt số dư mới tính là dùng thấu chi")]
    public void Polymorphism_UsesOverdraft()
    {
        Assert.False(Payment(1_000_000, 5_000_000).UsesOverdraft(1_000_000));
        Assert.True(Payment(1_000_000, 5_000_000).UsesOverdraft(1_000_001));
        Assert.False(Saving(1_000_000).UsesOverdraft(2_000_000));
    }

    [Fact(DisplayName = "OOP05 Đóng gói: Debit vượt khả dụng bị chặn ngay trong lớp, số dư không đổi")]
    public void Encapsulation_DebitGuard()
    {
        var s = Saving(1_000_000);
        var p = Payment(0, 5_000_000);

        Assert.Throws<InvalidOperationException>(() => s.Debit(1_000_001));
        Assert.Throws<InvalidOperationException>(() => p.Debit(5_000_001));
        Assert.Equal(1_000_000, s.Balance);
        Assert.Equal(0, p.Balance);

        p.Debit(5_000_000);
        Assert.Equal(-5_000_000, p.Balance);
    }

    [Theory(DisplayName = "OOP06 Đóng gói: Credit/Debit số tiền 0 hoặc âm bị từ chối")]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Encapsulation_NonPositiveAmounts(long amount)
    {
        var s = Saving(1_000_000);

        Assert.Throws<ArgumentOutOfRangeException>(() => s.Credit(amount));
        Assert.Throws<ArgumentOutOfRangeException>(() => s.Debit(amount));
        Assert.Equal(1_000_000, s.Balance);
    }

    [Fact(DisplayName = "OOP07 Lưu file rồi mở lại: tạo đúng lớp con SavingAccount / PaymentAccount với đủ thuộc tính")]
    public void Json_RoundTrip_KeepsSubclasses()
    {
        var opened = _bank.Service.OpenAccount(new OpenAccountRequest("payment", 0, 2_000_000, 5_000_000)).Value!;

        var repo = new BankRepository(_bank.FilePath, NullLogger<BankRepository>.Instance);
        var accounts = repo.Read(d => d.Accounts.ToList());

        var saving = Assert.IsType<SavingAccount>(accounts.Single(a => a.Id == TestBank.Saving));
        Assert.Equal(5.8, saving.InterestRate);
        Assert.Equal(6, saving.TermMonths);
        Assert.NotNull(saving.LastInterestDate);
        var payment = Assert.IsType<PaymentAccount>(accounts.Single(a => a.Id == TestBank.Payment));
        Assert.Equal(10_000_000, payment.OverdraftLimit);
        var added = Assert.IsType<PaymentAccount>(accounts.Single(a => a.Id == opened.Id));
        Assert.Equal(5_000_000, added.OverdraftLimit);
        Assert.Equal(2_000_000, added.Balance);
    }

    [Fact(DisplayName = "OOP08 File JSON có trường type, không lưu các thuộc tính tính toán (availableBalance, isActive...)")]
    public void Json_File_HasTypeButNoComputedProperties()
    {
        var json = File.ReadAllText(_bank.FilePath);

        Assert.Contains("\"type\": \"saving\"", json);
        Assert.Contains("\"type\": \"payment\"", json);
        Assert.DoesNotContain("availableBalance", json);
        Assert.DoesNotContain("isActive", json);
        Assert.DoesNotContain("earnsInterest", json);
    }

    [Fact(DisplayName = "OOP09 Đọc được file kiểu tuần 2 (một lớp Account chung, 'type' không đứng đầu)")]
    public void Json_ReadsWeek2Format()
    {
        const string week2 = """
        {
          "version": 1, "seq": 0,
          "user": { "name": "Luân Nhân" },
          "accounts": [
            { "id": "acc-saving", "type": "saving", "name": "Tài khoản tiết kiệm", "accountNumber": "0123 4567 8888",
              "balance": 1000000, "status": "active", "openedDate": "2024-06-12", "productName": "Tiết kiệm có kỳ hạn",
              "interestRate": 5.8, "termMonths": 6, "overdraftLimit": 0, "branch": "CN", "lastInterestDate": "2026-10-01T00:00:00" },
            { "id": "acc-payment", "name": "Tài khoản thanh toán", "type": "payment", "accountNumber": "0123 4567 9999",
              "balance": -200000, "status": "active", "openedDate": "2024-06-11", "productName": "Thanh toán",
              "interestRate": 0, "termMonths": 0, "overdraftLimit": 10000000, "branch": "CN", "lastInterestDate": null }
          ],
          "transactions": [], "notifications": []
        }
        """;
        var path = Path.Combine(Path.GetTempPath(), "nganhang-week2-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, week2);
        try
        {
            var repo = new BankRepository(path, NullLogger<BankRepository>.Instance);
            var service = new BankService(repo);

            Assert.Null(repo.CorruptBackupPath);
            Assert.Equal(1_000_000, service.GetAccount("acc-saving")!.Balance);
            Assert.Equal(5.8, service.GetAccount("acc-saving")!.InterestRate);
            Assert.Equal(-200_000, service.GetAccount("acc-payment")!.Balance);
            Assert.Equal(9_800_000, service.GetAccount("acc-payment")!.AvailableBalance);
        }
        finally
        {
            try { File.Delete(path); } catch { /* bỏ qua */ }
        }
    }

    [Fact(DisplayName = "OOP10 Loại tài khoản lạ trong file: báo lỗi JSON (repository coi là file hỏng)")]
    public void Json_UnknownType_Throws()
    {
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new AccountJsonConverter() } };

        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<Account>("{\"id\":\"x\",\"type\":\"credit\"}", options));
        Assert.IsType<SavingAccount>(JsonSerializer.Deserialize<Account>("{\"id\":\"x\",\"type\":\"saving\"}", options));
    }
}
