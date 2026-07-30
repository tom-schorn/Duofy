#!/bin/sh
set -e

# Config comes from the environment. pydantic-settings reads the same POSTGRES_*
# variables that alembic/env.py needs, so nothing writes an .env file here.
echo "Running migrations..."
alembic upgrade head

exec "$@"
