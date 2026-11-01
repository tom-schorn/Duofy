// Lädt das Auswertungsskript von Cloudflare erst, wenn die Person zugestimmt hat (Issue #35, #43).
// Wird nur von ConsentBanner aufgerufen, nie beim Laden der Seite.
const beaconUrl = "https://static.cloudflareinsights.com/beacon.min.js";

export function loadBeacon(token) {
    if (typeof token !== "string" || token.trim().length === 0) {
        return;
    }
    if (document.querySelector("script[data-duofy-beacon]")) {
        return;
    }
    const script = document.createElement("script");
    script.defer = true;
    script.src = beaconUrl;
    script.dataset.duofyBeacon = "1";
    // Cloudflare liest den Token als JSON im Attribut data-cf-beacon; JSON.stringify schützt vor Anführungszeichen.
    script.setAttribute("data-cf-beacon", JSON.stringify({ token: token.trim() }));
    document.body.appendChild(script);
}

// Lädt den Chat von Crisp erst, wenn die Person zugestimmt hat (Issue #36). Das ist das "autoload: false, dann load()"
// des Crisp-SDK ohne dessen Paket: Das Skript von Crisp (l.js) steht vorher nirgends in der Seite und wird hier eingefügt.
// An Crisp geht nur die Website-ID. Es werden keine Nutzerdaten gesetzt (keine E-Mail, kein Name, keine Kennung, keine
// Sitzungsverknüpfung); der Chat bleibt ein anonymer Besucher.
const chatUrl = "https://client.crisp.chat/l.js";
const websiteIdPattern = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

// Seiten, deren Adresse ein Geheimnis enthält (Links aus Mails mit Token). Crisp sieht die Adresse der Seite, auf der der
// Chat steht; dort lädt der Chat nicht.
const sensitivePaths = ["/bestaetigen", "/email-bestaetigen", "/passwort-neu", "/einladung/"];

function onSensitivePage() {
    const path = window.location.pathname.toLowerCase();
    return sensitivePaths.some((p) => path.startsWith(p));
}

export function loadChat(websiteId) {
    if (typeof websiteId !== "string" || !websiteIdPattern.test(websiteId.trim())) {
        console.error("Crisp: die Website-ID hat nicht die erwartete Form, der Chat wird nicht geladen.");
        return;
    }
    if (onSensitivePage()) {
        return;
    }
    if (document.querySelector("script[data-duofy-chat]")) {
        return;
    }
    window.$crisp = window.$crisp || [];
    window.CRISP_WEBSITE_ID = websiteId.trim();
    const script = document.createElement("script");
    script.async = true;
    script.src = chatUrl;
    script.dataset.duofyChat = "1";
    document.head.appendChild(script);
}

// Beendet die Chat-Sitzung beim Widerruf: Crisp wird gebeten, die Sitzung zurückzusetzen (session:reset), und die Cookies
// des Chats (crisp-client/*) werden hier selbst gelöscht, denn die Seite lädt gleich neu und Crisp kommt vielleicht nicht
// mehr dazu. Crisp setzt sie standardmäßig auf der Hauptdomain; darum werden Pfad / mit allen Domain-Stufen versucht.
export function resetChat() {
    if (window.$crisp && typeof window.$crisp.push === "function") {
        window.$crisp.push(["do", "session:reset"]);
    }
    const host = window.location.hostname.split(".");
    const domains = [null];
    for (let i = 0; i < host.length - 1; i++) {
        domains.push(host.slice(i).join("."));
    }
    for (const entry of document.cookie.split(";")) {
        const name = entry.split("=")[0].trim();
        if (!name.startsWith("crisp-client")) {
            continue;
        }
        for (const domain of domains) {
            document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/` + (domain ? `; domain=${domain}` : "");
        }
    }
}
