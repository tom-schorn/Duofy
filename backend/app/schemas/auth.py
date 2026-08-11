from pydantic import BaseModel


class AccessToken(BaseModel):
    """What signing in and refreshing hand back.

    Only the **access token** travels in the body. The refresh token goes into a
    cookie the server sets, so that JavaScript cannot read it — which is the whole
    reason for splitting the two.

    **Deliberately not built on `app.schemas.base.Schema`.** Every other schema in
    this API turns snake_case into camelCase on the wire, but this response is not
    ours to name: OAuth2 prescribes `access_token` and `token_type`, fastapi-users'
    own login route answers with exactly those, and the interactive docs read them to
    fill in the Authorize dialog. Renaming them to `accessToken` would break all
    three for the sake of consistency nobody benefits from.
    """

    access_token: str
    token_type: str = "bearer"
