/* =========================================================
   MODAL DÙNG CHUNG
   - Đóng bằng: nút X, nút có data-close, phím Esc, bấm ra ngoài.
   - Khóa cuộn trang, giữ focus trong modal, trả focus về nút đã mở.
   ========================================================= */
window.App = window.App || {};

App.modal = (function () {
  let current = null;
  let uid = 0;

  const FOCUSABLE = 'a[href], button:not([disabled]), input:not([disabled]):not([type="hidden"]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';

  function focusables(root) {
    return Array.prototype.slice.call(root.querySelectorAll(FOCUSABLE))
      .filter(function (el) { return el.offsetParent !== null || el === document.activeElement; });
  }

  function onKey(e) {
    if (!current) return;
    if (e.key === "Escape") { e.preventDefault(); close(); return; }
    if (e.key === "Tab") {
      const els = focusables(current.box);
      if (!els.length) { e.preventDefault(); return; }
      const first = els[0], last = els[els.length - 1];
      if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
      else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
    }
  }

  /**
   * Mở modal.
   * opts: { html, size: 'sm'|'lg', labelledBy, initialFocus: selector, onClose }
   * Trả về phần tử .modal để trang gắn sự kiện.
   */
  function open(opts) {
    const opener = (current && current.opener) || document.activeElement;
    if (current) close(true);
    uid++;
    const overlay = document.createElement("div");
    overlay.className = "modal-overlay";
    const box = document.createElement("div");
    box.className = "modal" + (opts.size ? " modal--" + opts.size : "") + (opts.className ? " " + opts.className : "");
    box.setAttribute("role", "dialog");
    box.setAttribute("aria-modal", "true");
    box.innerHTML =
      '<button type="button" class="modal__close" data-close aria-label="Đóng">' + App.icon("x", 22) + "</button>" +
      opts.html;
    const title = box.querySelector(".modal__title, [data-modal-title]");
    if (title) {
      if (!title.id) title.id = "modal-title-" + uid;
      box.setAttribute("aria-labelledby", title.id);
    }
    overlay.appendChild(box);
    document.body.appendChild(overlay);
    document.body.classList.add("no-scroll");

    current = { overlay: overlay, box: box, opener: opener, onClose: opts.onClose };

    overlay.addEventListener("mousedown", function (e) {
      overlay._downOnSelf = e.target === overlay;
    });
    overlay.addEventListener("click", function (e) {
      if (e.target === overlay && overlay._downOnSelf) close();
    });
    box.addEventListener("click", function (e) {
      if (e.target.closest("[data-close]")) close();
    });
    document.addEventListener("keydown", onKey);

    requestAnimationFrame(function () {
      overlay.classList.add("is-open");
      const target = (opts.initialFocus && box.querySelector(opts.initialFocus)) || focusables(box)[1] || focusables(box)[0];
      if (target) target.focus({ preventScroll: true });
    });
    return box;
  }

  /** Đóng modal. silent=true khi chuyển sang modal khác (không trả focus). */
  function close(silent) {
    if (!current) return;
    const c = current;
    current = null;
    document.removeEventListener("keydown", onKey);
    c.overlay.classList.remove("is-open");
    const remove = function () { c.overlay.remove(); };
    if (silent) remove(); else setTimeout(remove, 200);
    if (!silent) {
      document.body.classList.remove("no-scroll");
      if (c.opener && typeof c.opener.focus === "function" && document.contains(c.opener)) {
        c.opener.focus({ preventScroll: true });
      }
    }
    if (typeof c.onClose === "function") c.onClose();
  }

  function isOpen() { return !!current; }

  return { open: open, close: close, isOpen: isOpen };
})();
