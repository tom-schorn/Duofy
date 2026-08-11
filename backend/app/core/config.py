from typing import Literal

from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env.dev", extra="ignore")

    environment: str = "development"
    debug: bool = True

    # Frontend and backend live on separate domains — keep CORS explicit.
    cors_origins: list[str] = ["http://localhost:5173"]

    # For preview deployments: their hostnames contain a build hash and cannot be
    # listed. One pattern covers all of them.
    cors_origin_regex: str | None = None

    postgres_host: str
    postgres_port: int = 5432
    postgres_db: str
    postgres_user: str
    postgres_password: str

    jwt_secret: str

    #: Fifteen minutes. Deliberately short, because this token is not revocable:
    #: once signed it stays valid until it expires, whatever happens server side.
    #:
    #: It never touches localStorage — the frontend keeps it in memory and gets a
    #: new one from the refresh token. A stolen access token is therefore worth
    #: fifteen minutes, and only while the tab that leaked it is open.
    jwt_lifetime_seconds: int = 900

    #: Thirty days of **inactivity**. Every refresh moves the expiry along, so
    #: somebody who opens Duofy regularly never signs in again; an abandoned
    #: session dies after a month.
    #:
    #: Unlike the access token this one **is** revocable: it is a row in
    #: `refresh_tokens`, and deleting the row ends the session at once.
    refresh_lifetime_seconds: int = 60 * 60 * 24 * 30

    # --- Cookie carrying the refresh token ------------------------------------
    #
    # It has to be a cookie rather than localStorage: iOS deletes script-writable
    # storage after seven days without interaction, which would defeat the point of
    # a month-long session. A cookie the server sets is not subject to that.

    #: Which hosts the cookie is sent to.
    #:
    #: Empty means the exact host that set it — right for self-hosting, where the
    #: frontend proxies `/api` and everything shares one origin, and for local
    #: development.
    #:
    #: Set to `.example.com` when frontend and backend sit on different subdomains
    #: of one domain. That is still the same *site*, so `SameSite=lax` lets the
    #: cookie through and Safari treats it as first-party — which a cookie across
    #: two different domains would not be.
    cookie_domain: str | None = None

    #: HTTPS only. Off just for local development, because browsers drop secure
    #: cookies on `http://localhost`.
    cookie_secure: bool = True

    #: `lax` is enough: the refresh endpoint is only ever called by our own
    #: frontend, and subdomains of one domain count as the same site. `none` would
    #: require third-party cookies, which Safari blocks outright.
    cookie_samesite: Literal["lax", "strict", "none"] = "lax"

    #: Name of the cookie, prefixed so it is recognisable among cookies from other
    #: sites.
    cookie_name: str = "duofy_refresh"

    @property
    def database_url(self) -> str:
        return (
            f"postgresql+asyncpg://{self.postgres_user}:{self.postgres_password}"
            f"@{self.postgres_host}:{self.postgres_port}/{self.postgres_db}"
        )


settings = Settings()
