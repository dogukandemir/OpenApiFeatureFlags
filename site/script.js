// OpenApiFeatureFlags site. Two small behaviours: engine tabs and copy buttons.

document.addEventListener('DOMContentLoaded', () => {
  const tabs = Array.from(document.querySelectorAll('[role="tab"]'));
  const panels = Array.from(document.querySelectorAll('[role="tabpanel"]'));

  tabs.forEach((tab) => {
    tab.addEventListener('click', () => {
      const panelId = tab.getAttribute('aria-controls');
      tabs.forEach((t) => {
        const selected = t === tab;
        t.setAttribute('aria-selected', selected ? 'true' : 'false');
        t.tabIndex = selected ? 0 : -1;
      });
      panels.forEach((p) => { p.hidden = p.id !== panelId; });
    });
    tab.addEventListener('keydown', (event) => {
      if (event.key !== 'ArrowLeft' && event.key !== 'ArrowRight') return;
      const index = tabs.indexOf(tab);
      const next = event.key === 'ArrowRight'
        ? tabs[(index + 1) % tabs.length]
        : tabs[(index - 1 + tabs.length) % tabs.length];
      next.focus();
      next.click();
    });
  });

  document.querySelectorAll('[data-copy]').forEach((button) => {
    const label = button.textContent;
    button.addEventListener('click', async () => {
      const source = document.querySelector(button.getAttribute('data-copy'));
      if (!source) return;
      const text = source.innerText.replace(/^[$>]\s*/, '').trim();
      try {
        await navigator.clipboard.writeText(text);
        button.textContent = 'Copied';
      } catch {
        button.textContent = 'Press Ctrl+C';
      }
      setTimeout(() => { button.textContent = label; }, 1600);
    });
  });
});
