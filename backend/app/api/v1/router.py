from fastapi import APIRouter

from app.api.v1 import auth

api_router = APIRouter()

api_router.include_router(auth.router)

# Neue Endpunkte hier registrieren:
# from app.api.v1 import households
# api_router.include_router(households.router, prefix="/households", tags=["households"])
