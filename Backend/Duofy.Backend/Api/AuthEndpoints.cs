using System.Security.Claims;
using Duofy.Schemas;
using Duofy.Data;
using Duofy.Domain;
using Microsoft.AspNetCore.Authentication.BearerToken;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Duofy.Api;

/// <summary>
/// Registrierung, Anmeldung, Refresh und Abmelden überall (/api/v1/auth).
///
/// Microsoft bietet dafür fertig MapIdentityApi an. Dessen Registrierung kennt aber
/// keine Namen und keine Zustimmung; deshalb stehen die Endpunkte hier selbst,
/// gebaut wie MapIdentityApi intern: mit dem SignInManager und dem Verfahren
/// BearerToken, das die Tokens erzeugt und prüft.
/// </summary>
internal static class AuthEndpoints
{
    public const string RateLimitPolicy = "auth";

    private const string LogCategory = "Duofy.Api.Auth";

    public static void MapAuthEndpoints(this IEndpointRouteBuilder v1)
    {
        var auth = v1.MapGroup("/auth").RequireRateLimiting(RateLimitPolicy);
        auth.MapPost("/register", RegisterAsync);
        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/refresh", RefreshAsync);
        auth.MapPost("/logout-everywhere", LogoutEverywhereAsync).RequireAuthorization();
    }

    /// <summary>
    /// Registriert eine Person (Spec 57) und entscheidet vorher, ob sie auf diesen Server darf (Spec 70):
    /// (1) Solange es keinen Administrator gibt, macht der richtige Einrichtungscode sie zum Administrator, in jedem
    /// Modus; ein falscher Code ergibt 400 setup_code_invalid, egal ob überhaupt einer eingestellt ist.
    /// (2) Sonst entscheidet der Modus: open lässt durch, invite verlangt eine offene Einladung, closed niemanden.
    ///
    /// Das geschieht vor der Frage, ob die Adresse schon vergeben ist, damit ein geschlossener Server nicht verrät,
    /// welche Adressen es auf ihm gibt. Einladung, Administrator und Konto entstehen in einer Transaktion: Scheitert
    /// das Konto, bleibt die Einladung offen.
    /// </summary>
    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        UserManager<AppUser> users,
        DuofyDbContext db,
        InstanceSettings settings,
        InstanceAdmins admins,
        PublicLinks links,
        Mailer mailer,
        TimeProvider time,
        ILoggerFactory loggers,
        CancellationToken cancellationToken)
    {
        var firstName = AccountValidation.Clean(request.FirstName);
        var lastName = AccountValidation.Clean(request.LastName);
        var email = AccountValidation.Clean(request.Email);

        var error = AccountValidation.CheckNames(firstName, lastName)
            ?? AccountValidation.CheckEmail(email)
            ?? AccountValidation.CheckPassword(request.Password)
            ?? (request.AcceptedTerms ? null : ErrorCodes.TermsNotAccepted);
        if (error is not null)
        {
            return ApiErrors.BadRequest(error);
        }

        var logger = loggers.CreateLogger(LogCategory);
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var becomesAdmin = false;
        if (!string.IsNullOrWhiteSpace(request.SetupCode))
        {
            await admins.LockAsync(cancellationToken);
            if (!await admins.ExistAsync(cancellationToken))
            {
                if (!settings.IsSetupCode(request.SetupCode))
                {
                    logger.LogWarning("Registrierung mit falschem Einrichtungscode abgelehnt.");
                    return ApiErrors.BadRequest(ErrorCodes.SetupCodeInvalid);
                }
                becomesAdmin = true;
            }
        }

        RegistrationInvitation? invitation = null;
        if (!becomesAdmin)
        {
            switch (settings.RegistrationMode)
            {
                case RegistrationModes.Closed:
                    return Forbidden(ErrorCodes.RegistrationClosed);
                case RegistrationModes.Invite:
                    if (string.IsNullOrWhiteSpace(request.InvitationToken))
                    {
                        return Forbidden(ErrorCodes.InvitationRequired);
                    }
                    // Die Zeile bleibt bis zum Ende gesperrt: Zwei, die denselben Link zur selben Zeit benutzen,
                    // kommen nacheinander dran, und der zweite findet ihn verbraucht.
                    var token = request.InvitationToken.Trim();
                    invitation = await db.RegistrationInvitations
                        .FromSqlInterpolated($"SELECT * FROM registration_invitations WHERE token = {token} FOR UPDATE")
                        .FirstOrDefaultAsync(cancellationToken);
                    if (invitation is null || !invitation.Admits(email, now))
                    {
                        // Unbekannt, benutzt, abgelaufen oder für eine andere Adresse: dieselbe Antwort für alles.
                        logger.LogInformation("Registrierung mit ungültiger Einladung abgelehnt.");
                        return Forbidden(ErrorCodes.InvitationInvalid);
                    }
                    break;
            }
        }

        // Identity vergleicht E-Mails ohne Rücksicht auf Groß- und Kleinschreibung.
        if (await users.FindByEmailAsync(email) is not null)
        {
            return ApiErrors.BadRequest(ErrorCodes.EmailTaken);
        }

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            TermsAcceptedAt = now,
            IsInstanceAdmin = becomesAdmin,
        };
        IdentityResult result;
        try
        {
            result = await users.CreateAsync(user, request.Password);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Zwei Registrierungen mit derselben Adresse zur selben Zeit: Beide haben
            // die Adresse oben noch frei gefunden, der eindeutige Index lässt nur eine durch.
            logger.LogInformation("Registrierung abgelehnt, die Adresse wurde gerade vergeben.");
            return ApiErrors.BadRequest(ErrorCodes.EmailTaken);
        }
        if (!result.Succeeded)
        {
            // Die eigenen Prüfungen oben fangen das Erwartbare ab. Was hier noch
            // ankommt, wird übersetzt, so gut es geht, und sonst als Fehler gemeldet.
            var codes = result.Errors.Select(e => e.Code).ToList();
            if (codes.Contains(nameof(IdentityErrorDescriber.DuplicateEmail))
                || codes.Contains(nameof(IdentityErrorDescriber.DuplicateUserName)))
            {
                return ApiErrors.BadRequest(ErrorCodes.EmailTaken);
            }
            if (codes.Contains(nameof(IdentityErrorDescriber.InvalidEmail))
                || codes.Contains(nameof(IdentityErrorDescriber.InvalidUserName)))
            {
                return ApiErrors.BadRequest(ErrorCodes.EmailInvalid);
            }
            if (codes.Contains(nameof(IdentityErrorDescriber.PasswordTooShort)))
            {
                return ApiErrors.BadRequest(ErrorCodes.PasswordTooShort);
            }
            logger.LogError("Registrierung von Identity abgelehnt: {Codes}", string.Join(", ", codes));
            return Results.Problem(statusCode: StatusCodes.Status500InternalServerError);
        }

        // Das erste Konto gehört immer der gerade angelegten Person, nie einer Kennung aus der Anfrage (Spec 13).
        // Beim Registrieren ist noch niemand angemeldet, also gibt es keinen Besitzer zu prüfen: die eine enge
        // Ausnahme vom Besitzer-Filter für genau dieses Hinzufügen und Speichern (Entscheidung P-041).
        // Die Registrierung nutzt vorerst immer Deutsch; ein Sprachfeld gibt es nicht (Entscheidung P-040).
        using (db.Owners.Bypass("Erstes Konto bei der Registrierung"))
        {
            db.Accounts.Add(new Account
            {
                Id = Guid.NewGuid(),
                OwnerId = user.Id,
                Name = DefaultAccountName.For("de"),
                Type = AccountType.Checking,
                OpeningBalance = 0m,
                // Am Starttag lässt sich nichts buchen (Spec 59, R6); dieses eine Konto startet deshalb am Vortag,
                // damit man gleich am ersten Tag buchen kann.
                OpeningDate = Berlin.Today(time).AddDays(-1),
                CountsAsAvailable = Account.CountsAsAvailableFor(AccountType.Checking),
                IsDefault = true,
                Active = true,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync(cancellationToken);
        }
        if (invitation is not null)
        {
            invitation.Use(user.Id, now);
            await db.SaveChangesAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);

        if (becomesAdmin)
        {
            logger.LogWarning("Konto {UserId} ist mit dem Einrichtungscode Administrator dieses Servers geworden.", user.Id);
        }
        logger.LogInformation("Konto {UserId} registriert (Einladung: {InvitationId}).", user.Id, invitation?.Id);

        // Auf der gehosteten Version gilt das Konto erst mit bestätigter Adresse (Spec 53, R2). Scheitert die Mail,
        // bleibt das Konto; die Person lässt sich den Link noch einmal schicken.
        if (mailer.IsOn)
        {
            await MailEndpoints.SendConfirmationAsync(user, users, links, mailer, cancellationToken);
        }
        return Results.Created("/api/v1/users/me", value: null);
    }

    private static IResult Forbidden(string code) =>
        Results.Json(new ErrorResponse(code), statusCode: StatusCodes.Status403Forbidden);

    /// <summary>
    /// Meldet mit E-Mail und Passwort an. Auf der gehosteten Version braucht das Konto eine bestätigte Adresse (Spec 53,
    /// R2): sonst 403 email_not_confirmed. Das sagt die API erst nach dem richtigen Passwort, damit niemand ohne
    /// Passwort erfährt, dass es das Konto gibt.
    /// </summary>
    private static async Task<IResult> LoginAsync(
        LoginRequest request, SignInManager<AppUser> signIn, HostedSettings hosted, ILoggerFactory loggers)
    {
        signIn.AuthenticationScheme = IdentityConstants.BearerScheme;
        var logger = loggers.CreateLogger(LogCategory);
        var user = await signIn.UserManager.FindByNameAsync(AccountValidation.Clean(request.Email));
        var result = user is null
            ? SignInResult.Failed
            : await signIn.CheckPasswordSignInAsync(user, request.Password ?? "", lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            // Absichtlich dieselbe Antwort für falsches Passwort, unbekannte E-Mail und
            // Sperre. Ins Protokoll kommt nur die Art, nie die Adresse oder das Passwort.
            logger.LogInformation("Anmeldung fehlgeschlagen (gesperrt: {IsLockedOut}).", result.IsLockedOut);
            return ApiErrors.Unauthorized(ErrorCodes.LoginFailed);
        }
        if (hosted.Hosted && !user!.EmailConfirmed)
        {
            logger.LogInformation("Anmeldung von Konto {UserId} abgelehnt: Adresse nicht bestätigt.", user.Id);
            return Results.Json(new ErrorResponse(ErrorCodes.EmailNotConfirmed), statusCode: StatusCodes.Status403Forbidden);
        }

        // SignInAsync schreibt über das Verfahren BearerToken die Antwort mit dem
        // Token-Paar; hier kommt nichts mehr dazu.
        await signIn.SignInAsync(user!, isPersistent: false);
        return Results.Empty;
    }

    private static async Task<IResult> RefreshAsync(
        RefreshRequest request,
        SignInManager<AppUser> signIn,
        IOptionsMonitor<BearerTokenOptions> bearerOptions,
        TimeProvider time)
    {
        // Das Refresh-Token ist ein verschlüsseltes Paket mit Benutzer und Ablaufzeit.
        // Ein verändertes oder fremdes Token lässt sich nicht entschlüsseln.
        var protector = bearerOptions.Get(IdentityConstants.BearerScheme).RefreshTokenProtector;
        var ticket = protector.Unprotect(request.RefreshToken);

        // Der Sicherheitsstempel des Benutzers muss noch derselbe sein wie bei der
        // Anmeldung. Abmelden überall, ein neues Passwort und das Löschen ändern ihn.
        if (ticket?.Properties.ExpiresUtc is not { } expiresUtc
            || time.GetUtcNow() >= expiresUtc
            || await signIn.ValidateSecurityStampAsync(ticket.Principal) is not { } user)
        {
            return Results.Challenge();
        }

        var principal = await signIn.CreateUserPrincipalAsync(user);
        return Results.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
    }

    private static async Task<IResult> LogoutEverywhereAsync(
        ClaimsPrincipal principal, UserManager<AppUser> users, ILoggerFactory loggers)
    {
        if (await users.GetUserAsync(principal) is not { } user)
        {
            return Results.Unauthorized();
        }

        // Ein neuer Sicherheitsstempel macht jedes vorher ausgegebene Refresh-Token
        // ungültig. Access-Tokens laufen spätestens nach 15 Minuten ab.
        await users.UpdateSecurityStampAsync(user);
        loggers.CreateLogger(LogCategory).LogInformation("Konto {UserId} überall abgemeldet.", user.Id);
        return Results.NoContent();
    }
}
