using Microsoft.AspNetCore.Mvc;
using NganHang.Models;
using NganHang.Services;

namespace NganHang.Controllers;

/// <summary>Lịch sử giao dịch (mới nhất trước, có số dư sau giao dịch).</summary>
[ApiController]
[Route("api/transactions")]
public class TransactionsController : ControllerBase
{
    private readonly BankService _bank;

    public TransactionsController(BankService bank) => _bank = bank;

    /// <summary>GET /api/transactions?accountId=acc-saving&amp;limit=5</summary>
    [HttpGet]
    public ActionResult<List<TransactionDto>> Get([FromQuery] string? accountId, [FromQuery] int? limit)
        => _bank.GetTransactions(accountId, limit);
}
