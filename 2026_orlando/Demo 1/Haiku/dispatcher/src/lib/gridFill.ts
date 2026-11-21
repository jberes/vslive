/**
 * Grid Lite sizes its body from a shadow-DOM child (`igc-virtual-scroll`) that
 * otherwise stays at ~150px regardless of the host height. Making the host a flex
 * column and letting that child grow fills the host. This reaches into the
 * component's shadow root, so it runs after upgrade and again on the next frames.
 */
export function fillGrid(host: HTMLElement | null) {
  if (!host) return;
  const apply = () => {
    host.style.display = 'flex';
    host.style.flexDirection = 'column';
    const body = host.shadowRoot?.querySelector<HTMLElement>('igc-virtual-scroll');
    if (body) {
      body.style.flex = '1 1 auto';
      body.style.minHeight = '0';
    }
  };
  apply();
  requestAnimationFrame(apply);
  window.setTimeout(apply, 300);
}
