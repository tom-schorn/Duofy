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
  backend_dir = "${path.module}/../backend"

  # Local tag only — this image is built on the host and never pushed. A registry
  # prefix here would turn it into a remote reference.
  backend_image = "duofy-backend:${var.preview_branch}"

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

  # Only files that actually end up in the image. Caches and tests are excluded by
  # backend/.dockerignore anyway, but they must not trigger a rebuild either.
  backend_files = sort(concat(
    tolist(fileset(local.backend_dir, "app/**/*.py")),
    tolist(fileset(local.backend_dir, "alembic/**/*.py")),
    ["pyproject.toml", "uv.lock", "Dockerfile", "alembic.ini", "docker-entrypoint.sh"],
  ))

  backend_source_hash = sha1(join("", [
    for f in local.backend_files : filesha1("${local.backend_dir}/${f}")
  ]))
}

# The build does NOT go through `docker_image`'s build block. That path is broken on
# this host: the daemon uses the containerd image store (driver-type
# io.containerd.snapshotter.v1), which the provider does not support for builds — it
# fails silently and then falls back to pulling, which dies with "denied".
#
# The Docker CLI handles it fine, so the build runs through it. Everything else stays
# a real Terraform resource.
resource "terraform_data" "backend_build" {
  triggers_replace = local.backend_source_hash

  provisioner "local-exec" {
    command = "docker -H ${var.docker_host} build -t ${local.backend_image} ${local.backend_dir}"
  }
}

# Reads the image the build above produced, so the container tracks its id.
data "docker_image" "backend" {
  name       = local.backend_image
  depends_on = [terraform_data.backend_build]
}

resource "docker_container" "backend" {
  name    = "duofy-backend"
  image   = data.docker_image.backend.id
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

