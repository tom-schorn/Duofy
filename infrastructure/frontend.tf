# Pages instead of Workers: only Pages supports branch aliases, i.e. a custom domain
# that permanently serves the latest build of one branch. On Workers a custom domain
# always serves production, previews stay on *.workers.dev.
#
# Requires the GitHub account to be connected to Cloudflare — it already is.
resource "cloudflare_pages_project" "frontend" {
  account_id        = var.cloudflare_account_id
  name              = var.pages_project
  production_branch = "main"

  source = {
    type = "github"
    config = {
      owner                          = "tom-schorn"
      repo_name                      = "Duofy"
      production_branch              = "main"
      production_deployments_enabled = true
      preview_deployment_setting     = "custom"
      preview_branch_includes        = ["develop"]
    }
  }

  build_config = {
    root_dir        = "frontend"
    build_command   = "npm run build"
    destination_dir = "dist"
    build_caching   = true
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
  content = "develop.${cloudflare_pages_project.frontend.name}.pages.dev"
  proxied = true
  ttl     = 1
  comment = "Duofy frontend, develop branch — managed by Terraform"

  depends_on = [cloudflare_pages_domain.frontend_dev]
}

# TODO: production hostname is not decided yet. It needs the same pair — a
# cloudflare_pages_domain plus a proxied CNAME, but pointing at the bare
# <project>.pages.dev.
