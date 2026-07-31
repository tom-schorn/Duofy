# The tunnel already exists — it was created in the Zero Trust dashboard and runs
# as the `cloudflared` container on the backend host. Terraform adopts it instead
# of creating a second one.
import {
  to = cloudflare_zero_trust_tunnel_cloudflared.duofy
  id = "${var.cloudflare_account_id}/${var.cloudflare_tunnel_id}"
}

# Duofy-only tunnel. Florabase does not use it — that still goes through NPM and will
# get its own tunnel in its own state. The tunnel is not bound to a host: moving Duofy
# to its own server later just means running cloudflared with the same TUNNEL_TOKEN
# there, no DNS or ingress change.
resource "cloudflare_zero_trust_tunnel_cloudflared" "duofy" {
  account_id = var.cloudflare_account_id
  name       = "Florabase-VPS-Duofy-Network"
  config_src = "cloudflare"
}

# Points dev-api.tom-schorn.de at the tunnel. Cloudflare resolves the CNAME to the
# connector, so the origin needs no open port.
resource "cloudflare_dns_record" "backend" {
  zone_id = var.cloudflare_zone_id
  name    = "${var.backend_subdomain}.${var.domain}"
  type    = "CNAME"
  content = "${var.cloudflare_tunnel_id}.cfargotunnel.com"
  proxied = true
  ttl     = 1 # required to be 1 (automatic) while proxied
  comment = "Duofy backend via cloudflared tunnel — managed by Terraform"
}

# This resource owns the tunnel's ENTIRE ingress list — anything configured in the
# dashboard is replaced. That is safe here because the tunnel serves Duofy only.
#
# The service target uses the container name, not an IP: cloudflared and the backend
# share a user-defined network, so Docker's embedded DNS resolves it on every
# connection. An IP would go stale when the container is recreated.
resource "cloudflare_zero_trust_tunnel_cloudflared_config" "duofy" {
  account_id = var.cloudflare_account_id
  tunnel_id  = cloudflare_zero_trust_tunnel_cloudflared.duofy.id

  config = {
    ingress = [
      {
        hostname = "${var.backend_subdomain}.${var.domain}"
        service  = "http://duofy-backend:8000"
      },
      # Cloudflare requires a final rule without a hostname. Without it, requests for
      # unknown hostnames have nowhere to go and the config is rejected.
      {
        service = "http_status:404"
      },
    ]
  }
}
