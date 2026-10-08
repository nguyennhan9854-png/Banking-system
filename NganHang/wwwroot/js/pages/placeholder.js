/* Trang Chuyển tiền / Cộng lãi (tạm thời): chỉ dựng layout chung */
App.boot(function () {
  App.layout.render(document.querySelector("[data-page-key]").dataset.pageKey);
});
