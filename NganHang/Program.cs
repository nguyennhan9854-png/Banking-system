using NganHang.Data;
using NganHang.Services;

var builder = WebApplication.CreateBuilder(args);

// Controller trả JSON dạng camelCase (accountNumber, balanceAfter...) để JavaScript dùng trực tiếp
builder.Services.AddControllers();

// Kho dữ liệu (file JSON) và nghiệp vụ ngân hàng: dùng chung 1 bản cho toàn ứng dụng
builder.Services.AddSingleton(sp => new BankRepository(
    Path.Combine(builder.Environment.ContentRootPath, "App_Data", "bank-data.json"),
    sp.GetRequiredService<ILogger<BankRepository>>()));
builder.Services.AddSingleton<BankService>();

// Tự động cộng lãi tiết kiệm hằng tháng (chạy nền, kiểm tra mỗi giờ)
builder.Services.AddHostedService<InterestBackgroundService>();

var app = builder.Build();

// Phục vụ giao diện trong thư mục wwwroot (index.html là trang mặc định)
app.UseDefaultFiles();
app.UseStaticFiles();

// Các API nằm dưới /api/...
app.MapControllers();

app.Run();
