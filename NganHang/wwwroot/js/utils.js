/* =========================================================
   HÀM TIỆN ÍCH DÙNG CHUNG
   ========================================================= */
window.App = window.App || {};

App.utils = (function () {
  /** 12580000 -> "12.580.000" (tự viết để kết quả giống nhau trên mọi trình duyệt) */
  function formatNumber(n) {
    const v = Math.round(Math.abs(Number(n) || 0));
    return String(v).replace(/\B(?=(\d{3})+(?!\d))/g, ".");
  }

  /** 12580000 -> "12.580.000 đ", -500000 -> "-500.000 đ" */
  function formatVND(n) {
    const num = Number(n) || 0;
    return (num < 0 ? "-" : "") + formatNumber(num) + " đ";
  }

  /** Có dấu +/-: "+ 2.500.000 đ" / "- 1.000.000 đ" (dùng trong bảng giao dịch) */
  function formatSigned(n) {
    const num = Number(n) || 0;
    return (num >= 0 ? "+ " : "- ") + formatNumber(num) + " đ";
  }

  /** Rút gọn kiểu 120.5M cho nhãn biểu đồ */
  function formatShort(n) {
    const a = Math.abs(n);
    if (a >= 1e9) return (n / 1e9).toFixed(1).replace(".0", "") + " tỷ";
    if (a >= 1e6) return (n / 1e6).toFixed(1).replace(".0", "") + "tr";
    return formatNumber(n);
  }

  /** Phần trăm kiểu Việt Nam: 79.53 -> "79,5%" */
  function formatPercent(n, digits) {
    digits = digits === undefined ? 1 : digits;
    return (Number(n) || 0).toFixed(digits).replace(".", ",") + "%";
  }

  /** Lấy phần số từ chuỗi người dùng nhập: "1.000.000 đ" -> 1000000 */
  function parseDigits(str) {
    const d = String(str || "").replace(/\D/g, "");
    return d ? Number(d) : 0;
  }

  const pad = (x) => String(x).padStart(2, "0");

  function formatDate(d) {
    d = new Date(d);
    return pad(d.getDate()) + "/" + pad(d.getMonth() + 1) + "/" + d.getFullYear();
  }
  function formatTime(d) {
    d = new Date(d);
    return pad(d.getHours()) + ":" + pad(d.getMinutes());
  }
  function formatDateTime(d) {
    return formatDate(d) + " " + formatTime(d);
  }
  /** yyyy-mm-dd theo giờ địa phương (dùng cho <input type="date">) */
  function toInputDate(d) {
    d = new Date(d);
    return d.getFullYear() + "-" + pad(d.getMonth() + 1) + "-" + pad(d.getDate());
  }

  function addMonths(date, m) {
    const d = new Date(date);
    const day = d.getDate();
    d.setDate(1);
    d.setMonth(d.getMonth() + m);
    const last = new Date(d.getFullYear(), d.getMonth() + 1, 0).getDate();
    d.setDate(Math.min(day, last));
    return d;
  }
  function addDays(date, n) {
    const d = new Date(date);
    d.setDate(d.getDate() + n);
    return d;
  }
  function daysBetween(a, b) {
    const A = new Date(a); A.setHours(0, 0, 0, 0);
    const B = new Date(b); B.setHours(0, 0, 0, 0);
    return Math.round((B - A) / 86400000);
  }
  function endOfDay(d) { d = new Date(d); d.setHours(23, 59, 59, 999); return d; }
  function startOfDay(d) { d = new Date(d); d.setHours(0, 0, 0, 0); return d; }

  /** "0123 4567 8888" -> "****8888" */
  function maskNumber(num) {
    const digits = String(num).replace(/\D/g, "");
    return "****" + digits.slice(-4);
  }

  function escapeHtml(s) {
    return String(s === undefined || s === null ? "" : s)
      .replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;").replace(/'/g, "&#39;");
  }

  /** Bỏ dấu tiếng Việt + chữ thường để tìm kiếm: "Nguyễn Văn Minh" -> "nguyen van minh" */
  function normalizeText(s) {
    return String(s || "")
      .normalize("NFD").replace(/[\u0300-\u036f]/g, "")
      .replace(/đ/g, "d").replace(/Đ/g, "D")
      .toLowerCase().trim();
  }

  /* ---------- TOAST ---------- */
  function showToast(message, type) {
    type = type || "success";
    let stack = document.querySelector(".toast-stack");
    if (!stack) {
      stack = document.createElement("div");
      stack.className = "toast-stack";
      stack.setAttribute("role", "status");
      stack.setAttribute("aria-live", "polite");
      document.body.appendChild(stack);
    }
    const el = document.createElement("div");
    el.className = "toast toast--" + type;
    const iconName = type === "error" ? "alert" : type === "info" ? "info" : "checkCircle";
    el.innerHTML = App.icon(iconName, 20) + "<span></span>";
    el.querySelector("span").textContent = message;
    stack.appendChild(el);
    setTimeout(function () {
      el.classList.add("is-leaving");
      setTimeout(function () { el.remove(); }, 260);
    }, 2500);
    return el;
  }

  /* ---------- COPY ---------- */
  function copyToClipboard(text, successMsg) {
    const done = function () { showToast(successMsg || "Đã sao chép"); };
    const fallback = function () {
      const ta = document.createElement("textarea");
      ta.value = text;
      ta.setAttribute("readonly", "");
      ta.style.position = "fixed"; ta.style.opacity = "0";
      document.body.appendChild(ta);
      ta.select();
      let ok = false;
      try { ok = document.execCommand("copy"); } catch (e) { ok = false; }
      ta.remove();
      if (ok) done(); else showToast("Không sao chép được, hãy thử lại", "error");
    };
    if (navigator.clipboard && window.isSecureContext) {
      navigator.clipboard.writeText(text).then(done, fallback);
    } else {
      fallback();
    }
  }

  /**
   * Gắn định dạng tiền cho ô nhập: chỉ nhận số, tự thêm dấu chấm, giữ vị trí con trỏ.
   * onChange(value:number) được gọi mỗi lần đổi.
   */
  function bindMoneyInput(input, onChange, maxDigits) {
    maxDigits = maxDigits || 13;
    function apply() {
      const raw = input.value;
      const caret = input.selectionStart === null ? raw.length : input.selectionStart;
      const digitsBefore = raw.slice(0, caret).replace(/\D/g, "").replace(/^0+(?=\d)/, "").length;
      let digits = raw.replace(/\D/g, "").replace(/^0+/, "").slice(0, maxDigits);
      const formatted = digits ? formatNumber(Number(digits)) : "";
      input.value = formatted;
      // đặt lại con trỏ sau đúng số chữ số đã gõ
      let pos = 0, seen = 0;
      while (pos < formatted.length && seen < digitsBefore) {
        if (/\d/.test(formatted[pos])) seen++;
        pos++;
      }
      try { if (document.activeElement === input) input.setSelectionRange(pos, pos); } catch (e) { /* bỏ qua */ }
      if (onChange) onChange(digits ? Number(digits) : 0);
    }
    input.addEventListener("input", apply);
    return {
      set: function (v) {
        input.value = v ? formatNumber(v) : "";
        if (onChange) onChange(v || 0);
      },
      get: function () { return parseDigits(input.value); }
    };
  }

  /** Hiệu ứng số chạy từ 0 đến giá trị thật */
  function countUp(el, to, duration) {
    duration = duration || 1000;
    const reduce = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (reduce) { el.textContent = formatVND(to); return; }
    const start = performance.now();
    function frame(now) {
      const p = Math.min(1, (now - start) / duration);
      const eased = 1 - Math.pow(1 - p, 3);
      el.textContent = formatVND(Math.round(to * eased));
      if (p < 1) requestAnimationFrame(frame);
    }
    requestAnimationFrame(frame);
  }

  function qs(sel, root) { return (root || document).querySelector(sel); }
  function qsa(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }

  return {
    formatNumber, formatVND, formatSigned, formatShort, formatPercent, parseDigits,
    formatDate, formatTime, formatDateTime, toInputDate,
    addMonths, addDays, daysBetween, endOfDay, startOfDay,
    maskNumber, escapeHtml, normalizeText,
    showToast, copyToClipboard, bindMoneyInput, countUp, qs, qsa
  };
})();
