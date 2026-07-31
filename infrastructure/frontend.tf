locals {
  # The branch alias hostname. Derived from the branch name and the subdomain
  # Cloudflare actually assigned, so renaming the branch moves everything that
  # references it: the CNAME target and the backend's CORS list.
  pages_preview_host = "${var.preview_branch}.${cloudflare_pages_project.frontend.subdomain}"

  # What the frontend puts in VITE_API_URL. The path is part of it — see
  # frontend/src/lib/api.ts, whose fallback is http://localhost:8000/api/v1.
  api_base_url = "https://${cloudflare_dns_record.backend.name}${var.api_prefix}"
}

# Pages instead of Workers: only Pages supports branch aliases, i.e. a custom domain
# that permanently serves the latest build of one branch. On Workers a custom domain
# always serves production, previews stay on *.workers.dev.
#
# Requires the GitHub account to be connected to Cloudflare — it already is.
resource "cloudflare_pages_project" "frontend" {
  account_id        = var.cloudflare_account_id
  name              = var.pages_project
  production_branch = var.production_branch

  source = {
    type = "github"
    config = {
      owner                          = var.github_owner
      repo_name                      = var.github_repo
      production_branch              = var.production_branch
      production_deployments_enabled = true
      preview_deployment_setting     = "custom"
      preview_branch_includes        = [var.preview_branch]
    }
  }

  build_config = {
    root_dir        = "frontend"
    build_command   = "npm run build"
    destination_dir = "dist"
    build_caching   = true
  }

  # The backend address the frontend talks to — derived from the DNS record, so it can
  # never point at a hostname that does not exist. Nothing to type in the dashboard.
  #
  # plain_text, not secret_text: Vite bakes VITE_* into the bundle, so this is public
  # by construction. Real secrets must never go here.
  deployment_configs = {
    preview = {
      env_vars = {
        VITE_API_URL = {
          type  = "plain_text"
          value = local.api_base_url
        }
      }
    }

    # Must be present even though it is empty: leaving it out makes the provider fail
    # with "Received unknown value, however the target type cannot handle unknown
    # values" on PagesProjectDeploymentConfigsProductionModel.
    #
    # TODO: no prod backend hostname exists yet. Deliberately NOT pointing production
    # at the dev API — that would have the production frontend talk to the dev
    # database without anyone noticing.
    production = {
      env_vars = {}
    }
  }
}

# Registers the dev hostname on the project. Without this the CNAME below resolves
# but Pages does not know the domain and answers with an error.
resource "cloudflare_pages_domain" "frontend_dev" {
  account_id   = var.cloudflare_account_id
  project_name = cloudflare_pages_project.frontend.name
  name         = "${var.frontend_subdomain}.${var.domain}"
}

# The branch alias: pointing at develop.<project>.pages.dev instead of the bare
# <project>.pages.dev is what pins this domain to the develop branch. Must stay
# proxied — an unproxied record silently falls back to production.
resource "cloudflare_dns_record" "frontend_dev" {
  zone_id = var.cloudflare_zone_id
  name    = "${var.frontend_subdomain}.${var.domain}"
  type    = "CNAME"
  # Must use the `subdomain` attribute, never "<name>.pages.dev". Pages subdomains are
  # globally unique, so Cloudflare suffixed this project (duofy-5mw.pages.dev) because
  # `duofy` was already taken by a stranger. Building the name by hand pointed the
  # CNAME at their project and Cloudflare answered with 1014, CNAME Cross-User Banned.
  content = local.pages_preview_host
  proxied = true
  ttl     = 1
  comment = "Duofy frontend, develop branch — managed by Terraform"

  depends_on = [cloudflare_pages_domain.frontend_dev]
}

# TODO: production hostname is not decided yet. It needs the same pair — a
# cloudflare_pages_domain plus a proxied CNAME, but pointing at the bare
# <project>.pages.dev.
