/* =========================================================
   KẾT NỐI SERVER C# (ASP.NET Core Web API)
   - Dữ liệu và nghiệp vụ nằm ở server (thư mục Services/, Controllers/).
   - File này giữ 1 bản sao trạng thái mới nhất (GET /api/state) để giao diện đọc nhanh,
     mọi thao tác ghi đều gửi lên server rồi tải lại trạng thái.
   - Các tab đang mở được báo cập nhật qua BroadcastChannel.
   ========================================================= */
window.App = window.App || {};

App.store = (function () {
  const API = "api";
  const listeners = [];
  let state = null;

  /* Thông tin hiển thị theo loại giao dịch (chỉ dùng cho giao diện) */
  const TX_TYPES = {
    deposit:      { label: "Nạp tiền",           dir: "in",  icon: "arrowDown",     tone: "in" },
    open:         { label: "Nạp tiền",           dir: "in",  icon: "arrowDown",     tone: "in" },
    withdraw:     { label: "Rút tiền",           dir: "out", icon: "arrowUp",       tone: "out" },
    transfer_in:  { label: "Nhận tiền",          dir: "in",  icon: "arrowDownLeft", tone: "in" },
    transfer_out: { label: "Chuyển khoản",       dir: "out", icon: "arrowUpRight",  tone: "out" },
    interest:     { label: "Cộng lãi",           dir: "in",  icon: "percent",       tone: "interest" },
    bill:         { label: "Thanh toán hóa đơn", dir: "out", icon: "bill",          tone: "bill" },
    shopping:     { label: "Mua sắm",            dir: "out", icon: "bag",           tone: "shop" }
  };

  /* ---------- Gọi API ---------- */
  async function request(method, path, body) {
    let res;
    try {
      res = await fetch(API + path, {
        method: method,
        headers: body !== undefined ? { "Content-Type": "application/json" } : {},
        body: body !== undefined ? JSON.stringify(body) : undefined,
        cache: "no-store"
      });
    } catch (e) {
      return { ok: false, error: "Không kết nối được máy chủ. Hãy kiểm tra server C# đang chạy." };
    }
    let data = null;
    if (res.status !== 204) {
      try { data = await res.json(); } catch (e) { data = null; }
    }
    if (!res.ok) {
      const msg = (data && (data.error || data.title)) || ("Máy chủ báo lỗi (" + res.status + ").");
      return { ok: false, status: res.status, error: msg };
    }
    return { ok: true, data: data };
  }

  /* ---------- Đồng bộ ---------- */
  const channel = typeof BroadcastChannel !== "undefined" ? new BroadcastChannel("nhbank") : null;

  function emit(reason) {
    listeners.slice().forEach(function (fn) {
      try { fn(reason); } catch (e) { console.error(e); }
    });
  }

  async function refresh(reason) {
    const r = await request("GET", "/state");
    if (r.ok) {
      state = r.data;
      emit(reason || "refresh");
    }
    return r;
  }

  /** Sau khi ghi: tải lại trạng thái + báo các tab khác */
  async function afterWrite(reason) {
    await refresh(reason);
    if (channel) channel.postMessage(reason);
  }

  if (channel) channel.onmessage = function () { refresh("external"); };
  // Quay lại tab thì lấy dữ liệu mới nhất (phòng khi trình duyệt không có BroadcastChannel)
  document.addEventListener("visibilitychange", function () {
    if (document.visibilityState === "visible" && state) refresh("external");
  });

  /** Promise: true khi đã tải xong dữ liệu lần đầu, false nếu không kết nối được */
  const ready = refresh("init").then(function (r) { return r.ok; });

  /* ---------- Hàm đọc (đồng bộ, dùng bản sao mới nhất) ---------- */
  function getAccounts() { return state ? state.accounts.slice() : []; }
  function getAccount(id) { return getAccounts().find(function (a) { return a.id === id; }) || null; }
  function getPrimary(type) { return getAccounts().find(function (a) { return a.type === type; }) || null; }

  /** Giao dịch mới nhất trước; balanceAfter do server tính riêng từng tài khoản */
  function getTransactions(filter) {
    filter = filter || {};
    let list = state ? state.transactions.slice() : [];
    if (filter.accountId) list = list.filter(function (t) { return t.accountId === filter.accountId; });
    if (filter.limit) list = list.slice(0, filter.limit);
    return list;
  }
  function getTransaction(id) {
    return getTransactions().find(function (t) { return t.id === id; }) || null;
  }

  function totalBalance(type) {
    return getAccounts()
      .filter(function (a) { return !type || a.type === type; })
      .reduce(function (s, a) { return s + a.balance; }, 0);
  }

  /** % thay đổi so với 30 ngày trước (server tính sẵn) */
  function growth(accountId) {
    const a = getAccount(accountId);
    return a && a.growthPercent !== null && a.growthPercent !== undefined ? a.growthPercent : null;
  }

  /** Kỳ hạn + lãi dự kiến (server tính sẵn) */
  function savingTerm(acc) {
    if (!acc || !acc.term) return null;
    return {
      start: new Date(acc.term.start),
      maturity: new Date(acc.term.maturity),
      days: acc.term.days,
      estimatedInterest: acc.term.estimatedInterest
    };
  }

  /**
   * Kiểm tra nhanh khi đang gõ số tiền (để hiện cảnh báo ngay).
   * Server vẫn kiểm tra lại lần nữa khi bấm xác nhận.
   */
  function checkWithdraw(accountId, amount, verb) {
    verb = verb || "rút";
    const acc = getAccount(accountId);
    if (!acc) return { ok: false, error: "Không tìm thấy tài khoản." };
    if (!amount || amount <= 0) return { ok: false, error: "Vui lòng nhập số tiền lớn hơn 0." };
    const available = acc.availableBalance;
    if (amount > available) {
      return {
        ok: false, available: available,
        error: acc.type === "payment"
          ? "Số tiền " + verb + " vượt quá hạn mức khả dụng."
          : "Số dư không đủ. Số tiền " + verb + " tối đa là " + App.utils.formatVND(available) + "."
      };
    }
    return { ok: true, available: available, usesOverdraft: acc.type === "payment" && amount > acc.balance };
  }

  /* ---------- Hàm gọi server (bất đồng bộ) ---------- */

  /** Lịch sử số dư cho biểu đồ → Promise<[{date, value, label}]> */
  async function balanceSeries(accountIds, from, to, mode) {
    const ids = Array.isArray(accountIds) ? accountIds : [accountIds];
    const U = App.utils;
    const qs = "?accountIds=" + encodeURIComponent(ids.join(",")) +
      "&from=" + U.toInputDate(from) + "&to=" + U.toInputDate(to) + "&mode=" + encodeURIComponent(mode);
    const r = await request("GET", "/balance-history" + qs);
    if (!r.ok) throw new Error(r.error);
    return r.data.map(function (p) { return { date: new Date(p.date), value: p.value, label: p.label }; });
  }

  async function deposit(accountId, amount) {
    const r = await request("POST", "/accounts/" + encodeURIComponent(accountId) + "/deposit", { amount: amount });
    if (!r.ok) return { ok: false, error: r.error };
    await afterWrite("deposit");
    return { ok: true, account: getAccount(accountId) || r.data.account, tx: r.data.transaction };
  }

  async function withdraw(accountId, amount) {
    const r = await request("POST", "/accounts/" + encodeURIComponent(accountId) + "/withdraw", { amount: amount });
    if (!r.ok) return { ok: false, error: r.error };
    await afterWrite("withdraw");
    return { ok: true, account: getAccount(accountId) || r.data.account, tx: r.data.transaction, usesOverdraft: r.data.usesOverdraft };
  }

  /**
   * Chuyển tiền. opts = { fromAccountId, toAccountId? | (toAccountNumber, toName, toBank), amount, note }
   * Server kiểm tra lại số dư và hạn mức thấu chi trước khi ghi.
   */
  async function transfer(opts) {
    const r = await request("POST", "/transfers", {
      fromAccountId: opts.fromAccountId,
      toAccountId: opts.toAccountId || null,
      toAccountNumber: opts.toAccountNumber || null,
      toName: opts.toName || null,
      toBank: opts.toBank || null,
      amount: Number(opts.amount) || 0,
      note: opts.note || null
    });
    if (!r.ok) return { ok: false, error: r.error };
    await afterWrite("transfer");
    return {
      ok: true,
      from: getAccount(opts.fromAccountId) || r.data.from,
      outTx: getTransaction(r.data.outTransaction.id) || r.data.outTransaction,
      inTx: r.data.inTransaction,
      usesOverdraft: r.data.usesOverdraft
    };
  }

  /** Nhận ngay tiền lãi tạm tính của 1 tài khoản tiết kiệm */
  async function applyInterest(accountId) {
    const r = await request("POST", "/interest/" + encodeURIComponent(accountId) + "/apply");
    if (!r.ok) return { ok: false, error: r.error };
    await afterWrite("interest");
    return { ok: true, transactions: r.data.transactions, total: r.data.totalAmount };
  }

  /** Chạy đợt cộng lãi tự động ngay (chỉ cộng các kỳ đã đến hạn) */
  async function runInterest() {
    const r = await request("POST", "/interest/run");
    if (!r.ok) return { ok: false, error: r.error };
    await afterWrite("interest");
    return { ok: true, transactions: r.data.transactions, total: r.data.totalAmount };
  }

  /** opts = { type, termMonths, initialAmount, overdraftLimit } */
  async function openAccount(opts) {
    const r = await request("POST", "/accounts", {
      type: opts.type,
      termMonths: Number(opts.termMonths) || 0,
      initialAmount: Number(opts.initialAmount) || 0,
      overdraftLimit: Number(opts.overdraftLimit) || 0
    });
    if (!r.ok) return { ok: false, error: r.error };
    await afterWrite("open-account");
    return { ok: true, account: getAccount(r.data.id) || r.data };
  }

  async function markNotificationRead(id) {
    const n = state && state.notifications.find(function (x) { return x.id === id; });
    if (!n || n.read) return;
    n.read = true; // cập nhật ngay trên giao diện cho mượt
    emit("notification");
    await request("POST", "/notifications/" + encodeURIComponent(id) + "/read");
    await afterWrite("notification");
  }
  async function markAllNotificationsRead() {
    if (state) state.notifications.forEach(function (n) { n.read = true; });
    emit("notification");
    await request("POST", "/notifications/read-all");
    await afterWrite("notification");
  }
  async function setOtp(enabled) {
    if (state) state.user.otpEnabled = !!enabled;
    const r = await request("PUT", "/user/otp", { enabled: !!enabled });
    await afterWrite("settings");
    return r;
  }
  async function reset() {
    const r = await request("POST", "/system/reset");
    await afterWrite("reset");
    return r;
  }

  function subscribe(fn) {
    listeners.push(fn);
    return function () { const i = listeners.indexOf(fn); if (i >= 0) listeners.splice(i, 1); };
  }

  return {
    TX_TYPES: TX_TYPES,
    get SAVING_RATES() { return state ? state.savingRates : {}; },
    ready: ready,
    refresh: refresh,
    getUser: function () { return state ? state.user : { name: "", email: "", phone: "", lastLogin: new Date().toISOString(), otpEnabled: false }; },
    getNotifications: function () { return state ? state.notifications.slice() : []; },
    getAccounts: getAccounts, getAccount: getAccount, getPrimary: getPrimary,
    getTransactions: getTransactions, getTransaction: getTransaction,
    totalBalance: totalBalance, growth: growth, savingTerm: savingTerm, checkWithdraw: checkWithdraw,
    balanceSeries: balanceSeries, deposit: deposit, withdraw: withdraw, transfer: transfer, applyInterest: applyInterest, runInterest: runInterest, openAccount: openAccount,
    markNotificationRead: markNotificationRead, markAllNotificationsRead: markAllNotificationsRead,
    setOtp: setOtp, reset: reset, subscribe: subscribe
  };
})();

/**
 * Chạy hàm khởi tạo trang sau khi đã tải dữ liệu từ server.
 * Nếu không kết nối được thì hiện hướng dẫn chạy server.
 */
App.boot = function (init) {
  App.store.ready.then(function (ok) {
    if (ok) { init(); return; }
    document.body.innerHTML =
      '<div style="min-height:100vh;display:flex;align-items:center;justify-content:center;padding:20px">' +
      '<div class="card" style="max-width:520px;text-align:center;padding:36px">' +
      '<h1 style="font-size:22px;color:var(--navy)">Chưa kết nối được máy chủ</h1>' +
      '<p style="color:var(--text-2);margin:10px 0 18px">Trang web cần server C# đang chạy. Mở terminal trong thư mục <b>NganHang</b>, chạy lệnh dưới đây rồi vào <b>http://localhost:5000</b>.</p>' +
      '<pre style="background:#0B1F5C;color:#fff;padding:12px;border-radius:10px;text-align:left">dotnet run</pre>' +
      '<button class="btn btn--primary" style="margin-top:16px" onclick="location.reload()">Thử lại</button></div></div>';
  });
};
