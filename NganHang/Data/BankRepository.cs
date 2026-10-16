using System.Text.Encodings.Web;
using System.Text.Json;
using NganHang.Models;

namespace NganHang.Data;

/// <summary>
/// Lưu toàn bộ dữ liệu vào file App_Data/bank-data.json.
/// Mọi lần đọc/ghi đều đi qua lock nên nhiều request cùng lúc vẫn an toàn.
/// </summary>
public class BankRepository
{
    private readonly object _lock = new();
    private readonly string _filePath;
    private readonly ILogger<BankRepository> _logger;
    private BankData _data;

    private static readonly JsonSerializerOptions FileJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Giữ nguyên chữ tiếng Việt trong file cho dễ đọc
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <param name="filePath">Đường dẫn file JSON. Web dùng App_Data/bank-data.json; unit test dùng file tạm riêng.</param>
    public BankRepository(string filePath, ILogger<BankRepository> logger)
    {
        _logger = logger;
        _filePath = Path.GetFullPath(filePath);
        var folder = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        _data = LoadOrSeed();
    }

    /// <summary>Đường dẫn file dữ liệu đang dùng.</summary>
    public string FilePath => _filePath;

    /// <summary>Đọc dữ liệu (không ghi file).</summary>
    public T Read<T>(Func<BankData, T> reader)
    {
        lock (_lock) return reader(_data);
    }

    /// <summary>Sửa dữ liệu rồi lưu file. Nếu hàm trả về saveChanges = false thì không lưu.</summary>
    public T Write<T>(Func<BankData, (T result, bool saveChanges)> writer)
    {
        lock (_lock)
        {
            var (result, save) = writer(_data);
            if (save) Save();
            return result;
        }
    }

    /// <summary>Xóa hết và tạo lại dữ liệu mẫu.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _data = SeedData.Create(DateTime.Now);
            Save();
        }
    }

    private BankData LoadOrSeed()
    {
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                var data = JsonSerializer.Deserialize<BankData>(json, FileJson);
                if (data != null && data.Version == 1 && data.Accounts.Count > 0)
                {
                    _logger.LogInformation("Đã đọc dữ liệu từ {Path}", _filePath);
                    return data;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "File dữ liệu bị lỗi, sẽ tạo lại dữ liệu mẫu");
        }

        var seeded = SeedData.Create(DateTime.Now);
        _data = seeded;
        Save();
        _logger.LogInformation("Đã tạo dữ liệu mẫu tại {Path}", _filePath);
        return seeded;
    }

    private void Save()
    {
        // Ghi ra file tạm rồi thay thế để không bị hỏng file nếu tắt ngang
        var tmp = _filePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(_data, FileJson));
        File.Move(tmp, _filePath, overwrite: true);
    }
}
