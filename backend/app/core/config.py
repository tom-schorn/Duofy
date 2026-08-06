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
    #: Five hours. One hour was too short — a session died mid-work, because every
    #: 401 clears the token from localStorage.
    #:
    #: This is a compromise, not a fix: a stolen token now lasts five hours instead
    #: of one. The proper answer is refresh tokens with a short-lived access token
    #: held in memory rather than in localStorage — see issue #1.
    jwt_lifetime_seconds: int = 18000

    @property
    def database_url(self) -> str:
        return (
            f"postgresql+asyncpg://{self.postgres_user}:{self.postgres_password}"
            f"@{self.postgres_host}:{self.postgres_port}/{self.postgres_db}"
        )


settings = Settings()
