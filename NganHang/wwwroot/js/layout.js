/* =========================================================
   LAYOUT CHUNG: chèn Sidebar + Header vào mọi trang
   Dùng: App.layout.render('home' | 'accounts' | 'transactions' | 'transfer' | 'interest')
   ========================================================= */
window.App = window.App || {};

App.layout = (function () {
  const U = App.utils;
  const S = App.store;

  const MENU = [
    { key: "home", label: "Trang chủ", icon: "home", href: "index.html" },
    { key: "accounts", label: "Tài khoản của tôi", icon: "user", href: "tai-khoan.html" },
    { key: "transactions", label: "Giao dịch", icon: "receipt", href: "giao-dich.html" },
    { key: "transfer", label: "Chuyển tiền", icon: "send", href: "chuyen-tien.html" },
    { key: "interest", label: "Cộng lãi", icon: "barChart", href: "cong-lai.html" },
    { key: "statistics", label: "Thống kê", icon: "pieChart", href: "thong-ke.html" }
  ];

  let activeKey = "home";

  function sidebarHtml() {
    const items = MENU.map(function (m) {
      const active = m.key === activeKey;
      return '<li><a class="nav-link' + (active ? " is-active" : "") + '" href="' + m.href + '"' +
        (active ? ' aria-current="page"' : "") + ' title="' + m.label + '">' +
        App.icon(m.icon, 22) + "<span>" + m.label + "</span></a></li>";
    }).join("");
    return (
      '<div class="sidebar__art" aria-hidden="true"></div>' +
      '<a class="sidebar__logo" href="index.html" aria-label="Hệ thống Ngân hàng – Trang chủ">' + App.logoSvg(44) +
      '<span class="sidebar__logo-text">Hệ thống<br>Ngân hàng</span></a>' +
      '<ul class="sidebar__nav">' + items +
      '<li><button type="button" class="nav-link" data-action="logout" title="Đăng xuất">' + App.icon("logout", 22) + "<span>Đăng xuất</span></button></li>" +
      "</ul>" +
      '<p class="sidebar__foot">Tài chính vững vàng<br>Cuộc sống tươi sáng</p>'
    );
  }

  function headerHtml() {
    const user = S.getUser();
    return (
      '<button type="button" class="menu-toggle" aria-label="Mở menu" aria-expanded="false">' + App.icon("menu", 22) + "</button>" +
      '<form class="search" role="search" data-search>' + App.icon("search", 20) +
      '<label class="sr-only" for="global-search">Tìm kiếm</label>' +
      '<input id="global-search" type="search" placeholder="Tìm kiếm giao dịch, tài khoản, tính năng..." autocomplete="off"></form>' +
      '<div class="topbar__right">' +
        '<div class="dropdown-wrap">' +
          '<button type="button" class="icon-btn" data-toggle="notif" aria-label="Thông báo" aria-haspopup="true" aria-expanded="false">' +
            App.icon("bell", 24) + '<span class="badge-count" data-notif-count></span></button>' +
          '<div class="dropdown notif-list" data-dropdown="notif" role="menu"></div>' +
        "</div>" +
        '<div class="dropdown-wrap">' +
          '<button type="button" class="user-btn" data-toggle="user" aria-haspopup="true" aria-expanded="false">' +
            '<img src="assets/images/avatar.png" alt="">' +
            "<span>" + U.escapeHtml(user.name) + "</span>" + App.icon("chevronDown", 16) +
          "</button>" +
          '<div class="dropdown" data-dropdown="user" role="menu">' +
            '<button type="button" class="dropdown__item" data-action="profile">' + App.icon("user", 18) + "Hồ sơ cá nhân</button>" +
            '<button type="button" class="dropdown__item" data-action="security">' + App.icon("settings", 18) + "Cài đặt bảo mật</button>" +
            '<div class="dropdown__sep"></div>' +
            '<button type="button" class="dropdown__item is-danger" data-action="logout">' + App.icon("logout", 18) + "Đăng xuất</button>" +
          "</div>" +
        "</div>" +
      "</div>"
    );
  }

  function relTime(iso) {
    const diff = (Date.now() - new Date(iso).getTime()) / 60000;
    if (diff < 1) return "Vừa xong";
    if (diff < 60) return Math.floor(diff) + " phút trước";
    if (diff < 1440) return Math.floor(diff / 60) + " giờ trước";
    if (diff < 2880) return "Hôm qua";
    return U.formatDate(iso);
  }

  function renderNotifications() {
    const list = S.getNotifications();
    const unread = list.filter(function (n) { return !n.read; }).length;
    const badge = document.querySelector("[data-notif-count]");
    if (badge) {
      badge.textContent = unread > 9 ? "9+" : String(unread);
      badge.style.display = unread ? "" : "none";
    }
    const btn = document.querySelector('[data-toggle="notif"]');
    if (btn) btn.setAttribute("aria-label", "Thông báo" + (unread ? " (" + unread + " chưa đọc)" : ""));
    const box = document.querySelector('[data-dropdown="notif"]');
    if (!box) return;
    const items = list.slice(0, 6).map(function (n) {
      return '<button type="button" class="notif' + (n.read ? "" : " is-unread") + '" data-notif="' + n.id + '">' +
        '<span class="notif__dot"></span><span><span class="notif__title">' + U.escapeHtml(n.title) +
        '</span><span class="notif__time" style="display:block">' + relTime(n.date) + "</span></span></button>";
    }).join("");
    box.innerHTML =
      '<div class="dropdown__title">Thông báo' + (unread ? '<button type="button" data-action="read-all">Đánh dấu đã đọc</button>' : "") + "</div>" +
      (items || '<div class="empty">Bạn chưa có thông báo nào</div>');
  }

  function closeDropdowns(except) {
    U.qsa(".dropdown.is-open").forEach(function (d) {
      if (d === except) return;
      d.classList.remove("is-open");
      const t = document.querySelector('[data-toggle="' + d.dataset.dropdown + '"]');
      if (t) t.setAttribute("aria-expanded", "false");
    });
  }

  /* ---------- MODAL ĐĂNG XUẤT ---------- */
  function openLogout() {
    closeDropdowns();
    closeSidebar();
    const navBtn = document.querySelector('.sidebar [data-action="logout"]');
    const activeLink = document.querySelector(".sidebar .nav-link.is-active");
    if (navBtn) navBtn.classList.add("is-active");
    if (activeLink) activeLink.classList.remove("is-active");
    const box = App.modal.open({
      size: "lg",
      className: "logout-modal",
      initialFocus: "[data-cancel]",
      html:
        '<img class="logout-modal__art" src="assets/images/logout-door.png" alt="">' +
        '<h2 class="modal__title logout-modal__title">Bạn có chắc muốn đăng xuất?</h2>' +
        '<p class="logout-modal__desc">Phiên làm việc hiện tại sẽ kết thúc và bạn cần đăng nhập lại để tiếp tục sử dụng hệ thống Ngân hàng.</p>' +
        '<div class="modal__foot logout-modal__foot">' +
          '<button type="button" class="btn btn--soft btn--lg" data-close data-cancel>Hủy</button>' +
          '<button type="button" class="btn btn--danger btn--lg" data-confirm>' + App.icon("logout", 20) + "Đăng xuất</button>" +
        "</div>" +
        '<div class="logout-modal__or"><span>hoặc</span></div>' +
        '<a class="logout-modal__home" href="index.html">' + App.icon("home", 20) + "Quay về trang chủ</a>" +
        '<div class="logout-modal__tip">' + App.icon("shield", 22) + "<div><strong>Mẹo bảo mật:</strong> Hãy đăng xuất sau khi sử dụng trên thiết bị công cộng.</div></div>",
      onClose: function () {
        if (navBtn) navBtn.classList.remove("is-active");
        if (activeLink) activeLink.classList.add("is-active");
      }
    });
    box.querySelector("[data-confirm]").addEventListener("click", function () {
      const b = this;
      b.disabled = true;
      b.innerHTML = '<span class="spinner"></span>Đang đăng xuất...';
      try { sessionStorage.clear(); } catch (e) { /* bỏ qua */ }
      setTimeout(function () { window.location.replace("login.html"); }, 900);
    });
  }

  /* ---------- MODAL CÀI ĐẶT BẢO MẬT (dùng ở header và card An toàn) ---------- */
  function deviceName() {
    const ua = navigator.userAgent;
    const browser = /Edg\//.test(ua) ? "Microsoft Edge" : /OPR\//.test(ua) ? "Opera" : /Chrome\//.test(ua) ? "Chrome" :
      /Firefox\//.test(ua) ? "Firefox" : /Safari\//.test(ua) ? "Safari" : "Trình duyệt";
    const os = /Windows/.test(ua) ? "Windows" : /Android/.test(ua) ? "Android" : /iPhone|iPad/.test(ua) ? "iOS" :
      /Mac OS/.test(ua) ? "macOS" : /Linux/.test(ua) ? "Linux" : "thiết bị này";
    return browser + " trên " + os;
  }

  function openSecurity() {
    closeDropdowns();
    const user = S.getUser();
    const box = App.modal.open({
      size: "sm",
      html:
        '<h2 class="modal__title">Cài đặt bảo mật</h2>' +
        '<p class="modal__desc">Theo dõi phiên đăng nhập và bật xác thực để bảo vệ tài khoản.</p>' +
        '<div class="modal__body">' +
          '<div class="sec-row"><span class="info-cell__ic">' + App.icon("clock", 18) + '</span><div><div class="info-cell__label">Lần đăng nhập gần nhất</div><div class="info-cell__value">' + U.formatDateTime(user.lastLogin) + "</div></div></div>" +
          '<div class="sec-row"><span class="info-cell__ic">' + App.icon("phone", 18) + '</span><div><div class="info-cell__label">Thiết bị đang đăng nhập</div><div class="info-cell__value">' + U.escapeHtml(deviceName()) + "</div></div></div>" +
          '<div class="sec-row"><span class="info-cell__ic">' + App.icon("shield", 18) + '</span><div style="flex:1"><div class="info-cell__label">Xác thực OTP khi giao dịch</div><div class="info-cell__value" data-otp-text>' + (user.otpEnabled ? "Đang bật" : "Đang tắt") + "</div></div>" +
            '<label class="switch"><input type="checkbox" data-otp ' + (user.otpEnabled ? "checked" : "") + ' aria-label="Bật xác thực OTP"><span></span></label></div>' +
        "</div>" +
        '<div class="modal__foot">' +
          '<button type="button" class="btn btn--outline" data-close>Đóng</button>' +
          '<button type="button" class="btn btn--primary" data-change-pass>' + App.icon("key", 18) + "Đổi mật khẩu</button>" +
        "</div>"
    });
    box.querySelector("[data-otp]").addEventListener("change", function () {
      S.setOtp(this.checked);
      box.querySelector("[data-otp-text]").textContent = this.checked ? "Đang bật" : "Đang tắt";
      U.showToast(this.checked ? "Đã bật xác thực OTP" : "Đã tắt xác thực OTP", this.checked ? "success" : "info");
    });
    box.querySelector("[data-change-pass]").addEventListener("click", function () {
      App.modal.close();
      U.showToast("Đã gửi liên kết đổi mật khẩu tới " + user.email, "info");
    });
  }

  function openProfile() {
    closeDropdowns();
    const user = S.getUser();
    const accounts = S.getAccounts();
    const box = App.modal.open({
      size: "sm",
      html:
        '<div class="profile-head"><img src="assets/images/avatar.png" alt=""><div><h2 class="modal__title" style="padding:0">' + U.escapeHtml(user.name) + '</h2><p class="modal__desc" style="margin-top:2px">Khách hàng cá nhân</p></div></div>' +
        '<div class="modal__body">' +
          '<div class="sec-row"><span class="info-cell__ic">' + App.icon("mail", 18) + '</span><div><div class="info-cell__label">Email</div><div class="info-cell__value">' + U.escapeHtml(user.email) + "</div></div></div>" +
          '<div class="sec-row"><span class="info-cell__ic">' + App.icon("phone", 18) + '</span><div><div class="info-cell__label">Số điện thoại</div><div class="info-cell__value">' + U.escapeHtml(user.phone) + "</div></div></div>" +
          '<div class="sec-row"><span class="info-cell__ic">' + App.icon("wallet", 18) + '</span><div><div class="info-cell__label">Số tài khoản đang mở</div><div class="info-cell__value">' + accounts.length + " tài khoản</div></div></div>" +
          '<div class="note note--info">' + App.icon("info", 18) + "<span>Đây là bản demo. Bạn có thể khôi phục dữ liệu mẫu ban đầu bất cứ lúc nào.</span></div>" +
        "</div>" +
        '<div class="modal__foot">' +
          '<button type="button" class="btn btn--outline" data-close>Đóng</button>' +
          '<button type="button" class="btn btn--soft" data-reset>' + App.icon("refresh", 18) + "Khôi phục dữ liệu mẫu</button>" +
        "</div>"
    });
    const resetBtn = box.querySelector("[data-reset]");
    resetBtn.addEventListener("click", function () {
      if (resetBtn.dataset.confirm !== "1") {
        resetBtn.dataset.confirm = "1";
        resetBtn.innerHTML = App.icon("alert", 18) + "Bấm lần nữa để xác nhận";
        resetBtn.classList.add("btn--danger");
        resetBtn.classList.remove("btn--soft");
        return;
      }
      resetBtn.disabled = true;
      S.reset().then(function (r) {
        App.modal.close();
        if (r && r.ok) U.showToast("Đã khôi phục dữ liệu mẫu");
        else U.showToast((r && r.error) || "Không khôi phục được dữ liệu", "error");
      });
    });
  }

  /* ---------- SIDEBAR MOBILE ---------- */
  function openSidebar() {
    document.querySelector(".sidebar").classList.add("is-open");
    document.querySelector(".sidebar-backdrop").classList.add("is-open");
    document.querySelector(".menu-toggle").setAttribute("aria-expanded", "true");
  }
  function closeSidebar() {
    const sb = document.querySelector(".sidebar");
    if (!sb) return;
    sb.classList.remove("is-open");
    document.querySelector(".sidebar-backdrop").classList.remove("is-open");
    const t = document.querySelector(".menu-toggle");
    if (t) t.setAttribute("aria-expanded", "false");
  }

  /** Đổi <i data-ic="ten" data-size="20"></i> trong HTML tĩnh thành icon SVG */
  function hydrateIcons(root) {
    U.qsa("i[data-ic]", root).forEach(function (i) {
      const wrap = document.createElement("span");
      wrap.innerHTML = App.icon(i.dataset.ic, Number(i.dataset.size) || 20, Number(i.dataset.stroke) || 2);
      i.replaceWith(wrap.firstChild);
    });
  }

  /* ---------- KHỞI TẠO ---------- */
  function render(key) {
    activeKey = key;
    const sidebar = document.getElementById("app-sidebar");
    const header = document.getElementById("app-header");
    sidebar.className = "sidebar";
    sidebar.setAttribute("aria-label", "Menu chính");
    sidebar.innerHTML = sidebarHtml();
    header.className = "topbar";
    header.innerHTML = headerHtml();
    const backdrop = document.createElement("div");
    backdrop.className = "sidebar-backdrop";
    document.body.appendChild(backdrop);
    renderNotifications();
    hydrateIcons(document);

    // Tìm kiếm: Enter -> trang Giao dịch kèm từ khóa
    header.querySelector("[data-search]").addEventListener("submit", function (e) {
      e.preventDefault();
      const q = header.querySelector("#global-search").value.trim();
      window.location.href = "giao-dich.html" + (q ? "?q=" + encodeURIComponent(q) : "");
    });
    const params = new URLSearchParams(window.location.search);
    if (params.get("q")) header.querySelector("#global-search").value = params.get("q");

    // Mở/đóng dropdown
    header.addEventListener("click", function (e) {
      const toggle = e.target.closest("[data-toggle]");
      if (toggle) {
        const dd = header.querySelector('[data-dropdown="' + toggle.dataset.toggle + '"]');
        const willOpen = !dd.classList.contains("is-open");
        closeDropdowns(dd);
        dd.classList.toggle("is-open", willOpen);
        toggle.setAttribute("aria-expanded", String(willOpen));
        return;
      }
      const n = e.target.closest("[data-notif]");
      if (n) { S.markNotificationRead(n.dataset.notif); return; }
      if (e.target.closest('[data-action="read-all"]')) { S.markAllNotificationsRead(); return; }
    });
    document.addEventListener("click", function (e) {
      if (!document.contains(e.target)) return; // phần tử vừa được vẽ lại
      if (!e.target.closest(".dropdown-wrap")) closeDropdowns();
    });
    document.addEventListener("keydown", function (e) {
      if (e.key === "Escape" && !App.modal.isOpen()) { closeDropdowns(); closeSidebar(); }
    });

    // Hành động chung
    document.addEventListener("click", function (e) {
      const a = e.target.closest("[data-action]");
      if (!a) return;
      const act = a.dataset.action;
      if (act === "logout") { e.preventDefault(); openLogout(); }
      else if (act === "security") { e.preventDefault(); openSecurity(); }
      else if (act === "profile") { e.preventDefault(); openProfile(); }
    });

    header.querySelector(".menu-toggle").addEventListener("click", openSidebar);
    backdrop.addEventListener("click", closeSidebar);

    S.subscribe(function () { renderNotifications(); });
  }

  return { render: render, openLogout: openLogout, openSecurity: openSecurity, hydrateIcons: hydrateIcons };
})();
