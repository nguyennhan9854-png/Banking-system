/* =========================================================
   TRANG CHỦ (index.html)
   ========================================================= */
App.boot(function () {
  const U = App.utils;
  const S = App.store;

  App.layout.render("home");

  const els = {
    accounts: U.qs("[data-home-accounts]"),
    chart: U.qs("[data-home-chart]"),
    range: U.qs("[data-chart-range]"),
    recent: U.qs("[data-home-recent]"),
    donut: U.qs("[data-donut]"),
    legend: U.qs("[data-legend]")
  };

  /* ---------- Lời chào: tên lấy từ dữ liệu người dùng ---------- */
  function renderGreeting() {
    U.qs("[data-user-name]").textContent = S.getUser().name;
  }

  /* ---------- Hàng 1: 2 thẻ tài khoản ---------- */
  function trendHtml(g) {
    if (g === null || !isFinite(g)) return '<span class="trend trend--up" style="visibility:hidden">0%</span>';
    const up = g >= 0;
    const val = Math.abs(g) < 10 ? Math.abs(g).toFixed(1).replace(".0", "") : Math.round(Math.abs(g));
    return '<span class="trend ' + (up ? "trend--up" : "trend--down") + '">' +
      App.icon(up ? "arrowUp" : "arrowDown", 16, 2.6) + String(val).replace(".", ",") + "%</span>";
  }

  function accountCard(acc) {
    const isPay = acc.type === "payment";
    return (
      '<article class="home-acc ' + (isPay ? "home-acc--pay" : "home-acc--save") + '">' +
        '<span class="home-acc__icon">' + App.icon(isPay ? "wallet" : "piggy", 32) + "</span>" +
        '<div class="home-acc__body">' +
          '<h3 class="home-acc__title">' + U.escapeHtml(acc.name) + "</h3>" +
          '<div class="home-acc__num">STK: ' + acc.accountNumber +
            '<button type="button" class="copy-btn" data-copy="' + acc.accountNumber.replace(/\s/g, "") + '" aria-label="Sao chép số tài khoản ' + U.escapeHtml(acc.name) + '">' + App.icon("copy", 16) + "</button></div>" +
          '<div class="home-acc__bal' + (acc.balance < 0 ? " is-negative" : "") + '" data-balance="' + acc.balance + '">' + U.formatVND(acc.balance) + "</div>" +
          '<div class="home-acc__trend">' + trendHtml(S.growth(acc.id)) + "<span>So với tháng trước</span></div>" +
        "</div>" +
        '<a class="round-btn home-acc__go" href="tai-khoan.html#' + acc.id + '" aria-label="Xem chi tiết ' + U.escapeHtml(acc.name) + '">' + App.icon("chevronRight", 18, 2.4) + "</a>" +
        '<img class="home-acc__art" src="assets/images/' + (isPay ? "card-orange.png" : "sprout.png") + '" alt="">' +
      "</article>"
    );
  }

  function renderAccounts(animate) {
    const list = [S.getPrimary("saving"), S.getPrimary("payment")].filter(Boolean);
    els.accounts.innerHTML = list.map(accountCard).join("");
    if (animate) {
      U.qsa("[data-balance]", els.accounts).forEach(function (el) {
        U.countUp(el, Number(el.dataset.balance), 1000);
      });
    }
  }

  /* ---------- Biểu đồ biến động số dư (tổng các tài khoản) ---------- */
  function renderChart(animate) {
    const months = Number(els.range.value) || 6;
    const now = new Date();
    const from = new Date(now.getFullYear(), now.getMonth() - (months - 1), 1);
    const ids = S.getAccounts().map(function (a) { return a.id; });
    const token = ++chartToken;
    S.balanceSeries(ids, from, now, "monthly").then(function (points) {
      if (token !== chartToken) return; // đã có yêu cầu mới hơn
      App.charts.line(els.chart, {
        points: points,
        height: 240,
        animate: animate,
        ariaLabel: "Biến động tổng số dư " + months + " tháng gần nhất"
      });
    }).catch(function (err) {
      els.chart.innerHTML = '<div class="empty">' + U.escapeHtml(err.message) + "</div>";
    });
  }
  let chartToken = 0;

  /* ---------- Giao dịch gần đây ---------- */
  function renderRecent() {
    const list = S.getTransactions({ limit: 4 });
    if (!list.length) {
      els.recent.innerHTML = '<div class="empty">Chưa có giao dịch nào. Hãy thử nạp tiền vào tài khoản.</div>';
      return;
    }
    els.recent.innerHTML =
      '<table class="table"><thead><tr><th scope="col">Thời gian</th><th scope="col">Nội dung giao dịch</th>' +
      '<th scope="col" class="t-right">Số tiền</th><th scope="col" class="t-center">Trạng thái</th></tr></thead><tbody>' +
      list.map(function (tx) {
        const isIn = S.TX_TYPES[tx.type].dir === "in";
        return '<tr class="is-clickable" tabindex="0" data-tx="' + tx.id + '" aria-label="Xem chi tiết giao dịch ' + U.escapeHtml(tx.title) + '">' +
          '<td class="cell-time">' + U.formatDate(tx.date) + "<br>" + U.formatTime(tx.date) + "</td>" +
          '<td><div class="cell-desc">' + App.money.txIconHtml(tx) + "<span>" + U.escapeHtml(tx.title) + "</span></div></td>" +
          '<td class="t-right cell-amount ' + (isIn ? "money-in" : "money-out") + '">' + U.formatSigned(isIn ? tx.amount : -tx.amount) + "</td>" +
          '<td class="t-center">' + App.money.statusBadge(tx.status) + "</td></tr>";
      }).join("") + "</tbody></table>";
  }

  /* ---------- Tổng quan tài khoản (donut) ---------- */
  function renderOverview() {
    const saving = S.totalBalance("saving");
    const payment = S.totalBalance("payment");
    const total = saving + payment;
    const positive = Math.max(saving, 0) + Math.max(payment, 0);
    const pct = function (v) { return positive > 0 && v >= 0 ? "(" + U.formatPercent((v / positive) * 100) + ")" : "(—)"; };
    App.charts.donut(els.donut, {
      size: 148, thickness: 12,
      segments: [
        { label: "Tiết kiệm", value: Math.max(saving, 0), color: "#1E5BD8" },
        { label: "Thanh toán", value: Math.max(payment, 0), color: "#93C5FD" }
      ],
      centerHtml: "<small>Tổng cộng</small><strong>" + U.formatVND(total) + "</strong>",
      ariaLabel: "Tổng số dư " + U.formatVND(total)
    });
    els.legend.innerHTML =
      '<li><i style="background:#1E5BD8"></i><span>Tiết kiệm</span><strong>' + U.formatVND(saving) + "</strong><em>" + pct(saving) + "</em></li>" +
      '<li><i style="background:#93C5FD"></i><span>Thanh toán</span><strong' + (payment < 0 ? ' class="money-out"' : "") + ">" + U.formatVND(payment) + "</strong><em>" + pct(payment) + "</em></li>";
  }

  /* ---------- Gợi ý tài chính (xoay vòng) ---------- */
  const TIPS = [
    {
      title: "Tích lũy hôm nay<br>Vững vàng tương lai",
      desc: "Duy trì thói quen tiết kiệm mỗi tháng để đạt được những mục tiêu lớn hơn trong cuộc sống.",
      cta: "Bắt đầu tiết kiệm ngay",
      run: function () { const s = S.getPrimary("saving"); App.money.openDeposit(s ? s.id : null); }
    },
    {
      title: "Kỳ hạn dài hơn<br>Lãi suất tốt hơn",
      desc: "Sổ tiết kiệm kỳ hạn 12 tháng có lãi suất 6,3%/năm, cao hơn kỳ hạn 6 tháng.",
      cta: "Mở sổ tiết kiệm mới",
      run: function () { window.location.href = "tai-khoan.html#mo-tai-khoan"; }
    },
    {
      title: "Chi tiêu thông minh<br>Chủ động dòng tiền",
      desc: "Xem lại các khoản chi trong tháng để cân đối ngân sách và tránh dùng thấu chi.",
      cta: "Xem lịch sử giao dịch",
      run: function () { window.location.href = "giao-dich.html"; }
    }
  ];
  let tipIndex = 0;
  function renderTip() {
    const t = TIPS[tipIndex];
    U.qs("[data-tip-title]").innerHTML = t.title;
    U.qs("[data-tip-desc]").textContent = t.desc;
    U.qs("[data-tip-cta]").innerHTML = U.escapeHtml(t.cta) + " " + App.icon("arrowRight", 16, 2.4);
  }
  U.qs("[data-tip-next]").addEventListener("click", function () {
    const box = U.qs("[data-tip]");
    box.classList.add("is-fading");
    setTimeout(function () {
      tipIndex = (tipIndex + 1) % TIPS.length;
      renderTip();
      box.classList.remove("is-fading");
    }, 220);
  });
  U.qs("[data-tip-cta]").addEventListener("click", function () { TIPS[tipIndex].run(); });

  /* ---------- Sự kiện ---------- */
  document.addEventListener("click", function (e) {
    const cp = e.target.closest("[data-copy]");
    if (cp && !e.target.closest(".modal")) { U.copyToClipboard(cp.dataset.copy, "Đã sao chép số tài khoản"); return; }
    const q = e.target.closest("[data-quick]");
    if (q) {
      if (q.dataset.quick === "deposit") App.money.openDeposit();
      else App.money.openWithdraw();
      return;
    }
    const row = e.target.closest("[data-tx]");
    if (row) App.money.openTransaction(row.dataset.tx);
  });
  els.recent.addEventListener("keydown", function (e) {
    const row = e.target.closest("[data-tx]");
    if (row && (e.key === "Enter" || e.key === " ")) { e.preventDefault(); App.money.openTransaction(row.dataset.tx); }
  });
  els.range.addEventListener("change", function () { renderChart(true); });

  /* ---------- Vẽ lần đầu + cập nhật khi dữ liệu đổi ---------- */
  function renderAll(first) {
    renderAccounts(first);
    renderChart(first);
    renderRecent();
    renderOverview();
  }
  renderGreeting();
  renderTip();
  renderAll(true);
  S.subscribe(function (reason) {
    if (reason === "notification" || reason === "settings") return;
    renderGreeting();
    renderAll(false);
  });
});
