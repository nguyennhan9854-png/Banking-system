using Microsoft.Extensions.Logging.Abstractions;
using NganHang.Data;
using NganHang.Services;

namespace NganHang.Tests;

/// <summary>
/// Tạo một "ngân hàng" riêng cho từng test: dữ liệu mẫu ghi vào file tạm, test xong thì xóa.
/// Dữ liệu mẫu ban đầu:
///   - acc-saving : Tài khoản tiết kiệm, số dư 12.580.000 đ, không có thấu chi
///   - acc-payment: Tài khoản thanh toán, số dư 3.250.000 đ, hạn mức thấu chi 10.000.000 đ
/// </summary>
public sealed class TestBank : IDisposable
{
    public const string Saving = "acc-saving";
    public const string Payment = "acc-payment";

    public string FilePath { get; }
    public BankService Service { get; }

    public TestBank()
    {
        FilePath = Path.Combine(Path.GetTempPath(), "nganhang-test-" + Guid.NewGuid().ToString("N") + ".json");
        Service = new BankService(new BankRepository(FilePath, NullLogger<BankRepository>.Instance));
    }

    /// <summary>Số dư hiện tại của tài khoản.</summary>
    public long Balance(string accountId) => Service.GetAccount(accountId)!.Balance;

    /// <summary>Tổng số giao dịch đang lưu.</summary>
    public int TransactionCount() => Service.GetTransactions(null, null).Count;

    public void Dispose()
    {
        try { File.Delete(FilePath); } catch { /* bỏ qua */ }
    }
}
