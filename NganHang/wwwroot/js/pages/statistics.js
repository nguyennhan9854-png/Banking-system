/* =========================================================
   TRANG THỐNG KÊ (thong-ke.html) – Luân, tuần 3
   Số liệu do server C# tính (BankService.GetStatistics):
   số dư đầu kỳ, tiền vào, tiền ra, tiền lãi, số dư cuối kỳ cho từng kỳ và từng tài khoản.
   ========================================================= */
App.boot(function () {
  const U = App.utils;
  const S = App.store;

  App.layout.render("statistics");

  const els = {
    presets: U.qs("[data-st-presets]"),
    from: U.qs("[data-st-from]"),
    to: U.qs("[data-st-to]"),
    group: U.qs("[data-st-group]"),
    acc: U.qs("[data-st-acc]"),
    exportBtn: U.qs("[data-st-export]"),
    error: U.qs("[data-st-error]"),
    stats: U.qs("[data-st-stats]"),
    chart: U.qs("[data-st-chart]"),
    range: U.qs("[data-st-range]"),
    periods: U.qs("[data-st-periods]"),
    check: U.qs("[data-st-check]"),
    types: U.qs("[data-st-types]"),
    accounts: U.qs("[data-st-accounts]")
  };

  const COLORS = { in: "#16A34A", out: "#EF4444", interest: "#7C3AED" };
  const TYPE_ROWS = [
    { label: "Nạp tiền", types: ["deposit", "open"], tone: "in", icon: "wallet" },
    { label: "Nhận tiền", types: ["transfer_in"], tone: "in", icon: "arrowDownLeft" },
    { label: "Cộng lãi", types: ["interest"], tone: "interest", icon: "percent" },
    { label: "Rút tiền", types: ["withdraw"], tone: "out", icon: "arrowUp" },
    { label: "Chuyển khoản", types: ["transfer_out"], tone: "out", icon: "arrowUpRight" },
    { label: "Hóa đơn / Mua sắm", types: ["bill", "shopping"], tone: "out", icon: "bill" }
  ];

  let data = null;
  let loading = 0;

  /* ---------- Khoảng thời gian ---------- */
  function today() { return U.startOfDay(new Date()); }
  function parseInput(v) {
    const m = /^(\d{4})-(\d{2})-(\d{2})$/.exec(v || "");
    return m ? new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3])) : null;
  }

  function applyPreset(name) {
    const t = today();
    let from = t;
    let group = "month";
    if (name === "month") { from = new Date(t.getFullYear(), t.getMonth(), 1); group = "day"; }
    else if (name === "quarter") { from = new Date(t.getFullYear(), Math.floor(t.getMonth() / 3) * 3, 1); group = "month"; }
    else if (name === "6m") { from = new Date(t.getFullYear(), t.getMonth() - 5, 1); group = "month"; }
    else if (name === "year") { from = new Date(t.getFullYear(), 0, 1); group = "month"; }
    if (name !== "custom") {
      els.from.value = U.toInputDate(from);
      els.to.value = U.toInputDate(t);
      els.group.value = group;
    }
    U.qsa(".tab", els.presets).forEach(function (b) {
      const on = b.dataset.preset === name;
      b.classList.toggle("is-active", on);
      b.setAttribute("aria-selected", on ? "true" : "false");
    });
  }

  function renderAccountFilter() {
    const cur = els.acc.value;
    els.acc.innerHTML = '<option value="">Tất cả tài khoản</option>' + S.getAccounts().map(function (a) {
      return '<option value="' + a.id + '">' + U.escapeHtml(a.name + " (" + U.maskNumber(a.accountNumber) + ")") + "</option>";
    }).join("");
    if (cur && S.getAccount(cur)) els.acc.value = cur;
  }

  /* ---------- Gọi server ---------- */
  async function load() {
    const from = parseInput(els.from.value);
    const to = parseInput(els.to.value);
    if (!from || !to) { showError("Vui lòng chọn đủ từ ngày và đến ngày."); return; }
    if (from > to) { showError("Từ ngày phải trước hoặc bằng đến ngày."); return; }
    const my = ++loading;
    els.stats.classList.add("is-loading");
    const res = await S.getStatistics({ from: from, to: to, groupBy: els.group.value, accountId: els.acc.value });
    if (my !== loading) return;            // đã có yêu cầu mới hơn
    els.stats.classList.remove("is-loading");
    if (!res.ok) { showError(res.error); return; }
    showError("");
    data = res.data;
    renderAll();
  }

  function showError(msg) { els.error.textContent = msg || ""; }

  /* ---------- Tổng quan ---------- */
  function renderStats() {
    const s = data.summary;
    const card = function (tone, icon, label, value, sub, cls) {
      return '<div class="st-stat st-stat--' + tone + '"><span class="st-stat__ic">' + App.icon(icon, 24) + "</span>" +
        '<div style="min-width:0"><div class="st-stat__label">' + label + '</div><div class="st-stat__value' + (cls ? " " + cls : "") + '">' + value +
        '</div><div class="st-stat__sub">' + sub + "</div></div></div>";
    };
    const count = function (types) {
      return types.reduce(function (n, t) { return n + (s.byType[t] ? s.byType[t].count : 0); }, 0);
    };
    els.stats.innerHTML =
      card("green", "arrowDownLeft", "Tổng tiền vào", U.formatVND(s.totalIn), count(["deposit", "open", "transfer_in", "interest"]) + " giao dịch") +
      card("red", "arrowUpRight", "Tổng tiền ra", U.formatVND(s.totalOut), count(["withdraw", "transfer_out", "bill", "shopping"]) + " giao dịch") +
      card("blue", "scale", "Chênh lệch", (s.net > 0 ? "+" : "") + U.formatVND(s.net),
        "Số dư " + U.formatVND(s.openingBalance) + " → " + U.formatVND(s.closingBalance), s.net < 0 ? "money-out" : "money-in") +
      card("purple", "percent", "Tiền lãi nhận được", U.formatVND(s.interestReceived), (s.byType.interest ? s.byType.interest.count : 0) + " lần cộng lãi");
    els.range.textContent = U.formatDate(data.from) + " – " + U.formatDate(data.to);
  }

  /* ---------- Biểu đồ ---------- */
  function shortLabel(label) {
    // "T10/2026" -> "T10" khi tất cả kỳ cùng năm
    const years = {};
    data.periods.forEach(function (p) { years[new Date(p.start).getFullYear()] = true; });
    if (Object.keys(years).length === 1) return label.replace(/\/\d{4}$/, "");
    return label.replace(/\/(\d{2})(\d{2})$/, "/$2");
  }

  function renderChart() {
    App.charts.bars(els.chart, {
      height: 260,
      ariaLabel: "Biểu đồ tiền vào, tiền ra, tiền lãi theo kỳ",
      series: [
        { name: "Tiền vào", color: COLORS.in },
        { name: "Tiền ra", color: COLORS.out },
        { name: "Tiền lãi", color: COLORS.interest }
      ],
      groups: data.periods.map(function (p) {
        return {
          label: shortLabel(p.label),
          title: p.label + " (" + U.formatDate(p.start) + " – " + U.formatDate(p.end) + ")",
          values: [p.totalIn, p.totalOut, p.interest]
        };
      })
    });
  }

  /* ---------- Bảng từng kỳ ---------- */
  function money(v, cls) {
    return '<td class="t-right' + (cls ? " " + cls : "") + '">' + (v ? U.formatVND(v) : '<span class="st-zero">0 đ</span>') + "</td>";
  }

  function renderPeriods() {
    const list = data.periods;
    // Tự kiểm tra lại công thức cân đối ngay trên giao diện
    const balanced = list.every(function (p) { return p.openingBalance + p.totalIn - p.totalOut === p.closingBalance; });
    els.check.innerHTML = balanced
      ? '<span class="badge badge--success">' + App.icon("check", 14, 3) + "Cân đối: cuối kỳ = đầu kỳ + vào − ra</span>"
      : '<span class="badge badge--failed">Số liệu không cân đối</span>';
    if (!list.length) { els.periods.innerHTML = '<div class="empty">Không có kỳ nào.</div>'; return; }
    const sum = function (k) { return list.reduce(function (s, p) { return s + p[k]; }, 0); };
    els.periods.innerHTML =
      '<table class="table st-table"><thead><tr><th scope="col">Kỳ</th><th scope="col">Thời gian</th>' +
      '<th scope="col" class="t-right">Đầu kỳ</th><th scope="col" class="t-right">Tiền vào</th><th scope="col" class="t-right">Tiền ra</th>' +
      '<th scope="col" class="t-right">Tiền lãi</th><th scope="col" class="t-right">Cuối kỳ</th><th scope="col" class="t-center">Số GD</th></tr></thead><tbody>' +
      list.slice().reverse().map(function (p) {
        return "<tr><td><strong>" + U.escapeHtml(p.label) + "</strong></td>" +
          '<td class="st-dates">' + U.formatDate(p.start) + " – " + U.formatDate(p.end) + "</td>" +
          '<td class="t-right">' + U.formatVND(p.openingBalance) + "</td>" +
          money(p.totalIn, "money-in") + money(p.totalOut, "money-out") + money(p.interest, "st-interest") +
          '<td class="t-right"><strong>' + U.formatVND(p.closingBalance) + "</strong></td>" +
          '<td class="t-center">' + p.count + "</td></tr>";
      }).join("") +
      '</tbody><tfoot><tr><th scope="row" colspan="2">Cả khoảng</th>' +
      '<td class="t-right">' + U.formatVND(data.summary.openingBalance) + "</td>" +
      money(sum("totalIn"), "money-in") + money(sum("totalOut"), "money-out") + money(sum("interest"), "st-interest") +
      '<td class="t-right"><strong>' + U.formatVND(data.summary.closingBalance) + "</strong></td>" +
      '<td class="t-center">' + sum("count") + "</td></tr></tfoot></table>";
  }

  /* ---------- Theo loại ---------- */
  function renderTypes() {
    const by = data.summary.byType;
    const rows = TYPE_ROWS.map(function (r) {
      return {
        r: r,
        count: r.types.reduce(function (n, t) { return n + (by[t] ? by[t].count : 0); }, 0),
        amount: r.types.reduce(function (n, t) { return n + (by[t] ? by[t].amount : 0); }, 0)
      };
    });
    const max = Math.max.apply(null, rows.map(function (x) { return x.amount; }).concat([1]));
    els.types.innerHTML = rows.map(function (x) {
      return '<li class="st-type st-type--' + x.r.tone + '">' +
        '<span class="st-type__ic">' + App.icon(x.r.icon, 18, 2.4) + "</span>" +
        '<div class="st-type__main"><div class="st-type__row"><span>' + x.r.label + ' <small>' + x.count + " GD</small></span><strong>" +
        U.formatVND(x.amount) + "</strong></div>" +
        '<div class="st-type__bar"><span style="width:' + Math.round((x.amount / max) * 100) + '%"></span></div></div></li>';
    }).join("") +
      '<li class="st-type st-type--failed"><span class="st-type__ic">' + App.icon("alert", 18, 2.4) + "</span>" +
      '<div class="st-type__main"><div class="st-type__row"><span>Thất bại (đã hoàn tác)</span><strong>' + data.summary.failedCount + " GD</strong></div>" +
      (data.summary.failedCount ? '<a class="st-type__link" href="giao-dich.html?status=failed">Xem các giao dịch thất bại</a>' : "<small>Không tính vào tiền vào/ra</small>") +
      "</div></li>";
  }

  /* ---------- Theo tài khoản ---------- */
  function renderAccounts() {
    els.accounts.innerHTML =
      '<table class="table st-table"><thead><tr><th scope="col">Tài khoản</th>' +
      '<th scope="col" class="t-right">Đầu kỳ</th><th scope="col" class="t-right">Tiền vào</th><th scope="col" class="t-right">Tiền ra</th>' +
      '<th scope="col" class="t-right">Tiền lãi</th><th scope="col" class="t-right">Cuối kỳ</th><th scope="col" class="t-center">Số GD</th></tr></thead><tbody>' +
      data.accounts.map(function (a) {
        return '<tr><td class="cell-2l cell-acc">' + U.escapeHtml(a.name) + "<small>" + U.maskNumber(a.accountNumber) + "</small></td>" +
          '<td class="t-right">' + U.formatVND(a.openingBalance) + "</td>" +
          money(a.totalIn, "money-in") + money(a.totalOut, "money-out") + money(a.interest, "st-interest") +
          '<td class="t-right"><strong>' + U.formatVND(a.closingBalance) + "</strong></td>" +
          '<td class="t-center">' + a.count + "</td></tr>";
      }).join("") + "</tbody></table>";
  }

  function renderAll() {
    if (!data) return;
    renderStats();
    renderChart();
    renderPeriods();
    renderTypes();
    renderAccounts();
  }

  /* ---------- Xuất CSV (mở được bằng Excel) ---------- */
  function exportCsv() {
    if (!data) return;
    const cell = function (v) { const s = String(v); return /[",;\n]/.test(s) ? '"' + s.replace(/"/g, '""') + '"' : s; };
    const lines = [["Kỳ", "Từ ngày", "Đến ngày", "Số dư đầu kỳ", "Tiền vào", "Tiền ra", "Tiền lãi", "Số dư cuối kỳ", "Số giao dịch"]];
    data.periods.forEach(function (p) {
      lines.push([p.label, U.formatDate(p.start), U.formatDate(p.end), p.openingBalance, p.totalIn, p.totalOut, p.interest, p.closingBalance, p.count]);
    });
    const s = data.summary;
    lines.push(["Cả khoảng", U.formatDate(data.from), U.formatDate(data.to), s.openingBalance, s.totalIn, s.totalOut, s.interestReceived, s.closingBalance, s.transactionCount]);
    const csv = "﻿" + lines.map(function (r) { return r.map(cell).join(","); }).join("\r\n");
    const url = URL.createObjectURL(new Blob([csv], { type: "text/csv;charset=utf-8" }));
    const a = document.createElement("a");
    a.href = url;
    a.download = "thong-ke-" + U.toInputDate(new Date(data.from)) + "-den-" + U.toInputDate(new Date(data.to)) + ".csv";
    document.body.appendChild(a);
    a.click();
    a.remove();
    setTimeout(function () { URL.revokeObjectURL(url); }, 1000);
    U.showToast("Đã xuất file " + a.download);
  }

  /* ---------- Sự kiện ---------- */
  els.presets.addEventListener("click", function (e) {
    const b = e.target.closest("[data-preset]");
    if (!b) return;
    applyPreset(b.dataset.preset);
    if (b.dataset.preset === "custom") { els.from.focus(); return; }
    load();
  });
  [els.from, els.to].forEach(function (input) {
    input.addEventListener("change", function () { applyPreset("custom"); load(); });
  });
  els.group.addEventListener("change", load);
  els.acc.addEventListener("change", load);
  els.exportBtn.addEventListener("click", exportCsv);

  let t = 0;
  S.subscribe(function (reason) {
    if (reason === "notification" || reason === "settings") return;
    renderAccountFilter();
    clearTimeout(t);
    t = setTimeout(load, 150);
  });

  renderAccountFilter();
  applyPreset("6m");
  load();
});
