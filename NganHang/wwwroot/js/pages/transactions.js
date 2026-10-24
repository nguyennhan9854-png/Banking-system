/* =========================================================
   TRANG GIAO DỊCH (giao-dich.html) – bản cơ bản
   Đọc cùng store nên giao dịch nạp/rút ở trang khác xuất hiện ngay.
   ========================================================= */
App.boot(function () {
  const U = App.utils;
  const S = App.store;
  const PAGE_SIZE = 10;

  App.layout.render("transactions");

  const els = {
    q: U.qs("[data-f-q]"),
    acc: U.qs("[data-f-acc]"),
    type: U.qs("[data-f-type]"),
    status: U.qs("[data-f-status]"),
    reset: U.qs("[data-f-reset]"),
    table: U.qs("[data-tx-table]"),
    pager: U.qs("[data-pager]"),
    summary: U.qs("[data-summary]")
  };

  const TYPE_GROUPS = {
    deposit: ["deposit", "open"],
    withdraw: ["withdraw"],
    transfer: ["transfer_in", "transfer_out"],
    interest: ["interest"],
    payment: ["bill", "shopping"]
  };

  let page = 1;

  // Từ khóa từ ô tìm kiếm ở header (giao-dich.html?q=...)
  const params = new URLSearchParams(window.location.search);
  if (params.get("q")) els.q.value = params.get("q");
  if (params.get("status") === "failed" || params.get("status") === "success") els.status.value = params.get("status");

  function renderAccountFilter() {
    const cur = els.acc.value;
    els.acc.innerHTML = '<option value="">Tất cả tài khoản</option>' + S.getAccounts().map(function (a) {
      return '<option value="' + a.id + '">' + U.escapeHtml(a.name + " (" + U.maskNumber(a.accountNumber) + ")") + "</option>";
    }).join("");
    if (cur && S.getAccount(cur)) els.acc.value = cur;
  }

  function filtered() {
    const q = U.normalizeText(els.q.value);
    const accId = els.acc.value;
    const group = TYPE_GROUPS[els.type.value];
    const status = els.status.value;
    return S.getTransactions().filter(function (tx) {
      if (accId && tx.accountId !== accId) return false;
      if (status && tx.status !== status) return false;
      if (group && group.indexOf(tx.type) === -1) return false;
      if (q) {
        const acc = S.getAccount(tx.accountId);
        const hay = U.normalizeText([tx.title, tx.subtitle, tx.code, S.TX_TYPES[tx.type].label, acc ? acc.name + " " + acc.accountNumber : ""].join(" "));
        if (hay.indexOf(q) === -1) return false;
      }
      return true;
    });
  }

  function render() {
    const list = filtered();
    const pages = Math.max(1, Math.ceil(list.length / PAGE_SIZE));
    if (page > pages) page = pages;
    const slice = list.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
    // Chỉ giao dịch THÀNH CÔNG mới tính vào tổng tiền (giao dịch thất bại đã được hoàn tác)
    const done = list.filter(function (t) { return t.status === "success"; });
    const totalIn = done.reduce(function (s, t) { return s + (S.TX_TYPES[t.type].dir === "in" ? t.amount : 0); }, 0);
    const totalOut = done.reduce(function (s, t) { return s + (S.TX_TYPES[t.type].dir === "out" ? t.amount : 0); }, 0);
    const failedCount = list.length - done.length;
    els.summary.innerHTML = list.length + " giao dịch, tiền vào <strong class=\"money-in\">" + U.formatVND(totalIn) +
      "</strong>, tiền ra <strong class=\"money-out\">" + U.formatVND(totalOut) + "</strong>" +
      (failedCount ? ', <strong class="money-out">' + failedCount + " thất bại</strong>" : "");

    if (!slice.length) {
      els.table.innerHTML = '<div class="empty">Không tìm thấy giao dịch phù hợp. Hãy thử từ khóa khác hoặc bấm “Xóa bộ lọc”.</div>';
      els.pager.innerHTML = "";
      return;
    }
    els.table.innerHTML =
      '<table class="table"><thead><tr>' +
      '<th scope="col">Thời gian</th><th scope="col">Loại</th><th scope="col">Tài khoản</th><th scope="col">Nội dung</th>' +
      '<th scope="col" class="t-right">Số tiền</th><th scope="col" class="t-right">Số dư sau GD</th>' +
      '<th scope="col" class="t-center">Trạng thái</th><th scope="col">Mã GD</th></tr></thead><tbody>' +
      slice.map(function (tx) {
        const acc = S.getAccount(tx.accountId);
        const meta = S.TX_TYPES[tx.type];
        const isIn = meta.dir === "in";
        return '<tr class="is-clickable' + (tx.status === "failed" ? " is-failed" : "") + '" tabindex="0" data-tx="' + tx.id + '">' +
          '<td class="cell-time">' + U.formatDate(tx.date) + "<br>" + U.formatTime(tx.date) + "</td>" +
          '<td><span class="tx-type">' + App.money.txIconHtml(tx, 15) + meta.label + "</span></td>" +
          '<td class="cell-2l cell-acc">' + (acc ? U.escapeHtml(acc.name) : "—") + "<small>" + (acc ? U.maskNumber(acc.accountNumber) : "") + "</small></td>" +
          '<td class="cell-2l">' + U.escapeHtml(tx.title) + "<small>" + U.escapeHtml(tx.subtitle || "") + "</small></td>" +
          '<td class="t-right cell-amount ' + (isIn ? "money-in" : "money-out") + '">' + U.formatSigned(isIn ? tx.amount : -tx.amount) + "</td>" +
          '<td class="t-right cell-balance">' + U.formatVND(tx.balanceAfter) + "</td>" +
          '<td class="t-center">' + App.money.statusBadge(tx.status) + "</td>" +
          '<td style="color:var(--muted)">' + tx.code + "</td></tr>";
      }).join("") + "</tbody></table>";

    let btns = '<span class="pager__info">Trang ' + page + "/" + pages + "</span>" +
      '<button type="button" data-page="' + (page - 1) + '"' + (page === 1 ? " disabled" : "") + ' aria-label="Trang trước">' + App.icon("chevronLeft", 16) + "</button>";
    for (let i = 1; i <= pages; i++) {
      btns += '<button type="button" data-page="' + i + '"' + (i === page ? ' class="is-active" aria-current="page"' : "") + ">" + i + "</button>";
    }
    btns += '<button type="button" data-page="' + (page + 1) + '"' + (page === pages ? " disabled" : "") + ' aria-label="Trang sau">' + App.icon("chevronRight", 16) + "</button>";
    els.pager.innerHTML = btns;
  }

  let t = 0;
  els.q.addEventListener("input", function () { clearTimeout(t); t = setTimeout(function () { page = 1; render(); }, 200); });
  els.acc.addEventListener("change", function () { page = 1; render(); });
  els.type.addEventListener("change", function () { page = 1; render(); });
  els.status.addEventListener("change", function () { page = 1; render(); });
  els.reset.addEventListener("click", function () {
    els.q.value = ""; els.acc.value = ""; els.type.value = ""; els.status.value = ""; page = 1;
    const hs = document.getElementById("global-search");
    if (hs) hs.value = "";
    if (window.history.replaceState) window.history.replaceState(null, "", "giao-dich.html");
    render();
  });
  els.pager.addEventListener("click", function (e) {
    const b = e.target.closest("[data-page]");
    if (!b || b.disabled) return;
    page = Number(b.dataset.page);
    render();
  });
  els.table.addEventListener("click", function (e) {
    const row = e.target.closest("[data-tx]");
    if (row) App.money.openTransaction(row.dataset.tx);
  });
  els.table.addEventListener("keydown", function (e) {
    const row = e.target.closest("[data-tx]");
    if (row && (e.key === "Enter" || e.key === " ")) { e.preventDefault(); App.money.openTransaction(row.dataset.tx); }
  });

  renderAccountFilter();
  render();
  S.subscribe(function (reason) {
    if (reason === "notification" || reason === "settings") return;
    renderAccountFilter();
    render();
  });
});
