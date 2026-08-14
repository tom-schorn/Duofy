# Read-only: these networks belong to the Portainer stacks, Terraform must not
# manage them. cloudflared reaches the backend over the first one, the database
# lives on the second.
data "docker_network" "cloudflared" {
  name = var.cloudflared_network
}

data "docker_network" "reverse_proxy" {
  name = var.reverse_proxy_network
}

# All containers on the host, used only to map the postgres name to its id — the
# network data source knows addresses but not names.
data "docker_containers" "all" {}

locals {
  postgres_id = one([
    for c in data.docker_containers.all.containers : c.id
    if contains([for n in c.names : trimprefix(n, "/")], var.postgres_container)
  ])

  # ipv4_address comes back in CIDR form, e.g. "172.21.0.4/16".
  postgres_ip = one([
    for c in data.docker_network.reverse_proxy.containers : split("/", c.ipv4_address)[0]
    if c.container_id == local.postgres_id
  ])
}

locals {
  # The image the pipeline published, not one built here.
  #
  # Terraform used to build it on the host from `../backend`. That was work done
  # twice: every push to `develop` already builds and pushes the same image on
  # GitHub's runners. Doing it again on a host with 2.9 GB and no swap cost time and
  # memory for nothing — and it tied this configuration to the source tree lying
  # next to it, which is why it could not live anywhere else.
  backend_image = "${var.backend_image_repository}:${var.backend_image_tag}"

  # Port uvicorn listens on inside the container. Referenced by the tunnel ingress
  # rule so the two can never drift apart.
  backend_port = 8000

  cors_origins = concat([
    "https://${cloudflare_pages_domain.frontend_dev.name}",
    "https://${local.pages_preview_host}",
  ], var.extra_cors_origins)

  # Every Pages preview gets a hostname with a build hash in front, so they cannot be
  # listed. This pattern covers all of them, derived from the assigned subdomain.
  cors_origin_regex = "^https://[a-z0-9-]+\\.${replace(cloudflare_pages_project.frontend.subdomain, ".", "\\.")}$"
}

# `dev` is a moving tag: it points at whatever `develop` last produced. The tag alone
# would therefore never tell Terraform that anything changed — `apply` would report
# "no changes" while the host quietly kept running last week's build.
#
# The digest does tell it. A new push moves the digest, `pull_triggers` fires, the
# image is pulled and the container is replaced with it.
data "docker_registry_image" "backend" {
  name = local.backend_image
}

resource "docker_image" "backend" {
  name          = data.docker_registry_image.backend.name
  pull_triggers = [data.docker_registry_image.backend.sha256_digest]

  # Drop the superseded image instead of leaving it on the host. Disk sits at around
  # 78 %, and a dev image that changes several times a week piles up quickly.
  keep_locally = false
}

resource "docker_container" "backend" {
  name    = "duofy-backend"
  image   = docker_image.backend.image_id
  restart = "unless-stopped"

  # The host has 2.9 GB and no swap, so every container needs a cap. memory_swap
  # equal to memory disables swap for this container instead of leaving it
  # unlimited, which is what Docker would otherwise do.
  memory      = var.backend_memory_mb
  memory_swap = var.backend_memory_mb

  env = [
    # This deployment is the dev environment — it serves dev-api-duofy and gets
    # whatever is on `develop`. DEBUG stays off regardless: the URL is public, and
    # debug tracebacks would leak internals to anyone who finds it.
    "ENVIRONMENT=${var.backend_environment}",
    "DEBUG=false",
    "POSTGRES_HOST=${local.postgres_ip}",
    "POSTGRES_PORT=${var.postgres_port}",
    "POSTGRES_DB=${var.postgres_db}",
    "POSTGRES_USER=${var.postgres_user}",
    "POSTGRES_PASSWORD=${var.postgres_password}",
    "JWT_SECRET=${var.jwt_secret}",
    # Both hostnames the dev frontend is reachable under: the custom domain and the
    # Pages branch alias. Read from the resources so they cannot drift.
    #
    # Note this REPLACES the default from app/core/config.py, so localhost is not
    # allowed unless it is added via var.extra_cors_origins.
    "CORS_ORIGINS=${jsonencode(local.cors_origins)}",
    "CORS_ORIGIN_REGEX=${local.cors_origin_regex}",

    # The refresh cookie has to reach both hosts: the frontend sets the session, the
    # backend reads it. They are different origins but the same *site*, because both
    # are subdomains of one domain — so a leading dot is enough and SameSite=lax
    # lets the cookie through.
    #
    # A cookie across two unrelated domains would be a third-party cookie, and
    # Safari drops those entirely. That is why the month-long session depends on
    # this line.
    #
    # The Pages preview URLs (<hash>.*.pages.dev) are a different site and get no
    # cookie. Previews therefore ask for a sign-in every time, which is acceptable.
    "COOKIE_DOMAIN=.${var.domain}",
    "COOKIE_SECURE=true",
    "COOKIE_SAMESITE=lax",
  ]

  # No published ports — cloudflared reaches it by container name.
  networks_advanced {
    name = data.docker_network.cloudflared.name
  }

  networks_advanced {
    name = data.docker_network.reverse_proxy.name
  }

  # The IP is only known at apply time. Without this check a failed lookup would
  # silently set POSTGRES_HOST to an empty string and the container would just
  # restart-loop with a confusing error.
  lifecycle {
    precondition {
      condition     = local.postgres_ip != null
      error_message = "No container named ${var.postgres_container} found on network ${var.reverse_proxy_network}."
    }
  }
}

