namespace Duofy.ApiClient;

/// <summary>
/// Schreibt Tokens in die Ablage und meldet jede Änderung an den Anmeldezustand.
/// So kann kein Client vergessen, den Seiten Bescheid zu sagen.
/// </summary>
internal sealed class TokenSession
{
    private readonly ITokenStore store;
    private readonly TokenAuthenticationStateProvider authenticationState;

    public TokenSession(ITokenStore store, TokenAuthenticationStateProvider authenticationState)
    {
        this.store = store;
        this.authenticationState = authenticationState;
    }

    public Task<AuthTokens?> GetAsync(CancellationToken cancellationToken) => store.GetAsync(cancellationToken);

    /// <summary>Die Ablage war leer, obwohl die Seiten noch "angemeldet" zeigen können: Zustand neu ermitteln.</summary>
    public void NoteTokensMissing() => authenticationState.NotifyTokensMissing();

    public async Task SaveAsync(AuthTokens tokens, CancellationToken cancellationToken)
    {
        await store.SaveAsync(tokens, cancellationToken);
        authenticationState.NotifyTokensChanged();
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        await store.ClearAsync(cancellationToken);
        authenticationState.NotifyTokensChanged();
    }
}
