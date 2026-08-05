# Die Oberfläche als eigenes Image: gebaut mit Node, ausgeliefert von nginx.
#
# Der zweite Zweck des nginx ist wichtiger als das Ausliefern: er nimmt die
# Anfragen unter /api entgegen und reicht sie an den Backend-Container weiter.
# Dadurch spricht der Browser nur mit **einer** Adresse — es gibt keine zweite
# Herkunft, also auch kein CORS, und der Nutzer muss nirgends eintragen, wo sein
# Backend liegt.

FROM node:22-alpine AS builder

WORKDIR /app

# Abhängigkeiten zuerst: diese Schicht wird nur neu gebaut, wenn sich die
# Sperrdatei ändert.
COPY package.json package-lock.json ./
RUN npm ci

COPY . .

# Vite backt VITE_* beim Bauen ein — eine Umgebungsvariable zur Laufzeit käme zu
# spät. Deshalb hier ein **relativer** Pfad: der Browser schickt an denselben
# Host, unter dem er die Seite geholt hat, und nginx leitet weiter. Damit
# funktioniert dasselbe Image unter jeder Domain.
ENV VITE_API_URL=/api/v1
RUN npm run build


FROM nginx:1.30-alpine

COPY --from=builder /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
