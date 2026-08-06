variable "cloudflare_api_token" {
  description = "Cloudflare API token with Workers Scripts, Argo Tunnel, DNS and Zone permissions"
  type        = string
  sensitive   = true
}

variable "cloudflare_account_id" {
  description = "Cloudflare account ID (dashboard sidebar)"
  type        = string
}

variable "cloudflare_zone_id" {
  description = "Zone ID of the Duofy domain (domain overview page)"
  type        = string
}

variable "domain" {
  description = "Apex domain Duofy runs on, e.g. duofy.app"
  type        = string
}

variable "cloudflare_tunnel_id" {
  description = "UUID of the existing cloudflared tunnel running on the backend host"
  type        = string
}

variable "docker_host" {
  description = "Docker daemon of the backend host, reached over SSH"
  type        = string
  # No default on purpose: this points at a real machine. It belongs in
  # terraform.tfvars, which is not in the repository.
}

variable "cloudflared_network" {
  description = "Existing Docker network shared with the cloudflared container"
  type        = string
  default     = "infrastructure_cloudflared_network"
}

variable "reverse_proxy_network" {
  description = "Existing Docker network the shared postgres lives on"
  type        = string
  default     = "reverse_proxy_stack_public"
}

variable "backend_environment" {
  description = "Value of ENVIRONMENT inside the backend container"
  type        = string
  default     = "development"
}

variable "extra_cors_origins" {
  description = "Additional allowed origins, e.g. http://localhost:5173 for running the frontend locally against this backend"
  type        = list(string)
  default     = []
}

variable "backend_memory_mb" {
  description = "Hard memory cap for the backend container in MB"
  type        = number
  default     = 256
}

variable "postgres_container" {
  description = "Name of the postgres container — its IP is read from the Docker network, not configured"
  type        = string
  default     = "postgres"
}

variable "postgres_port" {
  description = "Port of the database"
  type        = number
  default     = 5432
}

variable "postgres_db" {
  description = "Database name on the shared postgres"
  type        = string
  default     = "duofy"
}

variable "postgres_user" {
  description = "Database role for the backend"
  type        = string
  default     = "duofy"
}

variable "postgres_password" {
  description = "Password of the backend's database role"
  type        = string
  sensitive   = true
}

variable "jwt_secret" {
  description = "Signing secret for fastapi-users JWTs"
  type        = string
  sensitive   = true
}

variable "api_prefix" {
  description = "Path the API router is mounted under — must match the prefix in backend/app/main.py"
  type        = string
  default     = "/api/v1"
}

variable "github_owner" {
  description = "GitHub account the repo lives under"
  type        = string
  default     = "tom-schorn"
}

variable "github_repo" {
  description = "Repository name Pages builds from"
  type        = string
  default     = "Duofy"
}

variable "production_branch" {
  description = "Git branch Pages deploys to production"
  type        = string
  default     = "main"
}

variable "preview_branch" {
  description = "Git branch Pages deploys as the dev environment — also forms the branch alias hostname"
  type        = string
  default     = "develop"
}

variable "pages_project" {
  description = "Name of the Cloudflare Pages project hosting the frontend"
  type        = string
  default     = "duofy"
}

variable "frontend_subdomain" {
  description = "Subdomain label served by the Cloudflare Worker, without the domain"
  type        = string
}

variable "backend_subdomain" {
  description = "Subdomain label routed through the cloudflared tunnel to the vServer, without the domain"
  type        = string
}
