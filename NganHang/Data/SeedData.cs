using NganHang.Models;

namespace NganHang.Data;

/// <summary>
/// Dữ liệu mẫu ban đầu. Ngày giao dịch được tính lùi từ lúc chạy lần đầu
/// để biểu đồ "6 tháng gần nhất" luôn có dữ liệu.
/// Số dư hiện tại là con số gốc; số dư trong quá khứ được tính ngược từ giao dịch.
/// </summary>
public static class SeedData
{
    // (số ngày trước, giờ, phút, tài khoản, loại, số tiền, nội dung, mô tả phụ)
    // giờ = -1 nghĩa là "hôm nay, cách đây 90 phút"
    private static readonly (int DaysAgo, int Hour, int Minute, string AccountId, string Type, long Amount, string Title, string Subtitle)[] Rows =
    {
        (175, 10, 0, "acc-saving", "deposit", 1_500_000, "Nạp tiền vào tài khoản", "Tài khoản tiết kiệm"),
        (170, 9, 20, "acc-payment", "transfer_in", 3_000_000, "Nhận lương tháng", "Công ty TNHH Minh Phát"),
        (163, 19, 5, "acc-payment", "bill", 1_200_000, "Thanh toán hóa đơn điện", "EVN Hà Nội"),
        (152, 20, 41, "acc-payment", "shopping", 800_000, "Mua sắm tại Shopee", "Thanh toán thẻ"),
        (150, 8, 0, "acc-saving", "interest", 18_000, "Cộng lãi định kỳ", "Tài khoản tiết kiệm"),
        (140, 9, 15, "acc-payment", "transfer_in", 3_000_000, "Nhận lương tháng", "Công ty TNHH Minh Phát"),
        (138, 10, 30, "acc-saving", "deposit", 2_000_000, "Nạp tiền vào tài khoản", "Tài khoản tiết kiệm"),
        (128, 14, 10, "acc-payment", "transfer_out", 2_000_000, "Chuyển tiền đến Phạm Anh Duy", "BIDV"),
        (120, 8, 0, "acc-saving", "interest", 25_000, "Cộng lãi định kỳ", "Tài khoản tiết kiệm"),
        (112, 18, 30, "acc-payment", "bill", 450_000, "Thanh toán hóa đơn nước", "Cấp nước Hà Nội"),
        (105, 19, 22, "acc-payment", "transfer_out", 1_500_000, "Chuyển tiền đến Phạm Thị Lan", "Vietcombank"),
        (104, 11, 0, "acc-saving", "deposit", 3_000_000, "Nạp tiền vào tài khoản", "Tài khoản tiết kiệm"),
        (99, 9, 10, "acc-payment", "transfer_in", 2_500_000, "Nhận tiền từ Lê Thị Mai", "Techcombank"),
        (90, 8, 0, "acc-saving", "interest", 35_000, "Cộng lãi định kỳ", "Tài khoản tiết kiệm"),
        (86, 13, 22, "acc-payment", "transfer_out", 1_500_000, "Chuyển tiền đến Trần Hoàng Minh", "MB Bank"),
        (74, 10, 10, "acc-saving", "deposit", 1_000_000, "Nạp tiền vào tài khoản", "Tài khoản tiết kiệm"),
        (71, 9, 5, "acc-payment", "transfer_in", 3_000_000, "Nhận lương tháng", "Công ty TNHH Minh Phát"),
        (66, 21, 5, "acc-payment", "transfer_out", 1_700_000, "Chuyển tiền đến Hoàng Minh Đức", "Vietcombank"),
        (60, 8, 0, "acc-saving", "interest", 40_000, "Cộng lãi định kỳ", "Tài khoản tiết kiệm"),
        (57, 16, 48, "acc-payment", "shopping", 2_200_000, "Mua sắm tại Điện Máy Xanh", "Thanh toán thẻ"),
        (43, 20, 15, "acc-payment", "bill", 320_000, "Thanh toán hóa đơn điện", "EVN Hà Nội"),
        (40, 15, 30, "acc-saving", "deposit", 2_000_000, "Nạp tiền vào tài khoản", "Tài khoản tiết kiệm"),
        (34, 16, 20, "acc-payment", "transfer_in", 2_000_000, "Nhận tiền từ Lê Thị Mai", "Chuyển khoản"),
        (30, 8, 0, "acc-saving", "interest", 50_000, "Cộng lãi định kỳ", "Tài khoản tiết kiệm"),
        (21, 9, 15, "acc-payment", "shopping", 180_000, "Mua sắm tại Shopee", "Thanh toán thẻ"),
        (12, 18, 45, "acc-payment", "transfer_out", 850_000, "Chuyển tiền đến Đặng Minh Quân", "VietinBank"),
        (10, 9, 40, "acc-saving", "deposit", 1_500_000, "Nạp tiền vào tài khoản", "Tài khoản tiết kiệm"),
        (4, 11, 20, "acc-saving", "interest", 58_000, "Cộng lãi định kỳ", "Tài khoản tiết kiệm"),
        (3, 9, 15, "acc-payment", "transfer_in", 2_500_000, "Nhận tiền từ Trần Thị Hoa", "Vietcombank"),
        (2, 20, 1, "acc-payment", "bill", 320_000, "Thanh toán hóa đơn", "Điện lực Hà Nội"),
        (0, -1, -1, "acc-payment", "transfer_out", 1_000_000, "Chuyển tiền đến Nguyễn Văn Minh", "MB Bank"),
    };

    public static BankData Create(DateTime now)
    {
        var transactions = new List<Transaction>();
        var seq = 0;
        foreach (var r in Rows)
        {
            DateTime date = r.Hour < 0
                ? now.AddMinutes(-90)
                : now.Date.AddDays(-r.DaysAgo).AddHours(r.Hour).AddMinutes(r.Minute);
            seq++;
            transactions.Add(new Transaction
            {
                Id = "tx-" + seq,
                Seq = seq,
                Code = "GD" + (1245700 + seq),
                AccountId = r.AccountId,
                Type = r.Type,
                Amount = r.Amount,
                Title = r.Title,
                Subtitle = r.Subtitle,
                Date = date,
                Status = "success"
            });
        }

        var data = new BankData
        {
            Version = 1,
            SeededAt = now,
            Seq = seq,
            User = new UserProfile
            {
                Name = "Luân Nhân",
                Email = "luannhan@email.com",
                Phone = "0901 234 567",
                LastLogin = now.AddHours(-1),
                OtpEnabled = true
            },
            Accounts = new List<Account>
            {
                new()
                {
                    Id = "acc-saving", Type = "saving", Name = "Tài khoản tiết kiệm",
                    AccountNumber = "0123 4567 8888", Balance = 12_580_000, Status = "active",
                    OpenedDate = "2024-06-12", ProductName = "Tiết kiệm có kỳ hạn",
                    InterestRate = 5.8, TermMonths = 6, OverdraftLimit = 0, Branch = "Chi nhánh Thủ Đức"
                },
                new()
                {
                    Id = "acc-payment", Type = "payment", Name = "Tài khoản thanh toán",
                    AccountNumber = "0123 4567 9999", Balance = 3_250_000, Status = "active",
                    OpenedDate = "2024-06-11", ProductName = "Thanh toán",
                    InterestRate = 0, TermMonths = 0, OverdraftLimit = 10_000_000, Branch = "Chi nhánh Thủ Đức"
                }
            },
            Transactions = transactions,
            Notifications = new List<Notification>
            {
                new() { Id = "n1", Title = "Bạn vừa nhận 2.500.000 đ từ Trần Thị Hoa", Date = now.AddDays(-3), Read = false },
                new() { Id = "n2", Title = "Tài khoản tiết kiệm được cộng lãi 58.000 đ", Date = now.AddDays(-4), Read = false },
                new() { Id = "n3", Title = "Đăng nhập mới trên trình duyệt Chrome", Date = now.AddDays(-5), Read = false }
            }
        };

        // Kỳ tính lãi hiện tại của tài khoản tiết kiệm bắt đầu từ lần cộng lãi gần nhất trong dữ liệu mẫu
        var saving = data.Accounts.First(a => a.Id == "acc-saving");
        saving.LastInterestDate = transactions
            .Where(t => t.AccountId == saving.Id && t.Type == "interest")
            .Max(t => t.Date).Date;
        return data;
    }
}
