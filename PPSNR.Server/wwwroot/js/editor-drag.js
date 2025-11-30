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
    },
    // Clear inline geometry so Blazor-rendered placement styles take effect
    clearInlineGeometry(){
      try{
        const canvas = document.getElementById('canvas');
        if (!canvas) return;
        const elems = Array.from(canvas.querySelectorAll('.drag'));
        elems.forEach(el => {
          el.style.left = '';
          el.style.top = '';
          el.style.width = '';
          el.style.height = '';
          // Keep z-index attribute as data for saves; drop inline to let server-provided style win
          el.style.zIndex = '';
        });
      }catch(e){ console.warn('[editor-drag] clearInlineGeometry failed', e); }
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
    let aspect = 1; // width/height

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
      aspect = (startBox.height > 0) ? (startBox.width / startBox.height) : 1;
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
        // resize with aspect ratio preserved (smallest edge)
        // First, compute raw width/height based on handle deltas
        let rawLeft = startBox.left;
        let rawTop = startBox.top;
        let rawWidth = startBox.width;
        let rawHeight = startBox.height;

        if (mode.indexOf('e') !== -1) rawWidth = Math.max(16, startBox.width + dx);
        if (mode.indexOf('s') !== -1) rawHeight = Math.max(16, startBox.height + dy);
        if (mode.indexOf('w') !== -1) {
          const nl = startBox.left + dx;
          const maxLeft = startBox.left + startBox.width - 16;
          rawLeft = clamp(nl, 0, maxLeft);
          rawWidth = startBox.width + (startBox.left - rawLeft);
        }
        if (mode.indexOf('n') !== -1) {
          const nt = startBox.top + dy;
          const maxTop = startBox.top + startBox.height - 16;
          rawTop = clamp(nt, 0, maxTop);
          rawHeight = startBox.height + (startBox.top - rawTop);
        }

        // Compute scale factor using smallest edge
        const sW = rawWidth / Math.max(1, startBox.width);
        const sH = rawHeight / Math.max(1, startBox.height);
        let s = Math.min(sW, sH);
        s = Math.max(s, 16 / Math.max(1, Math.min(startBox.width, startBox.height))); // enforce min size

        // Apply scaled size maintaining original aspect
        let width = Math.max(16, startBox.width * s);
        let height = Math.max(16, startBox.height * s);

        // Position based on handle (anchor opposite side)
        let left = startBox.left;
        let top = startBox.top;
        const dxW = startBox.width - width;
        const dyH = startBox.height - height;
        switch (mode) {
          case 'se':
            // anchor top-left
            left = startBox.left;
            top = startBox.top;
            break;
          case 'e':
            // anchor left center
            left = startBox.left;
            top = startBox.top + dyH / 2;
            break;
          case 's':
            // anchor top center
            left = startBox.left + dxW / 2;
            top = startBox.top;
            break;
          case 'ne':
            // anchor bottom-left
            left = startBox.left;
            top = startBox.top + dyH;
            break;
          case 'n':
            // anchor bottom center
            left = startBox.left + dxW / 2;
            top = startBox.top + dyH;
            break;
          case 'w':
            // anchor right center
            left = startBox.left + dxW;
            top = startBox.top + dyH / 2;
            break;
          case 'sw':
            // anchor top-right
            left = startBox.left + dxW;
            top = startBox.top;
            break;
          case 'nw':
          default:
            // anchor bottom-right
            left = startBox.left + dxW;
            top = startBox.top + dyH;
            break;
        }

        // Constrain within canvas while keeping aspect
        const maxW = parentRect.width - left;
        const maxH = parentRect.height - top;
        const sCanvas = Math.min(maxW / Math.max(1, startBox.width), maxH / Math.max(1, startBox.height));
        if (s > sCanvas) {
          s = sCanvas;
          width = Math.max(16, startBox.width * s);
          height = Math.max(16, startBox.height * s);
          // recompute left/top for the chosen handle
          const dxW2 = startBox.width - width;
          const dyH2 = startBox.height - height;
          switch (mode) {
            case 'se': left = startBox.left; top = startBox.top; break;
            case 'e': left = startBox.left; top = startBox.top + dyH2 / 2; break;
            case 's': left = startBox.left + dxW2 / 2; top = startBox.top; break;
            case 'ne': left = startBox.left; top = startBox.top + dyH2; break;
            case 'n': left = startBox.left + dxW2 / 2; top = startBox.top + dyH2; break;
            case 'w': left = startBox.left + dxW2; top = startBox.top + dyH2 / 2; break;
            case 'sw': left = startBox.left + dxW2; top = startBox.top; break;
            case 'nw': default: left = startBox.left + dxW2; top = startBox.top + dyH2; break;
          }
        }

        el.style.left = `${left}px`;
        el.style.top = `${top}px`;
        el.style.width = `${Math.round(width)}px`;
        el.style.height = `${Math.round(height)}px`;
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
