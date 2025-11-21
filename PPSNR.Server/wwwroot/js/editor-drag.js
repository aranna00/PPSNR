// Minimal placeholder to prevent 404 for editor drag script.
// Replace with actual implementation as needed.
(function(){
  const ready = () => {
    // no-op: hook for future drag logic
    console.debug('[editor-drag] loaded');
  };
  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', ready);
  } else {
    ready();
  }
})();
