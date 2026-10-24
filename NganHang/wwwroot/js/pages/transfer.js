/* =========================================================
   TRANG CHUYỂN TIỀN (chuyen-tien.html)
   - Kiểm tra ngay trên giao diện để báo lỗi sớm,
     server C# (BankService.Transfer) kiểm tra lại lần nữa trước khi ghi.
   ========================================================= */
App.boot(function () {
  const U = App.utils;
  const S = App.store;
  const BANK_NAME = "Hệ thống Ngân hàng";
  const MIN = 10000;
  const MAX = 1000000000;

  App.layout.render("transfer");

  const form = U.qs("[data-tf-form]");
  const els = {
    from: U.qs("[data-tf-from]"),
    fromInfo: U.qs("[data-tf-from-info]"),
    to: U.qs("[data-tf-to]"),
    bank: U.qs("[data-tf-bank]"),
    number: U.qs("[data-tf-number]"),
    name: U.qs("[data-tf-name]"),
    amount: U.qs("#tf-amount"),
    wrap: U.qs("[data-tf-wrap]"),
    chips: U.qs("[data-tf-chips]"),
    note: U.qs("[data-tf-note]"),
    error: U.qs("[data-tf-error]"),
    noteBox: U.qs("[data-tf-note-box]"),
    summary: U.qs("[data-tf-summary]"),
    recent: U.qs("[data-tf-recent]")
  };

  let mode = "other";   // "other" = người nhận khác, "own" = tài khoản của tôi
  let touched = false;  // đã bấm Tiếp tục ít nhất 1 lần -> hiện mọi lỗi

  const money = U.bindMoneyInput(els.amount, function () { refresh(); });

  /* ---------- Tiện ích ---------- */
  function digits(s) { return String(s || "").replace(/\D/g, ""); }
  function groupDigits(d) { return d.replace(/(\d{4})(?=\d)/g, "$1 "); }
  /** "Nguyễn Văn A" -> "NGUYEN VAN A" (ngân hàng dùng tên không dấu) */
  function plainUpper(s) { return U.normalizeText(s).toUpperCase().replace(/\s+/g, " ").trim(); }
  function accLabel(a) { return a.name + " (" + U.maskNumber(a.accountNumber) + ")"; }

  /* ---------- Chọn tài khoản ---------- */
  function renderSelects() {
    const accounts = S.getAccounts();
    const keepFrom = els.from.value;
    const keepTo = els.to.value;
    els.from.innerHTML = accounts.map(function (a) {
      return '<option value="' + a.id + '">' + U.escapeHtml(accLabel(a)) + "</option>";
    }).join("");
    const wanted = new URLSearchParams(window.location.search).get("from");
    if (keepFrom && S.getAccount(keepFrom)) els.from.value = keepFrom;
    else if (wanted && S.getAccount(wanted)) els.from.value = wanted;
    else if (S.getPrimary("payment")) els.from.value = S.getPrimary("payment").id;

    const others = accounts.filter(function (a) { return a.id !== els.from.value; });
    els.to.innerHTML = others.length
      ? others.map(function (a) { return '<option value="' + a.id + '">' + U.escapeHtml(accLabel(a)) + "</option>"; }).join("")
      : '<option value="">Bạn chưa có tài khoản khác</option>';
    if (keepTo && others.some(function (a) { return a.id === keepTo; })) els.to.value = keepTo;
    U.qs('[data-tf-mode="own"]').disabled = others.length === 0;
    if (!others.length && mode === "own") setMode("other");
  }

  function renderFromInfo() {
    const acc = S.getAccount(els.from.value);
    if (!acc) { els.fromInfo.innerHTML = ""; return; }
    let html = "<span>Số dư: <strong" + (acc.balance < 0 ? ' class="is-neg"' : "") + ">" + U.formatVND(acc.balance) + "</strong></span>";
    if (acc.type === "payment") {
      html += "<span>Hạn mức thấu chi: <strong>" + U.formatVND(acc.overdraftLimit) + "</strong></span>";
    }
    html += "<span>Có thể chuyển: <strong>" + U.formatVND(acc.availableBalance) + "</strong></span>";
    els.fromInfo.innerHTML = html;
  }

  function setMode(m) {
    mode = m;
    U.qsa("[data-tf-mode]").forEach(function (b) { b.setAttribute("aria-checked", String(b.dataset.tfMode === m)); });
    U.qsa("[data-tf-panel]").forEach(function (p) { p.hidden = p.dataset.tfPanel !== m; });
    refresh();
  }

  /* ---------- Gom dữ liệu + kiểm tra ---------- */
  function collect() {
    const from = S.getAccount(els.from.value);
    const amount = money.get();
    const data = { from: from, amount: amount, note: els.note.value.trim() };
    if (mode === "own") {
      data.toAcc = S.getAccount(els.to.value);
    } else {
      data.number = digits(els.number.value);
      data.name = plainUpper(els.name.value);
      data.bank = els.bank.value;
      // STK trùng với tài khoản của mình -> server sẽ xử lý như chuyển nội bộ
      data.toAcc = S.getAccounts().find(function (a) { return digits(a.accountNumber) === data.number; }) || null;
    }
    return data;
  }

  /**
   * Trả về { errors: [{field, msg}], warn, valid }.
   * Quy tắc giống hệt BankService.Transfer ở server.
   */
  function validate(data) {
    const errors = [];
    let warn = "";
    if (!data.from) errors.push({ field: "from", msg: "Vui lòng chọn tài khoản nguồn." });

    if (mode === "own") {
      if (!data.toAcc) errors.push({ field: "to", msg: "Vui lòng chọn tài khoản nhận." });
    } else {
      if (!data.bank) errors.push({ field: "bank", msg: "Vui lòng chọn ngân hàng nhận." });
      if (data.number.length < 6 || data.number.length > 19) errors.push({ field: "number", msg: "Số tài khoản nhận phải gồm 6 đến 19 chữ số." });
      else if (!data.toAcc && !data.name) errors.push({ field: "name", msg: "Vui lòng nhập tên người nhận." });
    }
    if (data.from && data.toAcc && data.toAcc.id === data.from.id) {
      errors.push({ field: mode === "own" ? "to" : "number", msg: "Không thể chuyển tiền vào chính tài khoản nguồn." });
    }

    if (!data.amount) errors.push({ field: "amount", msg: "Vui lòng nhập số tiền lớn hơn 0." });
    else if (data.amount < MIN) errors.push({ field: "amount", msg: "Số tiền chuyển tối thiểu là " + U.formatVND(MIN) + "." });
    else if (data.amount > MAX) errors.push({ field: "amount", msg: "Số tiền tối đa mỗi lần chuyển là " + U.formatVND(MAX) + "." });
    else if (data.from) {
      const check = S.checkWithdraw(data.from.id, data.amount, "chuyển");
      if (!check.ok) errors.push({ field: "amount", msg: check.error });
      else if (check.usesOverdraft) {
        warn = "Giao dịch hợp lệ: số tiền chuyển đang sử dụng một phần hạn mức thấu chi. Số dư sau khi chuyển: <strong>" +
          U.formatVND(data.from.balance - data.amount) + "</strong>.";
      }
    }
    return { errors: errors, warn: warn, valid: errors.length === 0 };
  }

  /* ---------- Vẽ lại phần tóm tắt, lỗi, cảnh báo ---------- */
  function recipientText(data) {
    if (data.toAcc) return { name: data.toAcc.name, meta: BANK_NAME + " · " + data.toAcc.accountNumber };
    if (mode === "other" && (data.name || data.number)) {
      return { name: data.name || "—", meta: (data.bank || "Chưa chọn ngân hàng") + (data.number ? " · " + groupDigits(data.number) : "") };
    }
    return { name: "—", meta: "" };
  }

  function summaryRows(data) {
    const to = recipientText(data);
    const after = data.from ? data.from.balance - data.amount : 0;
    const row = function (label, value, cls) {
      return "<div" + (cls ? ' class="' + cls + '"' : "") + "><dt>" + label + "</dt><dd>" + value + "</dd></div>";
    };
    return (
      row("Từ tài khoản", data.from ? U.escapeHtml(data.from.name) + "<small>" + data.from.accountNumber + "</small>" : "—") +
      row("Đến", U.escapeHtml(to.name) + (to.meta ? "<small>" + U.escapeHtml(to.meta) + "</small>" : "")) +
      row("Số tiền", data.amount ? U.formatVND(data.amount) : "0 đ", "is-amount") +
      row("Phí giao dịch", '<span class="is-green">Miễn phí</span>') +
      row("Nội dung", U.escapeHtml(data.note || (S.getUser().name + " chuyển tiền"))) +
      row("Số dư sau khi chuyển", data.from && data.amount
        ? '<span class="' + (after < 0 ? "is-neg" : "") + '">' + U.formatVND(after) + "</span>"
        : "—")
    );
  }

  function refresh() {
    renderFromInfo();
    const data = collect();
    const result = validate(data);
    U.qsa(".chip", els.chips).forEach(function (c) { c.classList.toggle("is-active", Number(c.dataset.chip) === data.amount); });

    // Lỗi về số tiền hiện ngay khi gõ; lỗi khác chỉ hiện sau khi bấm Tiếp tục
    const shown = result.errors.filter(function (e) {
      return touched || (e.field === "amount" && data.amount > 0) || (e.msg.indexOf("chính tài khoản") >= 0);
    });
    els.error.textContent = shown.length ? shown[0].msg : "";
    els.wrap.classList.toggle("is-error", shown.some(function (e) { return e.field === "amount"; }));
    [["bank", els.bank], ["number", els.number], ["name", els.name], ["to", els.to]].forEach(function (pair) {
      pair[1].setAttribute("aria-invalid", String(shown.some(function (e) { return e.field === pair[0]; })));
    });
    els.noteBox.innerHTML = result.warn
      ? '<div class="note note--warn">' + App.icon("alert", 20) + "<span>" + result.warn + "</span></div>"
      : "";
    els.summary.innerHTML = summaryRows(data);
    return { data: data, result: result };
  }

  /* ---------- Chuyển tiền gần đây ---------- */
  function renderRecent() {
    const list = S.getTransactions().filter(function (t) { return t.type === "transfer_out" && t.status === "success"; }).slice(0, 5);
    if (!list.length) {
      els.recent.innerHTML = '<li class="empty">Chưa có giao dịch chuyển tiền nào.</li>';
      return;
    }
    els.recent.innerHTML = list.map(function (tx) {
      const name = tx.title.replace(/^Chuyển tiền đến\s*/, "");
      return '<li><button type="button" class="tf-recent__item" data-recent="' + tx.id + '" title="Dùng lại người nhận này">' +
        App.money.txIconHtml(tx) +
        '<span class="tf-recent__main"><span class="tf-recent__name" style="display:block">' + U.escapeHtml(name) + "</span>" +
        '<span class="tf-recent__meta">' + U.formatDate(tx.date) + " · " + U.escapeHtml((tx.subtitle || "").split(" · ")[0]) + "</span></span>" +
        '<span class="tf-recent__amt money-out">' + U.formatSigned(-tx.amount) + "</span></button></li>";
    }).join("");
  }

  /** Bấm 1 dòng gần đây -> điền lại người nhận (nếu có đủ STK) */
  function reuseRecipient(txId) {
    const tx = S.getTransaction(txId);
    if (!tx) return;
    const parts = (tx.subtitle || "").split(" · ");
    const bank = parts[0] || "";
    const number = digits(parts[1] || "");
    const own = number && S.getAccounts().find(function (a) { return digits(a.accountNumber) === number; });
    if (own) {
      if (own.id === els.from.value) {
        U.showToast("Người nhận trùng tài khoản nguồn, hãy đổi tài khoản nguồn", "info");
        return;
      }
      setMode("own");
      els.to.value = own.id;
    } else {
      setMode("other");
      if (Array.prototype.some.call(els.bank.options, function (o) { return o.value === bank; })) els.bank.value = bank;
      if (number) els.number.value = groupDigits(number);
      els.name.value = tx.title.replace(/^Chuyển tiền đến\s*/, "");
      if (!number) U.showToast("Giao dịch cũ không lưu số tài khoản, vui lòng nhập lại", "info");
    }
    refresh();
    els.amount.focus();
  }

  /* ---------- Xác nhận + gửi lên server ---------- */
  function openConfirm(data) {
    const box = App.modal.open({
      size: "sm",
      initialFocus: "[data-confirm]",
      html:
        '<h2 class="modal__title">Xác nhận chuyển tiền</h2>' +
        '<p class="modal__desc">Kiểm tra lại thông tin trước khi chuyển. Giao dịch không thể hoàn tác.</p>' +
        '<div class="tf-confirm__amount"><strong>' + U.formatVND(data.amount) + "</strong><span>Miễn phí giao dịch</span></div>" +
        '<dl class="tf-sum">' + summaryRows(data) + "</dl>" +
        '<p class="field-error" role="alert" data-confirm-error></p>' +
        '<div class="modal__foot">' +
          '<button type="button" class="btn btn--outline btn--lg" data-close>Quay lại</button>' +
          '<button type="button" class="btn btn--primary btn--lg" data-confirm>Xác nhận chuyển</button>' +
        "</div>"
    });
    const btn = box.querySelector("[data-confirm]");
    btn.addEventListener("click", async function () {
      btn.disabled = true;
      btn.innerHTML = '<span class="spinner"></span>Đang chuyển...';
      const payload = { fromAccountId: data.from.id, amount: data.amount, note: data.note };
      if (mode === "own") payload.toAccountId = data.toAcc.id;
      else { payload.toAccountNumber = data.number; payload.toName = data.name; payload.toBank = data.bank; }
      const res = await S.transfer(payload);
      if (!res.ok) {
        btn.disabled = false;
        btn.textContent = "Xác nhận chuyển";
        box.querySelector("[data-confirm-error]").textContent = res.error;
        return;
      }
      openSuccess(res, data);
    });
  }

  function openSuccess(res, data) {
    const to = recipientText(data);
    const tx = res.outTx;
    const box = App.modal.open({
      size: "sm",
      className: "tf-success",
      initialFocus: "[data-again]",
      html:
        '<div class="tf-success__ic">' + App.icon("checkCircle", 40) + "</div>" +
        '<h2 class="modal__title">Chuyển tiền thành công</h2>' +
        '<div class="tf-confirm__amount"><strong>' + U.formatVND(data.amount) + "</strong><span>đến " + U.escapeHtml(to.name) + "</span></div>" +
        '<dl class="tf-sum">' +
          "<div><dt>Mã giao dịch</dt><dd>" + U.escapeHtml(tx.code) + "</dd></div>" +
          "<div><dt>Thời gian</dt><dd>" + U.formatDateTime(tx.date) + "</dd></div>" +
          "<div><dt>Số dư " + U.escapeHtml(res.from.name.toLowerCase()) + "</dt><dd" + (tx.balanceAfter < 0 ? ' class="is-neg"' : "") + ">" + U.formatVND(tx.balanceAfter) + "</dd></div>" +
        "</dl>" +
        '<div class="modal__foot">' +
          '<a class="btn btn--outline btn--lg" href="index.html">Về trang chủ</a>' +
          '<button type="button" class="btn btn--primary btn--lg" data-close data-again>Chuyển tiếp</button>' +
        "</div>"
    });
    box.querySelector("[data-again]").addEventListener("click", function () { els.amount.focus(); });
    resetForm();
    U.showToast("Đã chuyển " + U.formatVND(data.amount) + " đến " + to.name);
  }

  function resetForm() {
    touched = false;
    els.bank.value = "";
    els.number.value = "";
    els.name.value = "";
    els.note.value = "";
    money.set(0);
    refresh();
  }

  /* ---------- Sự kiện ---------- */
  U.qsa("[data-tf-mode]").forEach(function (b) {
    b.addEventListener("click", function () { if (!b.disabled) setMode(b.dataset.tfMode); });
  });
  els.from.addEventListener("change", function () { renderSelects(); refresh(); });
  [els.to, els.bank].forEach(function (el) { el.addEventListener("change", refresh); });
  els.number.addEventListener("input", function () {
    const d = digits(els.number.value).slice(0, 19);
    els.number.value = groupDigits(d);
    refresh();
  });
  els.name.addEventListener("input", refresh);
  els.name.addEventListener("blur", function () { els.name.value = plainUpper(els.name.value); refresh(); });
  els.note.addEventListener("input", refresh);
  els.chips.addEventListener("click", function (e) {
    const c = e.target.closest("[data-chip]");
    if (c) { money.set(Number(c.dataset.chip)); els.amount.focus(); }
  });
  els.recent.addEventListener("click", function (e) {
    const r = e.target.closest("[data-recent]");
    if (r) reuseRecipient(r.dataset.recent);
  });
  form.addEventListener("reset", function (e) { e.preventDefault(); resetForm(); });
  form.addEventListener("submit", function (e) {
    e.preventDefault();
    touched = true;
    const r = refresh();
    if (!r.result.valid) {
      const first = r.result.errors[0].field;
      const target = { from: els.from, to: els.to, bank: els.bank, number: els.number, name: els.name, amount: els.amount }[first];
      if (target) target.focus();
      return;
    }
    openConfirm(r.data);
  });

  /* ---------- Khởi tạo + đồng bộ ---------- */
  els.note.placeholder = S.getUser().name + " chuyển tiền";
  renderSelects();
  renderRecent();
  refresh();
  S.subscribe(function (reason) {
    if (reason === "notification" || reason === "settings") return;
    renderSelects();
    renderRecent();
    refresh();
  });
});
