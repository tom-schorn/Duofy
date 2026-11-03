// Hell und dunkel: setzt die Klasse "dark" an html (theme.css und Blueprint lesen sie) und meldet Änderungen am Gerät.
// Wird nur von Layout/ThemeVariables aufgerufen.
export function apply(dark) {
    document.documentElement.classList.toggle("dark", dark);
}

export function watch(listener) {
    const query = window.matchMedia("(prefers-color-scheme: dark)");
    const changed = event => listener.invokeMethodAsync("DeviceDarkChanged", event.matches);
    query.addEventListener("change", changed);
    listener.invokeMethodAsync("DeviceDarkChanged", query.matches);
    return {
        dispose: () => query.removeEventListener("change", changed),
    };
}
