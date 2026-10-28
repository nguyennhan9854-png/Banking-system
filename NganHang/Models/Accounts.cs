using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NganHang.Models;

// =====================================================================
//  TÀI KHOẢN NGÂN HÀNG – KẾ THỪA VÀ ĐA HÌNH (Luân – tuần 3)
//
//                Account (lớp trừu tượng)
//               /                        \
//        SavingAccount               PaymentAccount
//   (tiết kiệm: có lãi suất,     (thanh toán: có hạn mức thấu chi,
//    không được âm)               được âm trong hạn mức)
//
//  Trước tuần 3: một lớp Account chung + chuỗi Type "saving"/"payment" và nhiều câu
//  if (acc.Type == "payment") ... rải trong BankService.
//  Sau tuần 3: mỗi loại tự biết số dư khả dụng, có được cộng lãi không, báo lỗi thế nào
//  (ghi đè AvailableBalance, EarnsInterest, SpendError...). BankService chỉ gọi chung qua Account.
// =====================================================================

/// <summary>
/// Lớp cha của mọi loại tài khoản. Tiền lưu bằng long (đơn vị: đồng).
/// Số dư CHỈ thay đổi qua <see cref="Credit"/> / <see cref="Debit"/> (đóng gói),
/// setter để internal cho việc đọc file JSON và dữ liệu mẫu.
/// </summary>
public abstract class Account
{
    public string Id { get; set; } = "";

    /// <summary>"saving" hoặc "payment" – do lớp con quyết định, được ghi vào file để biết tạo lại lớp nào.</summary>
    public abstract string Type { get; }

    public string Name { get; set; } = "";
    public string AccountNumber { get; set; } = "";

    [JsonInclude]
    public long Balance { get; internal set; }

    /// <summary>"active" = đang hoạt động, "locked" = bị khóa</summary>
    public string Status { get; set; } = "active";

    /// <summary>Thời điểm bị khóa (null = đang hoạt động). Tuần 4.</summary>
    public DateTime? LockedAt { get; set; }

    /// <summary>Ngày mở dạng yyyy-MM-dd</summary>
    public string OpenedDate { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string Branch { get; set; } = "";

    [JsonIgnore]
    public bool IsActive => Status == "active";

    /// <summary>Số tiền tối đa được rút/chuyển ngay lúc này. Mặc định: không được âm.</summary>
    [JsonIgnore]
    public virtual long AvailableBalance => Math.Max(Balance, 0);

    /// <summary>Tài khoản có được cộng lãi không. Mặc định: không.</summary>
    [JsonIgnore]
    public virtual bool EarnsInterest => false;

    /// <summary>Rút/chuyển <paramref name="amount"/> có phải dùng tới tiền thấu chi không.</summary>
    public virtual bool UsesOverdraft(long amount) => false;

    /// <summary>Thông báo lỗi khi rút/chuyển vượt số tiền khả dụng (mỗi loại tài khoản báo khác nhau).</summary>
    protected abstract string SpendError(string verb);

    /// <summary>Kiểm tra có được trừ <paramref name="amount"/> không. null = hợp lệ, ngược lại là thông báo lỗi.</summary>
    public string? CheckSpend(long amount, string verb) =>
        amount <= AvailableBalance ? null : SpendError(verb);

    /// <summary>Cộng tiền vào tài khoản.</summary>
    public void Credit(long amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Số tiền cộng phải lớn hơn 0.");
        Balance = checked(Balance + amount);
    }

    /// <summary>Trừ tiền. Không bao giờ cho trừ quá số tiền khả dụng (bảo vệ lần cuối, kể cả khi nghiệp vụ quên kiểm tra).</summary>
    public void Debit(long amount)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Số tiền trừ phải lớn hơn 0.");
        if (amount > AvailableBalance) throw new InvalidOperationException(SpendError("trừ"));
        Balance -= amount;
    }

    /// <summary>12580000 → "12.580.000 đ"</summary>
    public static string Vnd(long value) =>
        (value < 0 ? "-" : "") + Math.Abs(value).ToString("N0", CultureInfo.InvariantCulture).Replace(",", ".") + " đ";
}

/// <summary>Tài khoản tiết kiệm: có kỳ hạn, lãi suất, được cộng lãi hằng tháng, không được âm.</summary>
public sealed class SavingAccount : Account
{
    public override string Type => "saving";

    /// <summary>Lãi suất %/năm</summary>
    public double InterestRate { get; set; }

    /// <summary>Kỳ hạn (tháng)</summary>
    public int TermMonths { get; set; }

    /// <summary>
    /// Ngày bắt đầu kỳ tính lãi hiện tại (= ngày cộng lãi gần nhất).
    /// Để trống thì hệ thống tự suy ra từ giao dịch cộng lãi gần nhất hoặc ngày mở tài khoản.
    /// </summary>
    public DateTime? LastInterestDate { get; set; }

    /// <summary>
    /// Có lãi suất là được cộng lãi – KỂ CẢ khi đang bị khóa (tuần 4).
    /// Khóa chỉ chặn giao dịch do người dùng làm (nạp, rút, chuyển, nhận lãi ngay), không làm mất tiền lãi.
    /// </summary>
    [JsonIgnore]
    public override bool EarnsInterest => InterestRate > 0;

    protected override string SpendError(string verb) =>
        "Số dư không đủ. Số tiền " + verb + " tối đa là " + Vnd(AvailableBalance) + ".";
}

/// <summary>Tài khoản thanh toán: không có lãi, được chi âm trong hạn mức thấu chi.</summary>
public sealed class PaymentAccount : Account
{
    public override string Type => "payment";

    /// <summary>Hạn mức thấu chi (0 = không cho âm)</summary>
    public long OverdraftLimit { get; set; }

    [JsonIgnore]
    public override long AvailableBalance => Balance + OverdraftLimit;

    public override bool UsesOverdraft(long amount) => amount > Balance;

    protected override string SpendError(string verb) =>
        "Số tiền " + verb + " vượt quá hạn mức khả dụng.";
}

/// <summary>
/// Đọc/ghi Account trong file JSON: nhìn trường "type" để tạo đúng lớp con
/// (SavingAccount hoặc PaymentAccount). File cũ của tuần 2 vẫn đọc được vì đã có sẵn "type".
/// </summary>
public sealed class AccountJsonConverter : JsonConverter<Account>
{
    public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Account);

    public override Account? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var doc = JsonDocument.ParseValue(ref reader);
        var root = doc.RootElement;
        string? type = null;
        foreach (var prop in root.EnumerateObject())
        {
            if (string.Equals(prop.Name, "type", StringComparison.OrdinalIgnoreCase) && prop.Value.ValueKind == JsonValueKind.String)
                type = prop.Value.GetString();
        }
        var target = type switch
        {
            "saving" => typeof(SavingAccount),
            "payment" => typeof(PaymentAccount),
            _ => throw new JsonException("Loại tài khoản không hợp lệ: " + (type ?? "(trống)"))
        };
        return (Account?)root.Deserialize(target, options);
    }

    public override void Write(Utf8JsonWriter writer, Account value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}
