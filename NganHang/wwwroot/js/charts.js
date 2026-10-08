/* =========================================================
   BIỂU ĐỒ SVG TỰ VẼ (không phụ thuộc thư viện ngoài → chạy offline)
   - App.charts.line(el, { points:[{label, value, date}], height, highlightLast, animate })
   - App.charts.donut(el, { segments:[{label, value, color}], centerHtml, size, thickness })
   ========================================================= */
window.App = window.App || {};

App.charts = (function () {
  const U = App.utils;
  const NS = "http://www.w3.org/2000/svg";
  let uid = 0;

  /* Chia trục Y "đẹp": 0, 5.000.000, 10.000.000 ... */
  function niceScale(min, max, ticks) {
    if (max === min) { max = min + (Math.abs(min) || 1000000); }
    const range = max - min;
    const rough = range / ticks;
    const pow = Math.pow(10, Math.floor(Math.log10(rough)));
    const steps = [1, 2, 2.5, 5, 10];
    let step = pow * 10;
    for (let i = 0; i < steps.length; i++) {
      if (steps[i] * pow >= rough) { step = steps[i] * pow; break; }
    }
    const niceMin = Math.floor(min / step) * step;
    const niceMax = Math.ceil(max / step) * step;
    const values = [];
    for (let v = niceMin; v <= niceMax + step / 2; v += step) values.push(Math.round(v));
    return { min: niceMin, max: niceMax, ticks: values };
  }

  /* Đường cong đơn điệu (không vọt quá điểm dữ liệu) – Fritsch–Carlson */
  function monotonePath(pts) {
    const n = pts.length;
    if (n < 2) return "";
    if (n === 2) return "M" + pts[0].x + "," + pts[0].y + "L" + pts[1].x + "," + pts[1].y;
    const dx = [], dy = [], m = [], t = [];
    for (let i = 0; i < n - 1; i++) {
      dx[i] = pts[i + 1].x - pts[i].x;
      dy[i] = pts[i + 1].y - pts[i].y;
      m[i] = dy[i] / dx[i];
    }
    t[0] = m[0];
    t[n - 1] = m[n - 2];
    for (let i = 1; i < n - 1; i++) {
      if (m[i - 1] * m[i] <= 0) t[i] = 0;
      else t[i] = (3 * (dx[i - 1] + dx[i])) / ((2 * dx[i] + dx[i - 1]) / m[i - 1] + (dx[i] + 2 * dx[i - 1]) / m[i]);
    }
    let d = "M" + pts[0].x.toFixed(2) + "," + pts[0].y.toFixed(2);
    for (let i = 0; i < n - 1; i++) {
      const h = dx[i] / 3;
      d += "C" + (pts[i].x + h).toFixed(2) + "," + (pts[i].y + t[i] * h).toFixed(2) + " " +
        (pts[i + 1].x - h).toFixed(2) + "," + (pts[i + 1].y - t[i + 1] * h).toFixed(2) + " " +
        pts[i + 1].x.toFixed(2) + "," + pts[i + 1].y.toFixed(2);
    }
    return d;
  }

  function el(name, attrs) {
    const e = document.createElementNS(NS, name);
    for (const k in attrs) e.setAttribute(k, attrs[k]);
    return e;
  }

  function observe(container, render) {
    if (container._ro) return;
    if (typeof ResizeObserver === "undefined") {
      window.addEventListener("resize", function () { render(false); });
      container._ro = true;
      return;
    }
    let lastW = container.clientWidth;
    let raf = 0;
    container._ro = new ResizeObserver(function () {
      const w = container.clientWidth;
      if (Math.abs(w - lastW) < 2) return;
      lastW = w;
      cancelAnimationFrame(raf);
      raf = requestAnimationFrame(function () { render(false); });
    });
    container._ro.observe(container);
  }

  /* ---------------- BIỂU ĐỒ ĐƯỜNG + VÙNG ---------------- */
  function line(container, opts) {
    container._lineOpts = opts;
    renderLine(container, opts.animate !== false);
    observe(container, function () { renderLine(container, false); });
  }

  function renderLine(container, animate) {
    const opts = container._lineOpts;
    let points = (opts.points || []).slice();
    if (points.length === 1) points = [points[0], points[0]];
    container.innerHTML = "";
    container.classList.add("chart");
    const height = opts.height || 240;
    const width = Math.max(container.clientWidth, 260);
    if (!points.length) {
      container.innerHTML = '<div class="empty">Chưa có dữ liệu để hiển thị</div>';
      return;
    }

    const values = points.map(function (p) { return p.value; });
    const scale = niceScale(Math.min(0, Math.min.apply(null, values)), Math.max.apply(null, values), 4);
    const longest = scale.ticks.reduce(function (m, v) { return Math.max(m, U.formatNumber(v).length + (v < 0 ? 1 : 0)); }, 1);
    const pad = { top: opts.highlightLast ? 36 : 16, right: 18, bottom: 30, left: Math.max(44, longest * 7.2 + 14) };
    const plotW = width - pad.left - pad.right;
    const plotH = height - pad.top - pad.bottom;
    const n = points.length;
    const xAt = function (i) { return pad.left + (n === 1 ? plotW / 2 : (plotW * i) / (n - 1)); };
    const yAt = function (v) { return pad.top + plotH - ((v - scale.min) / (scale.max - scale.min)) * plotH; };

    uid++;
    const gid = "chart-grad-" + uid;
    const svg = el("svg", { viewBox: "0 0 " + width + " " + height, width: width, height: height, role: "img" });
    svg.setAttribute("aria-label", opts.ariaLabel || "Biểu đồ biến động số dư");

    const defs = el("defs", {});
    const grad = el("linearGradient", { id: gid, x1: "0", y1: "0", x2: "0", y2: "1" });
    grad.appendChild(el("stop", { offset: "0%", "stop-color": "#1E5BD8", "stop-opacity": "0.35" }));
    grad.appendChild(el("stop", { offset: "100%", "stop-color": "#1E5BD8", "stop-opacity": "0.02" }));
    defs.appendChild(grad);
    svg.appendChild(defs);

    // Lưới ngang + nhãn trục Y
    scale.ticks.forEach(function (v) {
      const y = yAt(v);
      svg.appendChild(el("line", { x1: pad.left, x2: width - pad.right, y1: y, y2: y, stroke: v === 0 ? "#DCE3EE" : "#EEF2F8", "stroke-dasharray": v === 0 ? "" : "4 4" }));
      const t = el("text", { x: pad.left - 10, y: y + 4, "text-anchor": "end", "font-size": "12", fill: "#6B7590" });
      t.textContent = (v < 0 ? "-" : "") + U.formatNumber(v);
      svg.appendChild(t);
    });

    // Nhãn trục X (tự giãn để không chồng chữ)
    const labelW = points.reduce(function (m, p) { return Math.max(m, String(p.label).length * 6.3); }, 0) + 8;
    const spacing = n > 1 ? plotW / (n - 1) : plotW;
    const every = Math.max(1, Math.ceil(labelW / spacing));
    points.forEach(function (p, i) {
      const isLast = i === n - 1;
      if (i % every !== 0 && !isLast) return;
      if (!isLast && every > 1 && (n - 1 - i) < every) return; // tránh đè nhãn cuối
      const t = el("text", { x: xAt(i), y: height - 8, "text-anchor": "middle", "font-size": "11.5", fill: "#6B7590" });
      t.textContent = p.label;
      svg.appendChild(t);
    });

    const pts = points.map(function (p, i) { return { x: xAt(i), y: yAt(p.value) }; });
    const pathD = monotonePath(pts);
    const g = el("g", {});
    if (animate) g.setAttribute("class", "chart-reveal");
    g.appendChild(el("path", { d: pathD + "L" + pts[n - 1].x + "," + (pad.top + plotH) + "L" + pts[0].x + "," + (pad.top + plotH) + "Z", fill: "url(#" + gid + ")" }));
    g.appendChild(el("path", { d: pathD, fill: "none", stroke: "#1E5BD8", "stroke-width": "2.5", "stroke-linecap": "round" }));
    const showDots = n <= 16;
    pts.forEach(function (p, i) {
      if (!showDots && i !== n - 1) return;
      g.appendChild(el("circle", { cx: p.x, cy: p.y, r: 5, fill: "#1E5BD8", stroke: "#fff", "stroke-width": "2" }));
    });
    svg.appendChild(g);

    // Đường dò + điểm active khi rê chuột
    const guide = el("line", { x1: 0, x2: 0, y1: pad.top, y2: pad.top + plotH, stroke: "#1E5BD8", "stroke-width": "1", "stroke-dasharray": "3 3", opacity: "0" });
    const dot = el("circle", { cx: 0, cy: 0, r: 6.5, fill: "#1E5BD8", stroke: "#fff", "stroke-width": "3", opacity: "0" });
    svg.appendChild(guide);
    svg.appendChild(dot);
    const hit = el("rect", { x: pad.left - 10, y: 0, width: plotW + 20, height: height, fill: "transparent" });
    svg.appendChild(hit);
    container.appendChild(svg);

    const tip = document.createElement("div");
    tip.className = "chart__tooltip";
    container.appendChild(tip);

    // Bong bóng giá trị hiện tại ở điểm cuối (giống ảnh mẫu)
    let bubble = null;
    if (opts.highlightLast) {
      bubble = document.createElement("div");
      bubble.className = "chart__bubble";
      bubble.textContent = U.formatVND(points[n - 1].value);
      bubble.style.left = (pts[n - 1].x + 14) + "px";
      bubble.style.top = pts[n - 1].y + "px";
      container.appendChild(bubble);
    }

    function show(i) {
      const p = pts[i];
      guide.setAttribute("x1", p.x); guide.setAttribute("x2", p.x); guide.setAttribute("opacity", "1");
      dot.setAttribute("cx", p.x); dot.setAttribute("cy", p.y); dot.setAttribute("opacity", "1");
      const dateText = points[i].date ? U.formatDate(points[i].date) : "";
      tip.innerHTML = "<small></small><span></span>";
      tip.querySelector("small").textContent = points[i].label + (dateText && points[i].label.indexOf("/") === -1 ? " (" + dateText + ")" : "");
      tip.querySelector("span").textContent = U.formatVND(points[i].value);
      tip.style.left = Math.min(Math.max(p.x, 70), width - 70) + "px";
      tip.style.top = p.y + "px";
      tip.classList.add("is-visible");
      if (bubble) bubble.style.opacity = "0";
    }
    function hide() {
      guide.setAttribute("opacity", "0"); dot.setAttribute("opacity", "0");
      tip.classList.remove("is-visible");
      if (bubble) bubble.style.opacity = "1";
    }
    function nearest(clientX) {
      const r = svg.getBoundingClientRect();
      const x = (clientX - r.left) * (width / r.width);
      let best = 0, bd = Infinity;
      pts.forEach(function (p, i) { const d = Math.abs(p.x - x); if (d < bd) { bd = d; best = i; } });
      return best;
    }
    hit.addEventListener("pointermove", function (e) { show(nearest(e.clientX)); });
    hit.addEventListener("pointerdown", function (e) { show(nearest(e.clientX)); });
    hit.addEventListener("pointerleave", hide);
    container.dataset.points = String(n);
  }

  /* ---------------- BIỂU ĐỒ DONUT ---------------- */
  function donut(container, opts) {
    const size = opts.size || 155;
    const th = opts.thickness || 13;
    const r = (size - th) / 2;
    const C = 2 * Math.PI * r;
    const total = opts.segments.reduce(function (s, x) { return s + Math.max(0, x.value); }, 0);
    container.innerHTML = "";
    container.classList.add("donut");
    container.style.width = size + "px";
    container.style.height = size + "px";
    const svg = el("svg", { viewBox: "0 0 " + size + " " + size, width: size, height: size, role: "img" });
    svg.setAttribute("aria-label", opts.ariaLabel || "Biểu đồ tỷ trọng");
    svg.appendChild(el("circle", { cx: size / 2, cy: size / 2, r: r, fill: "none", stroke: "#EEF2F8", "stroke-width": th }));
    const visible = opts.segments.filter(function (s) { return s.value > 0; });
    const gap = visible.length > 1 ? th + 4 : 0; // chừa chỗ cho đầu bo tròn
    let offset = 0;
    if (total > 0) {
      visible.forEach(function (s) {
        const len = (s.value / total) * C;
        const drawn = Math.max(len - gap, 0.01);
        const c = el("circle", {
          cx: size / 2, cy: size / 2, r: r, fill: "none",
          stroke: s.color, "stroke-width": th, "stroke-linecap": visible.length > 1 ? "round" : "butt",
          "stroke-dasharray": drawn + " " + (C - drawn),
          "stroke-dashoffset": -(offset + gap / 2),
          transform: "rotate(-90 " + size / 2 + " " + size / 2 + ")"
        });
        const title = el("title", {});
        title.textContent = s.label + ": " + U.formatVND(s.value) + " (" + U.formatPercent((s.value / total) * 100) + ")";
        c.appendChild(title);
        svg.appendChild(c);
        offset += len;
      });
    }
    container.appendChild(svg);
    const center = document.createElement("div");
    center.className = "donut__center";
    center.innerHTML = opts.centerHtml || "";
    container.appendChild(center);
  }

  return { line: line, donut: donut, niceScale: niceScale };
})();
