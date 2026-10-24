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
    private readonly IFaultInjector _faults;
    private BankData _data;

    private static readonly JsonSerializerOptions FileJson = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Giữ nguyên chữ tiếng Việt trong file cho dễ đọc
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <param name="filePath">Đường dẫn file JSON. Web dùng App_Data/bank-data.json; unit test dùng file tạm riêng.</param>
    /// <param name="faults">Điểm cắm lỗi giả lập cho unit test; chạy thật để trống.</param>
    public BankRepository(string filePath, ILogger<BankRepository> logger, IFaultInjector? faults = null)
    {
        _logger = logger;
        _faults = faults ?? NoFaults.Instance;
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

    /// <summary>
    /// Sửa dữ liệu rồi lưu file – TẤT CẢ HOẶC KHÔNG GÌ CẢ (rollback).
    /// Trước khi sửa, chụp lại một bản sao dữ liệu. Nếu hàm sửa ném lỗi giữa chừng
    /// (ví dụ đã trừ tài khoản nguồn nhưng chưa cộng tài khoản nhận) hoặc lưu file thất bại,
    /// dữ liệu trong bộ nhớ được trả về đúng bản sao đó rồi ném lỗi tiếp cho BankService xử lý.
    /// File trên đĩa không bị ghi dở vì chỉ được thay khi file tạm đã ghi xong.
    /// Nếu hàm trả về saveChanges = false thì không lưu.
    /// </summary>
    public T Write<T>(Func<BankData, (T result, bool saveChanges)> writer)
    {
        lock (_lock)
        {
            var snapshot = Clone(_data);
            try
            {
                var (result, save) = writer(_data);
                if (save) Save();
                return result;
            }
            catch
            {
                _data = snapshot;   // ROLLBACK: bỏ mọi thay đổi dở dang
                _logger.LogWarning("Thao tác ghi dữ liệu bị lỗi giữa chừng, đã khôi phục dữ liệu về trước thao tác");
                throw;
            }
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

    /// <summary>Đường dẫn bản sao lưu file hỏng gần nhất (null nếu lần khởi động này file vẫn tốt).</summary>
    public string? CorruptBackupPath { get; private set; }

    private BankData LoadOrSeed()
    {
        if (File.Exists(_filePath))
        {
            try
            {
                var json = File.ReadAllText(_filePath);
                var data = JsonSerializer.Deserialize<BankData>(json, FileJson);
                if (data != null && data.Version == 1 && data.Accounts.Count > 0)
                {
                    _logger.LogInformation("Đã đọc dữ liệu từ {Path}", _filePath);
                    return data;
                }
                _logger.LogWarning("File dữ liệu không đúng định dạng, sẽ tạo lại dữ liệu mẫu");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "File dữ liệu bị lỗi, sẽ tạo lại dữ liệu mẫu");
            }
            BackupCorruptFile();
        }

        var seeded = SeedData.Create(DateTime.Now);
        _data = seeded;
        Save();
        _logger.LogInformation("Đã tạo dữ liệu mẫu tại {Path}", _filePath);
        return seeded;
    }

    /// <summary>
    /// File hỏng KHÔNG bị ghi đè mất: đổi tên thành bank-data.json.corrupt-yyyyMMdd-HHmmss
    /// để còn kiểm tra / khôi phục bằng tay, rồi mới tạo dữ liệu mẫu mới.
    /// </summary>
    private void BackupCorruptFile()
    {
        try
        {
            var backup = _filePath + ".corrupt-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            File.Move(_filePath, backup, overwrite: true);
            CorruptBackupPath = backup;
            _logger.LogWarning("Đã sao lưu file dữ liệu hỏng tại {Path}", backup);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không sao lưu được file dữ liệu hỏng");
        }
    }

    private void Save()
    {
        // Ghi ra file tạm rồi thay thế để không bị hỏng file nếu tắt ngang
        var tmp = _filePath + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonSerializer.Serialize(_data, FileJson));
            _faults.Hit(FaultPoints.SaveFile);   // giả lập lỗi: đã ghi file tạm nhưng chưa kịp thay file chính
            File.Move(tmp, _filePath, overwrite: true);
        }
        catch
        {
            // Lưu thất bại: xóa file tạm ghi dở, file chính vẫn là bản cũ
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { /* bỏ qua */ }
            throw;
        }
    }

    /// <summary>Bản sao độc lập của toàn bộ dữ liệu (dùng để hoàn tác).</summary>
    private static BankData Clone(BankData data) =>
        JsonSerializer.Deserialize<BankData>(JsonSerializer.SerializeToUtf8Bytes(data, FileJson), FileJson)!;
}
