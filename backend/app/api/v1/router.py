from fastapi import APIRouter

from app.api.v1 import auth, commitments, households, plans, positions

api_router = APIRouter()

api_router.include_router(auth.router)
api_router.include_router(households.router, prefix="/households", tags=["households"])
api_router.include_router(commitments.router, prefix="/commitments", tags=["commitments"])
api_router.include_router(plans.router, prefix="/plans", tags=["plans"])
# Eigener Präfix, damit „positions" nicht als Jahreszahl gelesen wird.
api_router.include_router(positions.router, prefix="/positions", tags=["positions"])
