/* =========================================================
   TRANG CỘNG LÃI (cong-lai.html)
   Số liệu lãi do server C# tính (BankService: InterestOf, ApplyDueInterest).
   ========================================================= */
App.boot(function () {
  const U = App.utils;
  const S = App.store;

  App.layout.render("interest");

  const els = {
    stats: U.qs("[data-it-stats]"),
    accounts: U.qs("[data-it-accounts]"),
    history: U.qs("[data-it-history]"),
    run: U.qs("[data-it-run]")
  };

  function savings() {
    return S.getAccounts().filter(function (a) { return a.type === "saving" && a.interest; });
  }

  /* ---------- Tổng quan ---------- */
  function renderStats() {
    const list = savings();
    const received = list.reduce(function (s, a) { return s + a.interest.totalReceived; }, 0);
    const accrued = list.reduce(function (s, a) { return s + a.interest.accruedInterest; }, 0);
    const next = list.map(function (a) { return new Date(a.interest.nextInterestDate); })
      .sort(function (x, y) { return x - y; })[0];
    const stat = function (tone, icon, label, value, sub) {
      return '<div class="it-stat it-stat--' + tone + '"><span class="it-stat__ic">' + App.icon(icon, 24) + "</span>" +
        '<div style="min-width:0"><div class="it-stat__label">' + label + '</div><div class="it-stat__value">' + value +
        '</div><div class="it-stat__sub">' + sub + "</div></div></div>";
    };
    els.stats.innerHTML =
      stat("purple", "percent", "Tổng lãi đã nhận", U.formatVND(received), "Từ tất cả tài khoản tiết kiệm") +
      stat("green", "trendUp", "Lãi tạm tính tháng này", U.formatVND(accrued), "Tính đến hết ngày hôm qua") +
      stat("blue", "calendar", "Lần cộng lãi tự động tiếp theo", next ? U.formatDate(next) : "—",
        next ? "Còn " + Math.max(U.daysBetween(new Date(), next), 0) + " ngày" : "Chưa có tài khoản tiết kiệm");
  }

  /* ---------- Thẻ từng tài khoản ---------- */
  function accountCard(a) {
    const it = a.interest;
    const pct = it.daysInPeriod ? Math.min(100, Math.round((it.daysElapsed / it.daysInPeriod) * 100)) : 0;
    const fig = function (label, value, cls) {
      return '<div class="it-fig"><div class="it-fig__label">' + label + '</div><div class="it-fig__value' + (cls ? " " + cls : "") + '">' + value + "</div></div>";
    };
    return (
      '<article class="it-acc" id="it-' + a.id + '" data-acc="' + a.id + '">' +
        '<div class="it-acc__head">' +
          '<span class="it-acc__icon"><img src="assets/images/piggy.png" alt=""></span>' +
          "<div style=\"min-width:0\"><div class=\"it-acc__title\">" + U.escapeHtml(a.name) + "</div>" +
          '<div class="it-acc__num">' + a.accountNumber + " · Kỳ hạn " + a.termMonths + " tháng</div></div>" +
          '<span class="it-acc__rate">' + U.formatPercent(a.interestRate, 1) + "/năm</span>" +
        "</div>" +
        '<div class="it-period">' +
          '<div class="it-period__row"><span>Kỳ lãi: <strong>' + U.formatDate(it.periodStart) + " – " + U.formatDate(U.addDays(it.nextInterestDate, -1)) +
          "</strong></span><span><strong>" + it.daysElapsed + "/" + it.daysInPeriod + "</strong> ngày</span></div>" +
          '<div class="it-bar" role="progressbar" aria-label="Tiến độ kỳ lãi" aria-valuemin="0" aria-valuemax="100" aria-valuenow="' + pct + '"><span style="width:' + pct + '%"></span></div>' +
          '<div class="it-period__row"><span>Tự động cộng lãi vào ngày <strong>' + U.formatDate(it.nextInterestDate) + "</strong></span></div>" +
        "</div>" +
        '<div class="it-figs">' +
          fig("Số dư hiện tại", U.formatVND(a.balance)) +
          fig("Lãi tạm tính", (it.accruedInterest > 0 ? "+" : "") + U.formatVND(it.accruedInterest), "is-green") +
          fig("Lãi dự kiến tháng này", U.formatVND(it.projectedInterest)) +
        "</div>" +
        '<div class="it-acc__actions">' +
          '<button type="button" class="btn btn--green" data-apply="' + a.id + '"' + (it.accruedInterest > 0 ? "" : ' disabled title="Chưa có tiền lãi để nhận"') + ">" +
            App.icon("percent", 18, 2.4) + "Nhận lãi ngay</button>" +
          '<button type="button" class="btn btn--outline" data-deposit="' + a.id + '">' + App.icon("wallet", 18) + "Nạp thêm</button>" +
        "</div>" +
      "</article>"
    );
  }

  function renderAccounts() {
    const list = savings();
    if (!list.length) {
      els.accounts.innerHTML =
        '<div class="card it-empty"><h2 class="card__title" style="justify-content:center">Bạn chưa có tài khoản tiết kiệm</h2>' +
        "<p>Mở tài khoản tiết kiệm để bắt đầu nhận lãi hằng tháng.</p>" +
        '<a class="btn btn--primary" href="tai-khoan.html#mo-tai-khoan">' + App.icon("plus", 18, 2.6) + "Mở tài khoản tiết kiệm</a></div>";
      return;
    }
    els.accounts.innerHTML = list.map(accountCard).join("");
  }

  /* ---------- Lịch sử cộng lãi ---------- */
  function renderHistory() {
    const list = S.getTransactions().filter(function (t) { return t.type === "interest"; });
    if (!list.length) {
      els.history.innerHTML = '<div class="empty">Chưa có lần cộng lãi nào.</div>';
      return;
    }
    els.history.innerHTML =
      '<table class="table"><thead><tr><th scope="col">Thời gian</th><th scope="col">Tài khoản</th><th scope="col">Nội dung</th>' +
      '<th scope="col" class="t-right">Tiền lãi</th><th scope="col" class="t-right">Số dư sau</th></tr></thead><tbody>' +
      list.slice(0, 10).map(function (tx) {
        const acc = S.getAccount(tx.accountId);
        const period = (tx.subtitle || "").split(" · ")[1] || "";
        return '<tr class="is-clickable" tabindex="0" data-tx="' + tx.id + '">' +
          '<td class="cell-time">' + U.formatDate(tx.date) + "<br>" + U.formatTime(tx.date) + "</td>" +
          '<td class="cell-2l cell-acc">' + (acc ? U.escapeHtml(acc.name) : "—") + "<small>" + (acc ? U.maskNumber(acc.accountNumber) : "") + "</small></td>" +
          '<td class="cell-2l">' + U.escapeHtml(tx.title) + (period ? "<small>Kỳ " + U.escapeHtml(period) + "</small>" : "") + "</td>" +
          '<td class="t-right cell-amount money-in">' + U.formatSigned(tx.amount) + "</td>" +
          '<td class="t-right cell-balance">' + U.formatVND(tx.balanceAfter) + "</td></tr>";
      }).join("") + "</tbody></table>";
  }

  function renderAll() {
    renderStats();
    renderAccounts();
    renderHistory();
  }

  /* ---------- Sự kiện ---------- */
  els.accounts.addEventListener("click", async function (e) {
    const dep = e.target.closest("[data-deposit]");
    if (dep) { App.money.openDeposit(dep.dataset.deposit); return; }
    const btn = e.target.closest("[data-apply]");
    if (!btn || btn.disabled) return;
    const id = btn.dataset.apply;
    btn.disabled = true;
    btn.innerHTML = '<span class="spinner"></span>Đang cộng lãi...';
    const res = await S.applyInterest(id);
    if (!res.ok) {
      U.showToast(res.error, "error");
      renderAll();
      return;
    }
    U.showToast("Đã cộng " + U.formatVND(res.total) + " tiền lãi");
    const card = document.getElementById("it-" + id);
    if (card) { card.classList.add("is-new"); setTimeout(function () { card.classList.remove("is-new"); }, 1700); }
  });
  els.run.addEventListener("click", async function () {
    els.run.disabled = true;
    const res = await S.runInterest();
    els.run.disabled = false;
    if (!res.ok) { U.showToast(res.error, "error"); return; }
    if (res.transactions.length) U.showToast("Đã tự động cộng lãi " + res.transactions.length + " kỳ, tổng " + U.formatVND(res.total));
    else U.showToast("Chưa có kỳ lãi nào đến hạn. Hệ thống sẽ tự cộng khi đủ 1 tháng.", "info");
  });
  els.history.addEventListener("click", function (e) {
    const row = e.target.closest("[data-tx]");
    if (row) App.money.openTransaction(row.dataset.tx);
  });
  els.history.addEventListener("keydown", function (e) {
    const row = e.target.closest("[data-tx]");
    if (row && (e.key === "Enter" || e.key === " ")) { e.preventDefault(); App.money.openTransaction(row.dataset.tx); }
  });

  renderAll();
  S.subscribe(function (reason) {
    if (reason === "notification" || reason === "settings") return;
    renderAll();
  });
});
