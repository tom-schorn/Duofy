# uv ships its own Python, so no separate base image juggling.
FROM ghcr.io/astral-sh/uv:python3.12-bookworm-slim AS builder

ENV UV_COMPILE_BYTECODE=1 \
    UV_LINK_MODE=copy \
    UV_PYTHON_DOWNLOADS=never

WORKDIR /app

# No `RUN --mount=type=cache` here: the Terraform Docker provider builds through the
# classic Engine builder, which does not understand BuildKit mount syntax.
#
# Dependencies first — this layer only rebuilds when the lockfile changes.
COPY pyproject.toml uv.lock ./
# --frozen, nicht --locked: beim Release hebt release-please die Version in
# pyproject.toml, in uv.lock steht sie noch alt, und --locked besteht darauf,
# dass beide übereinstimmen — jeder Release-Bau würde daran scheitern.
#
# Verloren geht dabei nichts: dass die Sperrdatei zum pyproject.toml passt,
# prüft die CI bei jedem Pull Request mit --locked. Der Image-Bau soll bauen.
RUN uv sync --frozen --no-install-project --no-dev

COPY . .
RUN uv sync --frozen --no-dev


FROM python:3.12-slim-bookworm

# Never run the app as root.
RUN useradd --create-home --uid 1000 duofy
WORKDIR /app

COPY --from=builder --chown=duofy:duofy /app /app
ENV PATH="/app/.venv/bin:$PATH"

# Runs `alembic upgrade head` before the app, so a deploy migrates itself.
COPY docker-entrypoint.sh /usr/local/bin/docker-entrypoint.sh
RUN chmod +x /usr/local/bin/docker-entrypoint.sh

USER duofy
EXPOSE 8000

ENTRYPOINT ["/usr/local/bin/docker-entrypoint.sh"]
# No --reload here: that is a development flag.
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8000"]
