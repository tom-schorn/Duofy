from fastapi import APIRouter

from app.api.v1 import (
    accounts,
    auth,
    commitments,
    households,
    imports,
    plans,
    positions,
    transactions,
)

api_router = APIRouter()

api_router.include_router(auth.router)
api_router.include_router(accounts.router, prefix="/accounts", tags=["accounts"])
api_router.include_router(households.router, prefix="/households", tags=["households"])
api_router.include_router(commitments.router, prefix="/commitments", tags=["commitments"])
api_router.include_router(imports.router, prefix="/imports", tags=["imports"])
api_router.include_router(plans.router, prefix="/plans", tags=["plans"])
# Its own prefix so that "positions" is not parsed as a year.
api_router.include_router(positions.router, prefix="/positions", tags=["positions"])
api_router.include_router(
    transactions.router, prefix="/transactions", tags=["transactions"]
)
