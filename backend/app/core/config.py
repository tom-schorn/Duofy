from pydantic_settings import BaseSettings, SettingsConfigDict


class Settings(BaseSettings):
    model_config = SettingsConfigDict(env_file=".env.dev", extra="ignore")

    environment: str = "development"
    debug: bool = True

    # Frontend und Backend laufen auf getrennten Domains — CORS explizit halten.
    cors_origins: list[str] = ["http://localhost:5173"]

    # Für Pages-Previews: deren Hostnamen enthalten einen Build-Hash und sind darum
    # nicht aufzählbar. Ein Muster deckt alle ab.
    cors_origin_regex: str | None = None

    postgres_host: str
    postgres_port: int = 5432
    postgres_db: str
    postgres_user: str
    postgres_password: str

    jwt_secret: str
    #: 5 Stunden. Eine Stunde war zu kurz — man flog mitten aus der Arbeit,
    #: weil jede 401 den Token aus dem localStorage räumt.
    #:
    #: Das ist ein Kompromiss, keine Lösung: ein gestohlener Token gilt jetzt
    #: fünf Stunden statt einer. Der saubere Weg sind Refresh-Tokens mit
    #: kurzlebigem Zugriffstoken im Speicher statt im localStorage — Issue #1.
    jwt_lifetime_seconds: int = 18000

    @property
    def database_url(self) -> str:
        return (
            f"postgresql+asyncpg://{self.postgres_user}:{self.postgres_password}"
            f"@{self.postgres_host}:{self.postgres_port}/{self.postgres_db}"
        )


settings = Settings()
