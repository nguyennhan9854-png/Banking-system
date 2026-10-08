/* =========================================================
   TRANG "TÀI KHOẢN CỦA TÔI" (tai-khoan.html)
   ========================================================= */
App.boot(function () {
  const U = App.utils;
  const S = App.store;
  const HIDDEN_KEY = "nhbank_hidden_balances";

  App.layout.render("accounts");

  const els = {
    grid: U.qs("[data-accounts]"),
    chart: U.qs("[data-acct-chart]"),
    chartAccount: U.qs("[data-chart-account]"),
    tabs: U.qs("[data-range-tabs]"),
    custom: U.qs("[data-custom-range]"),
    from: U.qs("[data-from]"),
    to: U.qs("[data-to]"),
    rangeError: U.qs("[data-range-error]"),
    activity: U.qs("[data-activity]")
  };

  /* ---------- Ẩn/hiện số dư (lưu localStorage) ---------- */
  function getHidden() {
    try { return JSON.parse(localStorage.getItem(HIDDEN_KEY)) || []; } catch (e) { return []; }
  }
  function setHidden(list) {
    try { localStorage.setItem(HIDDEN_KEY, JSON.stringify(list)); } catch (e) { /* bỏ qua */ }
  }
  function toggleHidden(id) {
    const list = getHidden();
    const i = list.indexOf(id);
    if (i >= 0) list.splice(i, 1); else list.push(id);
    setHidden(list);
  }

  /* ---------- Thẻ tài khoản ---------- */
  function lowerFirst(s) { return s.charAt(0).toLowerCase() + s.slice(1); }

  function infoItem(icon, label, value, cls) {
    return '<div class="acct__info-item"><span class="acct__info-ic">' + App.icon(icon, 18) + "</span><div style=\"min-width:0\">" +
      '<div class="acct__info-label">' + label + '</div><div class="acct__info-value' + (cls ? " " + cls : "") + '">' + value + "</div></div></div>";
  }

  function accountCard(acc, hidden) {
    const isPay = acc.type === "payment";
    const isHidden = hidden.indexOf(acc.id) >= 0;
    const amount = isHidden ? "••••••• đ" : U.formatVND(acc.balance);
    const third = isPay
      ? infoItem("card", "Hạn mức thấu chi", U.formatVND(acc.overdraftLimit))
      : infoItem("percent", "Lãi suất hiện tại", U.formatPercent(acc.interestRate, 1) + "/năm", "is-green");
    const overdraftNote = isPay && acc.balance < 0 && !isHidden
      ? '<div class="acct__overdraft">Đang dùng thấu chi ' + U.formatVND(-acc.balance) + "</div>" : "";
    return (
      '<article class="acct ' + (isPay ? "acct--pay" : "acct--save") + '" id="' + acc.id + '" data-account="' + acc.id + '">' +
        '<div class="acct__top">' +
          '<div class="acct__head-row">' +
            '<span class="acct__icon">' + App.icon(isPay ? "card" : "piggy", 30) + "</span>" +
            "<div>" +
              '<div class="acct__titlebar"><h3 class="acct__title">' + U.escapeHtml(acc.name) + "</h3>" +
                '<span class="badge badge--success badge--dot badge--sm">Đang hoạt động</span></div>' +
              '<div class="acct__label">Số tài khoản</div>' +
              '<div class="acct__num">' + acc.accountNumber +
                '<button type="button" class="copy-btn" data-copy="' + acc.accountNumber.replace(/\s/g, "") + '" aria-label="Sao chép số tài khoản ' + U.escapeHtml(acc.name) + '">' + App.icon("copy", 17) + "</button></div>" +
            "</div>" +
          "</div>" +
          '<div class="acct__balance">' +
            '<div class="acct__label">Số dư hiện tại</div>' +
            '<div class="acct__amount"><strong class="' + (isHidden ? "is-hidden" : acc.balance < 0 ? "is-negative" : "") + '" data-amount>' + amount + "</strong>" +
              '<button type="button" class="eye-btn" data-eye="' + acc.id + '" aria-pressed="' + isHidden + '" aria-label="' + (isHidden ? "Hiện số dư" : "Ẩn số dư") + '">' +
                App.icon(isHidden ? "eyeOff" : "eye", 20) + "</button></div>" +
            overdraftNote +
          "</div>" +
          '<img class="acct__art" src="assets/images/' + (isPay ? "bank-cards.png" : "piggy.png") + '" alt="">' +
        "</div>" +
        '<div class="acct__info">' +
          infoItem("calendar", "Ngày mở tài khoản", U.formatDate(acc.openedDate + "T00:00:00")) +
          infoItem("layers", "Loại tài khoản", U.escapeHtml(acc.productName)) +
          third +
        "</div>" +
        '<div class="acct__actions">' +
          '<button type="button" class="btn ' + (isPay ? "btn--acc-pay" : "btn--primary") + '" data-detail="' + acc.id + '">Xem chi tiết ' + App.icon("chevronRight", 16, 2.4) + "</button>" +
          '<button type="button" class="btn ' + (isPay ? "btn--outline-orange" : "btn--outline") + '" data-deposit="' + acc.id + '">' + App.icon("wallet", 18) + "Nạp tiền</button>" +
          '<button type="button" class="btn ' + (isPay ? "btn--outline-orange" : "btn--outline") + '" data-withdraw="' + acc.id + '">' + App.icon("arrowUp", 18) + "Rút tiền</button>" +
        "</div>" +
      "</article>"
    );
  }

  function renderAccounts() {
    const hidden = getHidden();
    els.grid.innerHTML = S.getAccounts().map(function (a) { return accountCard(a, hidden); }).join("");
  }

  /* ---------- Modal chi tiết tài khoản ---------- */
  function openDetail(id) {
    const acc = S.getAccount(id);
    if (!acc) return;
    const user = S.getUser();
    const isPay = acc.type === "payment";
    const cell = function (icon, label, value, cls) {
      return '<div class="info-cell"><span class="info-cell__ic">' + App.icon(icon, 18) + '</span><div style="min-width:0"><div class="info-cell__label">' +
        label + '</div><div class="info-cell__value' + (cls ? " " + cls : "") + '">' + value + "</div></div></div>";
    };
    const numCell = cell("card", "Số tài khoản", acc.accountNumber +
      '<button type="button" class="copy-btn" data-copy="' + acc.accountNumber.replace(/\s/g, "") + '" aria-label="Sao chép số tài khoản">' + App.icon("copy", 16) + "</button>");
    let cells = cell("user", "Chủ tài khoản", U.escapeHtml(user.name)) + numCell +
      cell("layers", "Loại tài khoản", U.escapeHtml(acc.productName)) + cell("bank", "Chi nhánh", U.escapeHtml(acc.branch)) +
      cell("calendar", "Ngày mở", U.formatDate(acc.openedDate + "T00:00:00"));
    if (isPay) {
      const usedOd = acc.balance < 0 ? -acc.balance : 0;
      cells += cell("checkCircle", "Trạng thái", "Đang hoạt động", "is-green") +
        cell("wallet", "Số dư hiện tại", U.formatVND(acc.balance), "is-big" + (acc.balance < 0 ? " is-red" : "")) +
        cell("gauge", "Hạn mức thấu chi", U.formatVND(acc.overdraftLimit), "is-big") +
        cell("alert", "Đang dùng thấu chi", U.formatVND(usedOd)) +
        cell("trendUp", "Số tiền khả dụng", U.formatVND(acc.balance + acc.overdraftLimit), "is-big");
    } else {
      const term = S.savingTerm(acc);
      cells += cell("clock", "Kỳ hạn", acc.termMonths + " tháng") +
        cell("calendar", "Ngày đáo hạn", term ? U.formatDate(term.maturity) : "—") +
        cell("percent", "Lãi suất", U.formatPercent(acc.interestRate, 1) + "/năm", "is-green") +
        cell("wallet", "Số dư hiện tại", U.formatVND(acc.balance), "is-big") +
        cell("trendUp", "Tiền lãi dự kiến", term ? U.formatVND(term.estimatedInterest) : "—", "is-big");
    }
    const term = isPay ? null : S.savingTerm(acc);
    const box = App.modal.open({
      html:
        '<div class="acct-detail__head">' +
          '<img class="acct-detail__art" src="assets/images/' + (isPay ? "bank-cards.png" : "piggy.png") + '" alt="">' +
          '<h2 class="modal__title">Chi tiết ' + U.escapeHtml(lowerFirst(acc.name)) + "</h2>" +
          '<span class="badge badge--success badge--dot badge--sm">Đang hoạt động</span>' +
        "</div>" +
        '<div class="info-grid" style="margin-top:16px">' + cells + "</div>" +
        (term ? '<p class="modal__desc" style="margin-top:10px">Tiền lãi dự kiến tính cho kỳ hạn hiện tại (' + term.days + " ngày, từ " + U.formatDate(term.start) + "). Sổ tự động tái tục khi đến hạn.</p>" : "") +
        '<div class="modal__foot">' +
          '<button type="button" class="btn btn--outline btn--lg" data-close>Đóng</button>' +
          '<button type="button" class="btn btn--primary btn--lg" data-go-deposit>' + App.icon("wallet", 20) + "Nạp tiền</button>" +
        "</div>"
    });
    box.addEventListener("click", function (e) {
      const cp = e.target.closest("[data-copy]");
      if (cp) U.copyToClipboard(cp.dataset.copy, "Đã sao chép số tài khoản");
      if (e.target.closest("[data-go-deposit]")) App.money.openDeposit(acc.id);
    });
  }

  /* ---------- Modal mở tài khoản mới ---------- */
  function openNewAccount() {
    const rates = S.SAVING_RATES;
    const box = App.modal.open({
      size: "lg",
      initialFocus: '[data-type="saving"]',
      html:
        '<h2 class="modal__title">Mở tài khoản mới</h2>' +
        '<p class="modal__desc">Chọn loại tài khoản phù hợp với nhu cầu của bạn</p>' +
        '<div class="modal__body">' +
          '<div class="type-pick" role="radiogroup" aria-label="Loại tài khoản">' +
            '<button type="button" class="type-card type-card--save" role="radio" aria-checked="true" data-type="saving">' +
              '<span class="type-card__check">' + App.icon("check", 14, 3) + "</span>" +
              '<span class="type-card__ic">' + App.icon("piggy", 32) + "</span>" +
              '<span class="type-card__title">Tiết kiệm</span><span class="type-card__desc">Tích lũy an toàn, nhận lãi suất hấp dẫn</span></button>' +
            '<button type="button" class="type-card type-card--pay" role="radio" aria-checked="false" data-type="payment">' +
              '<span class="type-card__check">' + App.icon("check", 14, 3) + "</span>" +
              '<span class="type-card__ic">' + App.icon("card", 32) + "</span>" +
              '<span class="type-card__title">Thanh toán</span><span class="type-card__desc">Giao dịch linh hoạt, thanh toán mọi nhu cầu</span></button>' +
          "</div>" +
          '<div class="form-rows">' +
            '<div class="form-row"><label for="na-type">Loại tài khoản</label><input id="na-type" class="input" readonly value="Tiết kiệm"></div>' +
            '<div class="form-row" data-only="saving"><label for="na-term">Kỳ hạn</label><select id="na-term" class="select">' +
              Object.keys(rates).map(function (k) { return '<option value="' + k + '"' + (k === "6" ? " selected" : "") + ">" + k + " tháng</option>"; }).join("") +
            "</select></div>" +
            '<div class="form-row" data-only="payment" hidden><label for="na-limit">Hạn mức thấu chi</label><select id="na-limit" class="select">' +
              '<option value="0">Không sử dụng</option><option value="5000000" selected>5.000.000 đ</option><option value="10000000">10.000.000 đ</option>' +
            "</select></div>" +
            '<div class="form-row"><label for="na-amount" data-amount-label>Số tiền gửi ban đầu</label>' +
              '<div class="money-input" data-na-wrap><input id="na-amount" type="text" inputmode="numeric" placeholder="Tối thiểu 1.000.000" autocomplete="off" aria-describedby="na-error"><span class="money-input__unit">đ</span></div>' +
              '<p class="field-error" id="na-error" role="alert" data-na-error></p></div>' +
            '<div class="form-row" data-only="saving"><label for="na-rate">Lãi suất dự kiến</label><input id="na-rate" class="input is-green" readonly></div>' +
          "</div>" +
          '<div class="note note--info">' + App.icon("info", 18) + "<span>Số tài khoản sẽ được tạo tự động sau khi xác nhận</span></div>" +
        "</div>" +
        '<div class="modal__foot">' +
          '<button type="button" class="btn btn--outline btn--lg" data-close>Hủy</button>' +
          '<button type="button" class="btn btn--primary btn--lg" data-submit>Mở tài khoản</button>' +
        "</div>"
    });

    let type = "saving";
    const amountInput = box.querySelector("#na-amount");
    const errorEl = box.querySelector("[data-na-error]");
    const wrap = box.querySelector("[data-na-wrap]");
    const term = box.querySelector("#na-term");
    const rate = box.querySelector("#na-rate");
    const money = U.bindMoneyInput(amountInput, function () { if (errorEl.textContent) validate(); });

    function paintRate() { rate.value = U.formatPercent(rates[term.value], 1) + "/năm"; }
    function setType(t) {
      type = t;
      U.qsa("[data-type]", box).forEach(function (b) { b.setAttribute("aria-checked", String(b.dataset.type === t)); });
      U.qsa("[data-only]", box).forEach(function (r) { r.hidden = r.dataset.only !== t; });
      box.querySelector("#na-type").value = t === "saving" ? "Tiết kiệm" : "Thanh toán";
      box.querySelector("[data-amount-label]").textContent = t === "saving" ? "Số tiền gửi ban đầu" : "Số tiền nạp ban đầu";
      amountInput.placeholder = t === "saving" ? "Tối thiểu 1.000.000" : "Có thể để trống";
      errorEl.textContent = ""; wrap.classList.remove("is-error");
    }
    function validate() {
      const v = money.get();
      let err = "";
      if (type === "saving" && v < 1000000) err = "Số tiền gửi tối thiểu là 1.000.000 đ.";
      if (v > 1000000000) err = "Số tiền tối đa là 1.000.000.000 đ.";
      errorEl.textContent = err;
      wrap.classList.toggle("is-error", !!err);
      return !err;
    }
    paintRate();
    term.addEventListener("change", paintRate);
    box.addEventListener("click", function (e) {
      const t = e.target.closest("[data-type]");
      if (t) setType(t.dataset.type);
    });
    // phím mũi tên chuyển loại (chuẩn radiogroup)
    box.querySelector(".type-pick").addEventListener("keydown", function (e) {
      if (["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"].indexOf(e.key) === -1) return;
      e.preventDefault();
      const next = type === "saving" ? "payment" : "saving";
      setType(next);
      box.querySelector('[data-type="' + next + '"]').focus();
    });
    const submitBtn = box.querySelector("[data-submit]");
    submitBtn.addEventListener("click", async function () {
      if (!validate()) { amountInput.focus(); return; }
      submitBtn.disabled = true;
      const res = await S.openAccount({
        type: type,
        termMonths: Number(term.value),
        initialAmount: money.get(),
        overdraftLimit: Number(box.querySelector("#na-limit").value)
      });
      submitBtn.disabled = false;
      if (!res.ok) { errorEl.textContent = res.error; wrap.classList.add("is-error"); return; }
      App.modal.close();
      U.showToast("Đã mở " + res.account.name + " – STK " + res.account.accountNumber);
      const card = document.getElementById(res.account.id);
      if (card) {
        card.scrollIntoView({ behavior: "smooth", block: "center" });
        card.classList.add("is-new");
        setTimeout(function () { card.classList.remove("is-new"); }, 1700);
      }
    });
  }

  /* ---------- Biểu đồ theo tài khoản ---------- */
  let range = "6m";
  let customFrom = null, customTo = null, customToToday = false;

  function renderChartAccounts() {
    const current = els.chartAccount.value;
    const accounts = S.getAccounts();
    els.chartAccount.innerHTML = accounts.map(function (a) {
      return '<option value="' + a.id + '">' + U.escapeHtml(a.name + " (" + U.maskNumber(a.accountNumber) + ")") + "</option>";
    }).join("");
    if (current && S.getAccount(current)) els.chartAccount.value = current;
  }

  function renderChart(animate) {
    const id = els.chartAccount.value;
    if (!id) return;
    const now = new Date();
    let from, to = now, mode;
    if (range === "6m") { from = new Date(now.getFullYear(), now.getMonth() - 5, 1); mode = "monthly"; }
    else if (range === "3m") { from = U.addMonths(now, -3); mode = "weekly"; }
    else if (range === "1m") { from = U.addMonths(now, -1); mode = "daily"; }
    else {
      from = customFrom || U.addMonths(now, -1);
      to = customToToday ? now : (customTo || now);
      const span = U.daysBetween(from, to);
      mode = span <= 45 ? "daily" : span <= 200 ? "weekly" : "monthly";
    }
    const acc = S.getAccount(id);
    const token = ++chartToken;
    S.balanceSeries(id, from, to, mode).then(function (points) {
      if (token !== chartToken) return; // đã có yêu cầu mới hơn
      App.charts.line(els.chart, {
        points: points,
        height: 250,
        highlightLast: true,
        animate: animate,
        ariaLabel: "Biến động số dư " + acc.name
      });
    }).catch(function (err) {
      els.chart.innerHTML = '<div class="empty">' + U.escapeHtml(err.message) + "</div>";
    });
  }
  let chartToken = 0;

  function setRange(r) {
    range = r;
    U.qsa("[data-range]", els.tabs).forEach(function (t) {
      const on = t.dataset.range === r;
      t.classList.toggle("is-active", on);
      t.setAttribute("aria-selected", String(on));
    });
    els.custom.hidden = r !== "custom";
    if (r === "custom") {
      const now = new Date();
      if (!els.from.value) els.from.value = U.toInputDate(U.addMonths(now, -2));
      if (!els.to.value) els.to.value = U.toInputDate(now);
      els.from.max = els.to.max = U.toInputDate(now);
      applyCustom();
      return;
    }
    renderChart(true);
  }

  function applyCustom() {
    els.rangeError.textContent = "";
    const f = els.from.value ? new Date(els.from.value + "T00:00:00") : null;
    const t = els.to.value ? new Date(els.to.value + "T00:00:00") : null;
    const today = U.startOfDay(new Date());
    if (!f || !t) { els.rangeError.textContent = "Vui lòng chọn đủ từ ngày và đến ngày."; return; }
    if (f > t) { els.rangeError.textContent = "Từ ngày phải trước hoặc bằng đến ngày."; return; }
    if (t > today) { els.rangeError.textContent = "Đến ngày không được sau hôm nay."; return; }
    if (U.daysBetween(f, t) > 730) { els.rangeError.textContent = "Chỉ xem tối đa 2 năm mỗi lần."; return; }
    customFrom = f;
    customToToday = U.daysBetween(t, today) === 0;
    customTo = U.endOfDay(t);
    renderChart(true);
  }

  /* ---------- Hoạt động gần đây ---------- */
  function renderActivity() {
    const list = S.getTransactions({ limit: 5 });
    if (!list.length) {
      els.activity.innerHTML = '<div class="empty">Chưa có hoạt động nào.</div>';
      return;
    }
    els.activity.innerHTML =
      '<table class="table"><thead><tr><th scope="col">Thời gian</th><th scope="col">Tài khoản</th><th scope="col">Nội dung giao dịch</th>' +
      '<th scope="col" class="t-right">Số tiền</th><th scope="col" class="t-right">Số dư sau giao dịch</th></tr></thead><tbody>' +
      list.map(function (tx) {
        const acc = S.getAccount(tx.accountId);
        const isIn = S.TX_TYPES[tx.type].dir === "in";
        return '<tr class="is-clickable" tabindex="0" data-tx="' + tx.id + '">' +
          '<td><div class="cell-time">' + App.money.txIconHtml(tx, 15) + "<span>" + U.formatDate(tx.date) + "<br>" + U.formatTime(tx.date) + "</span></div></td>" +
          '<td class="cell-2l cell-acc">' + (acc ? (acc.type === "saving" ? "Tiết kiệm" : "Thanh toán") : "—") + "<small>" + (acc ? U.maskNumber(acc.accountNumber) : "") + "</small></td>" +
          '<td class="cell-2l">' + U.escapeHtml(tx.title) + "<small>" + U.escapeHtml(tx.subtitle || "") + "</small></td>" +
          '<td class="t-right cell-amount ' + (isIn ? "money-in" : "money-out") + '">' + U.formatSigned(isIn ? tx.amount : -tx.amount) + "</td>" +
          '<td class="t-right cell-balance">' + U.formatVND(tx.balanceAfter) + "</td></tr>";
      }).join("") + "</tbody></table>";
  }

  /* ---------- Sự kiện ---------- */
  els.grid.addEventListener("click", function (e) {
    const t = e.target;
    const cp = t.closest("[data-copy]");
    if (cp) { U.copyToClipboard(cp.dataset.copy, "Đã sao chép số tài khoản"); return; }
    const eye = t.closest("[data-eye]");
    if (eye) {
      toggleHidden(eye.dataset.eye);
      renderAccounts();
      const again = els.grid.querySelector('[data-eye="' + eye.dataset.eye + '"]');
      if (again) again.focus();
      return;
    }
    const d = t.closest("[data-detail]");
    if (d) { openDetail(d.dataset.detail); return; }
    const dep = t.closest("[data-deposit]");
    if (dep) { App.money.openDeposit(dep.dataset.deposit); return; }
    const w = t.closest("[data-withdraw]");
    if (w) { App.money.openWithdraw(w.dataset.withdraw); }
  });
  U.qs("[data-open-account]").addEventListener("click", openNewAccount);
  els.chartAccount.addEventListener("change", function () { renderChart(true); });
  els.tabs.addEventListener("click", function (e) {
    const t = e.target.closest("[data-range]");
    if (t) setRange(t.dataset.range);
  });
  U.qs("[data-apply-range]").addEventListener("click", applyCustom);
  els.activity.addEventListener("click", function (e) {
    const row = e.target.closest("[data-tx]");
    if (row) App.money.openTransaction(row.dataset.tx);
  });
  els.activity.addEventListener("keydown", function (e) {
    const row = e.target.closest("[data-tx]");
    if (row && (e.key === "Enter" || e.key === " ")) { e.preventDefault(); App.money.openTransaction(row.dataset.tx); }
  });

  /* ---------- Vẽ + đồng bộ ---------- */
  function renderAll(animate) {
    renderAccounts();
    renderChartAccounts();
    renderChart(animate);
    renderActivity();
  }
  renderAll(true);
  S.subscribe(function (reason) {
    if (reason === "notification" || reason === "settings") return;
    renderAll(false);
  });

  /* ---------- Điều hướng từ trang khác: #acc-id hoặc #mo-tai-khoan ---------- */
  function handleHash() {
    const h = decodeURIComponent(window.location.hash.slice(1));
    if (!h) return;
    if (h === "mo-tai-khoan") { openNewAccount(); return; }
    const card = document.getElementById(h);
    if (card && card.classList.contains("acct")) {
      card.scrollIntoView({ block: "center" });
      card.classList.add("is-new");
      setTimeout(function () { card.classList.remove("is-new"); }, 1700);
      els.chartAccount.value = h;
      renderChart(false);
    }
  }
  handleHash();
  window.addEventListener("hashchange", handleHash);
});
