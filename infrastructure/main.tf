terraform {
  required_version = ">= 1.9"

  required_providers {
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 5.22"
    }
    docker = {
      source  = "kreuzwerker/docker"
      version = "~> 4.5"
    }
  }

  # TODO: state is local for now. Move to a remote backend (Cloudflare R2 via
  # the S3 backend) before anyone else works on this infra.
}

provider "cloudflare" {
  api_token = var.cloudflare_api_token
}

# Talks to the remote daemon over SSH — no port is opened on the host.
provider "docker" {
  host = var.docker_host
}
