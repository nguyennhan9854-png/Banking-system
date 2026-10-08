/* =========================================================
   MODAL NẠP TIỀN / RÚT TIỀN / CHI TIẾT GIAO DỊCH (dùng chung mọi trang)
   - App.money.openDeposit(accountId?)   (không truyền -> cho chọn tài khoản)
   - App.money.openWithdraw(accountId?)
   - App.money.openTransaction(txId)
   ========================================================= */
window.App = window.App || {};

App.money = (function () {
  const U = App.utils;
  const S = App.store;

  const DEPOSIT_CHIPS = [100000, 500000, 1000000, 5000000];
  const WITHDRAW_CHIPS = [500000, 1000000, 2000000, 5000000];

  function accountOption(a) {
    return a.name + " (" + U.maskNumber(a.accountNumber) + ")";
  }

  /** Khối thông tin tài khoản ở đầu modal */
  function accountBlock(acc, mode) {
    const isPay = acc.type === "payment";
    const tile = isPay
      ? '<span class="acc-block__tile acc-block__tile--pay">' + App.icon("card", 28) + "</span>"
      : '<span class="acc-block__tile acc-block__tile--save"><img src="assets/images/piggy.png" alt=""></span>';
    let extra = "";
    if (mode === "withdraw" && isPay) {
      extra = '<div class="acc-block__meta">Hạn mức thấu chi: <strong>' + U.formatVND(acc.overdraftLimit) + "</strong></div>" +
        '<div class="acc-block__meta">Số tiền khả dụng: <strong>' + U.formatVND(acc.balance + acc.overdraftLimit) + "</strong></div>";
    }
    return (
      '<div class="acc-block acc-block--' + (isPay ? "pay" : "save") + '">' +
        tile +
        '<div class="acc-block__main">' +
          '<div class="acc-block__name">' + U.escapeHtml(acc.name) + "</div>" +
          '<div class="acc-block__label">Số tài khoản</div>' +
          '<div class="acc-block__num">' + acc.accountNumber +
            '<button type="button" class="copy-btn" data-copy="' + acc.accountNumber.replace(/\s/g, "") + '" aria-label="Sao chép số tài khoản">' + App.icon("copy", 16) + "</button></div>" +
        "</div>" +
        '<div class="acc-block__bal">' +
          (mode === "withdraw" ? '<span class="badge badge--success badge--dot badge--sm">Đang hoạt động</span>' : "") +
          '<div class="acc-block__label">Số dư hiện tại</div>' +
          '<div class="acc-block__amount' + (acc.balance < 0 ? " money-out" : "") + '">' + U.formatVND(acc.balance) + "</div>" + extra + "</div>" +
      "</div>"
    );
  }

  /**
   * Modal chung cho nạp/rút. mode = "deposit" | "withdraw"
   */
  function openMoney(mode, accountId) {
    const accounts = S.getAccounts();
    if (!accounts.length) return;
    let acc = (accountId && S.getAccount(accountId)) || null;
    const chooser = !acc;
    if (!acc) acc = mode === "withdraw" ? (S.getPrimary("payment") || accounts[0]) : (S.getPrimary("saving") || accounts[0]);
    const isW = mode === "withdraw";
    const chips = isW ? WITHDRAW_CHIPS : DEPOSIT_CHIPS;

    const html =
      '<h2 class="modal__title">' + (isW ? "Rút tiền" : "Nạp tiền") + "</h2>" +
      '<p class="modal__desc">' + (isW
        ? "Rút tiền từ tài khoản. Số dư được cập nhật ngay sau khi xác nhận."
        : "Nạp tiền vào tài khoản để gia tăng số dư và đạt được mục tiêu tài chính của bạn.") + "</p>" +
      '<div class="modal__body">' +
        (chooser
          ? '<div class="field"><label class="field__label" for="money-acc">' + (isW ? "Rút từ tài khoản" : "Nạp vào tài khoản") + "</label>" +
            '<select id="money-acc" class="select">' + accounts.map(function (a) {
              return '<option value="' + a.id + '"' + (a.id === acc.id ? " selected" : "") + ">" + U.escapeHtml(accountOption(a)) + "</option>";
            }).join("") + "</select></div>"
          : "") +
        '<div data-acc-block></div>' +
        '<div class="field">' +
          '<label class="field__label" for="money-amount">' + (isW ? "Số tiền rút" : "Số tiền nạp") + "</label>" +
          '<div class="money-input" data-money-wrap>' +
            '<input id="money-amount" type="text" inputmode="numeric" placeholder="Nhập số tiền" autocomplete="off" aria-describedby="money-error">' +
            '<span class="money-input__unit">đ</span>' +
            '<button type="button" class="money-input__clear" data-clear aria-label="Xóa số tiền">' + App.icon("x", 12, 3) + "</button>" +
          "</div>" +
          '<p class="field-error" id="money-error" data-error role="alert"></p>' +
        "</div>" +
        '<div class="chips" data-chips>' + chips.map(function (c) {
          return '<button type="button" class="chip" data-chip="' + c + '">' + U.formatVND(c) + "</button>";
        }).join("") + "</div>" +
        '<div data-note></div>' +
      "</div>" +
      '<div class="modal__foot">' +
        '<button type="button" class="btn btn--outline btn--lg" data-close>Hủy</button>' +
        '<button type="button" class="btn ' + (isW ? "btn--orange" : "btn--primary") + ' btn--lg" data-submit>' +
          (isW ? "Xác nhận rút tiền" : "Xác nhận nạp tiền") + "</button>" +
      "</div>";

    const box = App.modal.open({ html: html, initialFocus: "#money-amount", className: "money-modal" });
    const input = box.querySelector("#money-amount");
    const wrap = box.querySelector("[data-money-wrap]");
    const errorEl = box.querySelector("[data-error]");
    const noteEl = box.querySelector("[data-note]");
    const blockEl = box.querySelector("[data-acc-block]");
    const chipsEl = box.querySelector("[data-chips]");
    let touched = false;

    function paintAccount() {
      blockEl.innerHTML = accountBlock(acc, mode);
      const orange = isW && acc.type === "payment";
      wrap.classList.toggle("is-orange", isW);
      chipsEl.classList.toggle("chips--orange", isW);
      box.classList.toggle("money-modal--pay", orange);
    }

    function validate(showEmpty) {
      const v = money.get();
      wrap.classList.toggle("has-value", v > 0);
      U.qsa(".chip", chipsEl).forEach(function (c) { c.classList.toggle("is-active", Number(c.dataset.chip) === v); });
      let error = "";
      let note = "";
      if (!v) {
        if (showEmpty) error = "Vui lòng nhập số tiền lớn hơn 0.";
      } else if (v > 1000000000) {
        error = "Số tiền tối đa mỗi lần " + (isW ? "rút" : "nạp") + " là 1.000.000.000 đ.";
      } else if (isW) {
        const check = S.checkWithdraw(acc.id, v);
        if (!check.ok) error = check.error;
        else if (check.usesOverdraft) {
          note = '<div class="note note--warn">' + App.icon("alert", 20) +
            "<span>Giao dịch hợp lệ: số tiền rút đang sử dụng một phần hạn mức thấu chi. Số dư sau khi rút: <strong>" +
            U.formatVND(acc.balance - v) + "</strong>.</span></div>";
        }
      }
      if (!note && !error) {
        note = isW
          ? '<div class="note note--info">' + App.icon("info", 20) + "<span>Số tiền có thể rút: <strong>" +
            U.formatVND(acc.type === "payment" ? acc.balance + acc.overdraftLimit : Math.max(acc.balance, 0)) + "</strong></span></div>"
          : '<div class="note note--info">' + App.icon("info", 20) + "<span>Giao dịch sẽ được cập nhật ngay sau khi xác nhận</span></div>";
      }
      errorEl.textContent = error;
      wrap.classList.toggle("is-error", !!error);
      input.setAttribute("aria-invalid", error ? "true" : "false");
      noteEl.innerHTML = note;
      return !error && v > 0;
    }

    const money = U.bindMoneyInput(input, function () { validate(touched); });
    paintAccount();
    validate(false);

    const select = box.querySelector("#money-acc");
    if (select) {
      select.addEventListener("change", function () {
        acc = S.getAccount(select.value);
        paintAccount();
        validate(touched);
      });
    }

    box.addEventListener("click", function (e) {
      const chip = e.target.closest("[data-chip]");
      if (chip) { money.set(Number(chip.dataset.chip)); input.focus(); return; }
      if (e.target.closest("[data-clear]")) { money.set(0); input.focus(); return; }
      const cp = e.target.closest("[data-copy]");
      if (cp) { U.copyToClipboard(cp.dataset.copy, "Đã sao chép số tài khoản"); }
    });

    let busy = false;
    async function submit() {
      if (busy) return;
      touched = true;
      if (!validate(true)) { input.focus(); return; }
      const btn = box.querySelector("[data-submit]");
      btn.disabled = true;
      busy = true;
      const amount = money.get();
      // Gửi lên server C#; server kiểm tra lại số dư/thấu chi trước khi ghi
      const res = isW ? await S.withdraw(acc.id, amount) : await S.deposit(acc.id, amount);
      busy = false;
      if (!res.ok) {
        btn.disabled = false;
        errorEl.textContent = res.error;
        wrap.classList.add("is-error");
        return;
      }
      App.modal.close();
      U.showToast((isW ? "Rút tiền thành công: -" : "Nạp tiền thành công: +") + U.formatVND(amount) +
        (isW ? " từ " : " vào ") + res.account.name);
    }
    box.querySelector("[data-submit]").addEventListener("click", submit);
    input.addEventListener("keydown", function (e) { if (e.key === "Enter") { e.preventDefault(); submit(); } });
    return box;
  }

  /* ---------- CHI TIẾT GIAO DỊCH ---------- */
  function txIconHtml(tx, size) {
    const meta = S.TX_TYPES[tx.type] || S.TX_TYPES.deposit;
    return '<span class="tx-ic tx-ic--' + meta.tone + '">' + App.icon(meta.icon, size || 16, 2.4) + "</span>";
  }

  function statusBadge(status) {
    if (status === "pending") return '<span class="badge badge--pending">Đang xử lý</span>';
    if (status === "failed") return '<span class="badge badge--failed">Thất bại</span>';
    return '<span class="badge badge--success">Thành công</span>';
  }

  function openTransaction(txId) {
    const tx = S.getTransaction(txId);
    if (!tx) return;
    const acc = S.getAccount(tx.accountId);
    const meta = S.TX_TYPES[tx.type];
    const isIn = meta.dir === "in";
    const row = function (icon, label, value, cls) {
      return '<div class="info-cell"><span class="info-cell__ic">' + App.icon(icon, 18) + '</span><div><div class="info-cell__label">' +
        label + '</div><div class="info-cell__value' + (cls ? " " + cls : "") + '">' + value + "</div></div></div>";
    };
    App.modal.open({
      html:
        '<div class="tx-detail__head">' + txIconHtml(tx, 22) +
          '<h2 class="modal__title" style="padding:0">Chi tiết giao dịch</h2>' +
          '<div class="tx-detail__amount ' + (isIn ? "money-in" : "money-out") + '">' + U.formatSigned(isIn ? tx.amount : -tx.amount) + "</div>" +
          statusBadge(tx.status) +
        "</div>" +
        '<div class="info-grid" style="margin-top:18px">' +
          row("hash", "Mã giao dịch", tx.code) +
          row("tag", "Loại giao dịch", meta.label) +
          row("wallet", "Tài khoản", acc ? U.escapeHtml(acc.name) + " " + U.maskNumber(acc.accountNumber) : "—") +
          row("clock", "Thời gian", U.formatDateTime(tx.date)) +
          row("receipt", "Nội dung", U.escapeHtml(tx.title)) +
          row("bank", "Chi tiết", U.escapeHtml(tx.subtitle || "—")) +
          row("trendUp", "Số tiền", U.formatVND(tx.amount), "is-big") +
          row("layers", "Số dư sau giao dịch", U.formatVND(tx.balanceAfter), "is-big") +
        "</div>" +
        '<div class="modal__foot" style="grid-template-columns:1fr">' +
          '<button type="button" class="btn btn--outline btn--lg" data-close>Đóng</button>' +
        "</div>"
    });
  }

  return {
    openDeposit: function (id) { return openMoney("deposit", id); },
    openWithdraw: function (id) { return openMoney("withdraw", id); },
    openTransaction: openTransaction,
    txIconHtml: txIconHtml,
    statusBadge: statusBadge,
    accountBlock: accountBlock
  };
})();
