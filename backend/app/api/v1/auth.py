"""Signing in, staying signed in, signing out.

fastapi-users brings registration, password hashing and password reset, and it
issues the short access token. What it does not have — in any version — is refresh
tokens. That half lives here and in `app/services/refresh_tokens.py`.

## Why our own login route

fastapi-users' login route answers with the access token and nothing else. We need
it to also start a session and set a cookie. Rather than fight its router, this
module defines `/auth/login` and leaves the original in place under `/auth/jwt` for
the interactive docs and for anything that only wants a bearer token.

## Why the refresh token is a cookie

So that JavaScript cannot read it. An injected script can then act as the user
while the tab is open, but it cannot walk away with a credential that is good for a
month. As a side effect the cookie also survives on iOS, where script-writable
storage is deleted after seven days without interaction.
"""

from typing import Annotated

from fastapi import APIRouter, Cookie, Depends, HTTPException, Request, Response, status
from fastapi.security import OAuth2PasswordRequestForm
from fastapi_users.exceptions import InvalidPasswordException, UserAlreadyExists
from sqlalchemy.ext.asyncio import AsyncSession

from app.core.auth import (
    UserManager,
    auth_backend,
    current_active_user,
    fastapi_users,
    get_jwt_strategy,
    get_user_manager,
)
from app.core.config import settings
from app.db.session import get_session
from app.models.user import User
from app.schemas.auth import AccessToken, RegistrationInfo
from app.schemas.user import RegisterRequest, UserCreate, UserRead, UserUpdate
from app.services import refresh_tokens, registration

router = APIRouter()

#: The refresh token, read out of the cookie.
#:
#: An explicit alias because the cookie is called `duofy_refresh` while the
#: parameter is called `refresh_token` — without it FastAPI would look for a cookie
#: named after the parameter. `None` when no cookie was sent, which is the normal
#: case for anyone who has never signed in.
RefreshCookie = Annotated[str | None, Cookie(alias=settings.cookie_name)]


def _set_cookie(response: Response, token: str) -> None:
    """Hand the refresh token to the browser.

    `path` is narrowed to the refresh endpoint: the cookie is useless everywhere
    else, so there is no reason to send it along with every request. That also keeps
    it out of the way of the bearer token, which does the actual authenticating.
    """
    response.set_cookie(
        settings.cookie_name,
        token,
        max_age=settings.refresh_lifetime_seconds,
        path="/api/v1/auth",
        domain=settings.cookie_domain,
        secure=settings.cookie_secure,
        httponly=True,
        samesite=settings.cookie_samesite,
    )


def _clear_cookie(response: Response) -> None:
    """Remove it again — with the same path and domain, or the browser keeps it."""
    response.delete_cookie(
        settings.cookie_name,
        path="/api/v1/auth",
        domain=settings.cookie_domain,
        secure=settings.cookie_secure,
        httponly=True,
        samesite=settings.cookie_samesite,
    )


async def _issue(
    response: Response,
    session: AsyncSession,
    user: User,
) -> AccessToken:
    """Both halves at once: a short access token, and a session behind a cookie."""
    access = await get_jwt_strategy().write_token(user)
    refresh = await refresh_tokens.issue(session, user.id)
    _set_cookie(response, refresh)
    return AccessToken(access_token=access)


@router.get("/auth/registration", response_model=RegistrationInfo, tags=["auth"])
async def registration_info() -> RegistrationInfo:
    """Who may register here, so the sign-up page can show the right thing.

    Public on purpose: the page needs it before anybody is signed in, and the mode
    is no secret — a closed door says "closed" to everyone who knocks.
    """
    return RegistrationInfo(mode=settings.registration_mode)


@router.post(
    "/auth/register",
    response_model=UserRead,
    status_code=status.HTTP_201_CREATED,
    tags=["auth"],
)
async def register(
    body: RegisterRequest,
    request: Request,
    user_manager: UserManager = Depends(get_user_manager),
    session: AsyncSession = Depends(get_session),
) -> User:
    """Create an account, if `REGISTRATION_MODE` lets this person in.

    Replaces fastapi-users' register route, which knows no modes. The answers to a
    taken address and a weak password keep its codes, so the frontend needs no new
    mapping for them.
    """
    invitation = await registration.admit(session, body.email, body.invitation_token)
    user_manager.user_db.invitation = invitation
    try:
        return await user_manager.create(
            UserCreate(**body.model_dump(exclude={"invitation_token"})),
            safe=True,
            request=request,
        )
    except UserAlreadyExists as error:
        await session.rollback()  # let go of the invitation lock
        raise HTTPException(
            status.HTTP_400_BAD_REQUEST, detail={"code": "REGISTER_USER_ALREADY_EXISTS"}
        ) from error
    except InvalidPasswordException as error:
        await session.rollback()
        raise HTTPException(
            status.HTTP_400_BAD_REQUEST,
            detail={"code": "REGISTER_INVALID_PASSWORD", "reason": error.reason},
        ) from error


@router.post("/auth/login", response_model=AccessToken, tags=["auth"])
async def login(
    response: Response,
    credentials: OAuth2PasswordRequestForm = Depends(),
    user_manager: UserManager = Depends(get_user_manager),
    session: AsyncSession = Depends(get_session),
) -> AccessToken:
    """Sign in with email and password.

    The form fields are called `username` and `password` because that is what the
    OAuth2 password flow prescribes — `username` carries the email address.

    One error for both wrong password and unknown address, deliberately: separate
    messages would tell anyone which addresses are registered here.
    """
    user = await user_manager.authenticate(credentials)
    if user is None or not user.is_active:
        raise HTTPException(
            status.HTTP_400_BAD_REQUEST,
            detail={"code": "LOGIN_BAD_CREDENTIALS"},
        )
    return await _issue(response, session, user)


@router.post("/auth/refresh", response_model=AccessToken, tags=["auth"])
async def refresh(
    response: Response,
    session: AsyncSession = Depends(get_session),
    refresh_token: RefreshCookie = None,
) -> AccessToken:
    """Trade the refresh token for a fresh pair.

    Takes nothing from the request body: the token comes out of the cookie, which is
    the point of putting it there.

    Every call replaces the token. A client that keeps the old one gets a 401 the
    next time — and if the old one shows up, the service treats that as theft and
    ends every session of that user. See `refresh_tokens.rotate`.
    """
    if refresh_token is None:
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, detail={"code": "refresh_token_missing"})

    try:
        user_id, fresh = await refresh_tokens.rotate(session, refresh_token)
    except refresh_tokens.RefreshTokenError as error:
        _clear_cookie(response)
        code = "refresh_token_reused" if error.all_sessions_ended else "refresh_token_invalid"
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, detail={"code": code}) from error

    user = await session.get(User, user_id)
    if user is None or not user.is_active:
        # The account was deleted or blocked while the session was open. The row is
        # gone through the cascade or is about to be; either way this is over.
        await refresh_tokens.revoke(session, fresh)
        _clear_cookie(response)
        raise HTTPException(status.HTTP_401_UNAUTHORIZED, detail={"code": "refresh_token_invalid"})

    access = await get_jwt_strategy().write_token(user)
    _set_cookie(response, fresh)
    return AccessToken(access_token=access)


@router.post("/auth/logout", status_code=status.HTTP_204_NO_CONTENT, tags=["auth"])
async def logout(
    response: Response,
    session: AsyncSession = Depends(get_session),
    refresh_token: RefreshCookie = None,
) -> None:
    """End this session.

    Deletes the row, so it takes effect immediately rather than whenever the token
    would have expired. The access token still works for its remaining minutes —
    that is the price of a signed token, and the reason it only lasts fifteen.

    Answers 204 even without a token: whoever asks to be signed out is signed out.
    """
    if refresh_token is not None:
        await refresh_tokens.revoke(session, refresh_token)
    _clear_cookie(response)


@router.post("/auth/logout-everywhere", status_code=status.HTTP_204_NO_CONTENT, tags=["auth"])
async def logout_everywhere(
    response: Response,
    session: AsyncSession = Depends(get_session),
    user: User = Depends(current_active_user),
) -> None:
    """End every session of the signed-in user, this one included.

    For the case where somebody suspects a device is not theirs any more — a lost
    phone, a borrowed laptop. Without this, a month-long session would have no
    emergency brake.
    """
    await refresh_tokens.revoke_all(session, user.id)
    _clear_cookie(response)


# fastapi-users' own login route stays available under /auth/jwt. It hands out a
# bearer token without starting a session, which is what the interactive docs use.
router.include_router(
    fastapi_users.get_auth_router(auth_backend),
    prefix="/auth/jwt",
    tags=["auth"],
)
router.include_router(
    fastapi_users.get_reset_password_router(),
    prefix="/auth",
    tags=["auth"],
)


def _own_profile_only() -> APIRouter:
    """fastapi-users' users router, minus the routes that address someone else.

    Once `is_superuser` means something, `GET/PATCH/DELETE /users/{id}` would let an
    admin read, rewrite (including the password) and delete any account. The admin
    runs the instance and owns nobody's data (#168), so only `/users/me` stays.
    """
    users = fastapi_users.get_users_router(UserRead, UserUpdate)
    users.routes[:] = [route for route in users.routes if "{id}" not in getattr(route, "path", "")]
    return users


router.include_router(_own_profile_only(), prefix="/users", tags=["users"])
