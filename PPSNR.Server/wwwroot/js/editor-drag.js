// Drag + Resize helper for the layout editor
// Exposes: window.PPSNR_Drag.init(apiBase, pairId)
(function(){
  const PPSNR_Drag = {
    init(apiBase, pairId){
      try {
        const canvas = document.getElementById('canvas');
        if (!canvas) return;
        if (canvas.__pps_init) return; // prevent double init

        // Derive pairId and apiBase if not provided (handles auto-init case)
        const pathSegs = (window.location.pathname || '')
          .split('/')
          .filter(Boolean);
        // Route patterns:
        // /{pairId}/edit/{token}
        // /{pairId}/partner-edit/{token}
        const urlPairId = pathSegs.length >= 3 ? pathSegs[0] : null;
        const urlMode = pathSegs.length >= 3 ? pathSegs[1] : null; // 'edit' or 'partner-edit'
        const urlToken = pathSegs.length >= 3 ? pathSegs[2] : null;

        const pair = pairId || canvas.getAttribute('data-pair-id') || urlPairId || undefined;

        let base = apiBase;
        if (!base) {
          if (urlMode === 'partner-edit' && urlToken) {
            base = `/api/partner/${urlToken}`;
          } else {
            base = '/api';
          }
        }

        // mark initialized only after we have derived the values
        canvas.__pps_init = true;

        const elems = Array.from(canvas.querySelectorAll('.drag'));
        elems.forEach(el => attachDragAndResize(el, base, pair));
        // Observe for any dynamic changes (if future updates re-render)
        const mo = new MutationObserver(muts => {
          for (const m of muts) {
            if (m.type === 'childList') {
              m.addedNodes.forEach(n => {
                if (n.nodeType === 1 && n.classList && n.classList.contains('drag')) {
                  attachDragAndResize(n, base, pair);
                } else if (n.nodeType === 1) {
                  n.querySelectorAll && n.querySelectorAll('.drag').forEach(d => attachDragAndResize(d, base, pair));
                }
              });
            }
          }
        });
        mo.observe(canvas, { childList: true, subtree: true });
      } catch (e) {
        console.error('[editor-drag] init failed', e);
      }
    }
  };

  function clamp(v, min, max){ return Math.max(min, Math.min(max, v)); }

  function getBox(el){
    const left = parseFloat(el.style.left || '0') || 0;
    const top = parseFloat(el.style.top || '0') || 0;
    const width = el.offsetWidth || parseFloat(el.style.width || '0') || 0;
    const height = el.offsetHeight || parseFloat(el.style.height || '0') || 0;
    return { left, top, width, height };
  }

  function bringToFront(el){
    try {
      // Optional: bump z-index locally for UX; persistence handled server-side if needed
      const parent = el.parentElement;
      if (!parent) return;
      let maxZ = 0;
      parent.querySelectorAll('.drag').forEach(d => {
        const z = parseInt(d.style.zIndex || d.getAttribute('data-z-index') || '0', 10) || 0;
        if (z > maxZ) maxZ = z;
      });
      const z = maxZ + 1;
      el.style.zIndex = String(z);
      el.setAttribute('data-z-index', String(z));
    } catch {}
  }

  function attachDragAndResize(el, apiBase, pairId){
    if (el.__pps_attached) return;
    el.__pps_attached = true;
    // Ensure the element itself doesn't trigger native panning/zooming
    el.style.touchAction = 'none';
    // Prevent accidental text/image selection while dragging
    el.style.userSelect = 'none';

    const canvas = el.parentElement; // #canvas
    let pointerId = null;
    let start = null;
    let mode = null; // 'drag' or direction: 'n','ne','e','se','s','sw','w','nw'
    let startBox = null;

    function onPointerDown(ev){
      // Only respond to primary button / touch
      if (typeof ev.button === 'number' && ev.button !== 0) return;
      if (ev.isPrimary === false) return;
      if (pointerId !== null) return;
      const target = ev.target;
      const isResizer = target && target.classList && target.classList.contains('resizer');
      mode = isResizer ? (target.getAttribute('data-dir') || 'drag') : 'drag';
      pointerId = ev.pointerId || 'mouse';
      start = { x: ev.clientX, y: ev.clientY };
      startBox = getBox(el);
      bringToFront(el);
      try { el.setPointerCapture && el.setPointerCapture(ev.pointerId); } catch {}
      // Prevent native drag image and text selection
      ev.preventDefault();
      ev.stopPropagation();
      // Fallback listeners on window in case pointer capture is not honored on this platform
      window.addEventListener('pointermove', onPointerMove, { passive: false });
      window.addEventListener('pointerup', onPointerUp, { passive: false, once: true });
      window.addEventListener('pointercancel', onPointerCancel, { passive: false, once: true });
    }

    function onPointerMove(ev){
      if (pointerId === null) return;
      // Only track move events for the active pointer
      if (ev.pointerId && ev.pointerId !== pointerId) return;
      const dx = ev.clientX - start.x;
      const dy = ev.clientY - start.y;
      const parentRect = canvas.getBoundingClientRect();

      if (mode === 'drag') {
        let newLeft = startBox.left + dx;
        let newTop = startBox.top + dy;
        // constrain within canvas
        newLeft = clamp(newLeft, 0, Math.max(0, parentRect.width - el.offsetWidth));
        newTop = clamp(newTop, 0, Math.max(0, parentRect.height - el.offsetHeight));
        el.style.left = `${newLeft}px`;
        el.style.top = `${newTop}px`;
      } else {
        // resize
        let left = startBox.left;
        let top = startBox.top;
        let width = startBox.width;
        let height = startBox.height;

        if (mode.indexOf('e') !== -1) width = Math.max(16, startBox.width + dx);
        if (mode.indexOf('s') !== -1) height = Math.max(16, startBox.height + dy);
        if (mode.indexOf('w') !== -1) {
          const nl = startBox.left + dx;
          const maxLeft = startBox.left + startBox.width - 16;
          left = clamp(nl, 0, maxLeft);
          width = startBox.width + (startBox.left - left);
        }
        if (mode.indexOf('n') !== -1) {
          const nt = startBox.top + dy;
          const maxTop = startBox.top + startBox.height - 16;
          top = clamp(nt, 0, maxTop);
          height = startBox.height + (startBox.top - top);
        }

        // constrain within canvas
        const maxW = parentRect.width - left;
        const maxH = parentRect.height - top;
        width = clamp(width, 16, Math.max(16, maxW));
        height = clamp(height, 16, Math.max(16, maxH));

        el.style.left = `${left}px`;
        el.style.top = `${top}px`;
        el.style.width = `${width}px`;
        el.style.height = `${height}px`;
      }
      ev.preventDefault();
      ev.stopPropagation();
    }

    async function onPointerUp(ev){
      if (pointerId === null) return;
      try {
        await save(el, apiBase, pairId);
      } catch (e) {
        console.warn('[editor-drag] save failed', e);
      }
      try {
        el.releasePointerCapture && el.releasePointerCapture(ev.pointerId);
      } catch {}
      pointerId = null;
      mode = null;
      start = null;
      startBox = null;
      ev.stopPropagation();
      // Remove fallback listeners if any
      window.removeEventListener('pointermove', onPointerMove);
      window.removeEventListener('pointerup', onPointerUp);
      window.removeEventListener('pointercancel', onPointerCancel);
    }

    function onPointerCancel(ev){
      // Treat as pointer up without saving (no change guaranteed)
      try { el.releasePointerCapture && el.releasePointerCapture(ev.pointerId); } catch {}
      pointerId = null;
      mode = null;
      start = null;
      startBox = null;
      window.removeEventListener('pointermove', onPointerMove);
      window.removeEventListener('pointerup', onPointerUp);
      window.removeEventListener('pointercancel', onPointerCancel);
      ev.stopPropagation();
    }

    el.addEventListener('pointerdown', onPointerDown, { passive: false });
    // Listen on the element itself since it holds the pointer capture
    el.addEventListener('pointermove', onPointerMove, { passive: false });
    el.addEventListener('pointerup', onPointerUp, { passive: false });
    el.addEventListener('lostpointercapture', onPointerCancel);
  }

  async function save(el, apiBase, pairId){
    const slotId = el.getAttribute('data-slot-id');
    const layoutId = el.getAttribute('data-layout-id');
    const z = parseInt(el.getAttribute('data-z-index') || el.style.zIndex || '0', 10) || 0;
    const vis = (el.getAttribute('data-visible') || 'true') === 'true';
    const img = el.getAttribute('data-image-url');
    // Ensure SlotType is posted as a number to satisfy System.Text.Json EnumConverter (no JsonStringEnumConverter configured)
    const slotTypeAttr = el.getAttribute('data-slot-type');
    let slotType;
    if (slotTypeAttr == null) {
      slotType = 0; // default to 0 (Pokemon) if missing
    } else {
      const n = Number(slotTypeAttr);
      if (!Number.isNaN(n)) {
        slotType = n;
      } else {
        const name = String(slotTypeAttr).toLowerCase();
        // Known mappings
        if (name === 'pokemon') slotType = 0;
        else if (name === 'badge') slotType = 1;
        else slotType = 0; // sensible default
      }
    }
    const index = parseInt(el.getAttribute('data-index') || '0', 10) || 0;
    const x = Math.round(parseFloat(el.style.left || '0') || 0);
    const y = Math.round(parseFloat(el.style.top || '0') || 0);
    const w = Math.round(el.offsetWidth || parseFloat(el.style.width || '0') || 0);
    const h = Math.round(el.offsetHeight || parseFloat(el.style.height || '0') || 0);

    // build AdditionalProperties JSON with size
    const additional = JSON.stringify({ w, h });

    const url = `${apiBase}/pairs/${pairId}/layouts/${layoutId}/slots/${slotId}`;
    const payload = JSON.stringify({
      id: slotId,
      layoutId: layoutId,
      x, y,
      zIndex: z,
      visible: vis,
      imageUrl: img,
      slotType: slotType,
      index: index,
      additionalProperties: additional
    });

    // Use antiforgery helper
    if (!window.ppsnr || !window.ppsnr.postWithAntiforgery){
      throw new Error('antiforgery helper missing');
    }
    await window.ppsnr.postWithAntiforgery(url, 'POST', payload);
  }

  window.PPSNR_Drag = PPSNR_Drag;

  // auto-ready logs
  const ready = () => {
    console.log('[editor-drag] loaded');
    // Global safeguard against native image drag within the editor canvas
    const canvas = document.getElementById('canvas');
    if (canvas) {
      canvas.addEventListener('dragstart', e => e.preventDefault(), { passive: false });
      // Auto-initialize as a fallback in case Blazor page didn't invoke init
      try {
        if (!canvas.__pps_init) {
          // slight delay to allow Blazor to render children
          setTimeout(() => {
            try { window.PPSNR_Drag && window.PPSNR_Drag.init(); } catch (e) { console.warn('[editor-drag] auto-init failed', e); }
          }, 0);
        }
      } catch {}
    }
  };
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ready);
  } else {
    ready();
  }
})();
