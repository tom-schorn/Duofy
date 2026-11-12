namespace Duofy.Schemas;

/// <summary>
/// Der Inhalt einer Fehlerantwort der API, zum Beispiel <c>{ "code": "email_taken" }</c>.
///
/// Die API schickt vor allem einen Code. Die Oberfläche übersetzt den Code in einen deutschen Satz aus ihrer
/// Ressourcendatei. Bei Fehlern, die ein Assistent oder ein Skript erklärt bekommen soll (Issue 137), steht in
/// <c>Message</c> zusätzlich ein deutscher Satz.
/// </summary>
public sealed record ErrorResponse(string Code, string? Message = null);

/// <summary>
/// Alle Fehlercodes, die die API schicken kann. Die Oberfläche braucht für jeden
/// dieser Codes einen Text.
/// </summary>
public static class ErrorCodes
{
    public const string FirstNameRequired = "first_name_required";
    public const string FirstNameTooLong = "first_name_too_long";
    public const string LastNameRequired = "last_name_required";
    public const string LastNameTooLong = "last_name_too_long";
    public const string EmailInvalid = "email_invalid";
    public const string EmailTaken = "email_taken";
    public const string PasswordTooShort = "password_too_short";
    public const string PasswordWrong = "password_wrong";
    public const string TermsNotAccepted = "terms_not_accepted";

    // Buchungen
    public const string KindInvalid = "kind_invalid";
    public const string AmountInvalid = "amount_invalid";
    public const string NoteTooLong = "note_too_long";
    public const string PotRequired = "pot_required";
    public const string CategoryRequired = "category_required";
    public const string LinesRequired = "lines_required";
    public const string LinesTooMany = "lines_too_many";
    public const string LineAmountInvalid = "line_amount_invalid";
    public const string LinePotInvalid = "line_pot_invalid";
    public const string LineCategoryInvalid = "line_category_invalid";
    public const string LinesSumMismatch = "lines_sum_mismatch";
    public const string CounterAccountRequired = "counter_account_required";
    public const string CounterAccountUnexpected = "counter_account_unexpected";
    public const string TransferSameAccount = "transfer_same_account";
    public const string TransferPotInvalid = "transfer_pot_invalid";
    public const string TransferPotsRequired = "transfer_pots_required";
    public const string TransferCategoryInvalid = "transfer_category_invalid";
    public const string DateBeforeOpening = "date_before_opening";
    public const string DateInFuture = "date_in_future";
    public const string AccountClosed = "account_closed";
    public const string MonthInvalid = "month_invalid";

    // Konten
    public const string AccountNameRequired = "account_name_required";
    public const string AccountNameTooLong = "account_name_too_long";
    public const string AccountTypeInvalid = "account_type_invalid";
    public const string AccountIconInvalid = "account_icon_invalid";
    public const string OpeningBalanceInvalid = "opening_balance_invalid";
    public const string OpeningDateInFuture = "opening_date_in_future";
    public const string OpeningDateAfterBooking = "opening_date_after_booking";
    public const string IbanInvalid = "iban_invalid";
    public const string AccountNotFound = "account_not_found";

    // Kategorien
    public const string CategoryNameRequired = "category_name_required";
    public const string CategoryNameTooLong = "category_name_too_long";
    public const string CategoryNameTaken = "category_name_taken";
    public const string CategoryGroupInvalid = "category_group_invalid";
    public const string CategoryPotInvalid = "category_pot_invalid";
    public const string CategoryInUse = "category_in_use";
    public const string CategoryNotFound = "category_not_found";

    // Verträge
    public const string CommitmentTypeInvalid = "commitment_type_invalid";
    public const string CommitmentNameRequired = "commitment_name_required";
    public const string CommitmentNameTooLong = "commitment_name_too_long";
    public const string CommitmentIntervalInvalid = "commitment_interval_invalid";
    public const string CommitmentFirstDueDateRequired = "commitment_first_due_date_required";
    public const string CommitmentCategoryInvalid = "commitment_category_invalid";
    public const string CommitmentPotInvalid = "commitment_pot_invalid";
    public const string CommitmentTargetSameAccount = "commitment_target_same_account";
    public const string CommitmentTargetInvalid = "commitment_target_invalid";
    public const string CommitmentTargetOnlyForSavingsGoal = "commitment_target_only_for_savings_goal";
    public const string CommitmentTargetAmountInvalid = "commitment_target_amount_invalid";
    public const string PaymentMethodInvalid = "payment_method_invalid";
    public const string CommitmentNotFound = "commitment_not_found";

    // Sparziele (Issue 30)
    public const string SavingsGoalNotFound = "savings_goal_not_found";
    public const string SavingsGoalNameRequired = "savings_goal_name_required";
    public const string SavingsGoalNameTooLong = "savings_goal_name_too_long";
    public const string SavingsGoalTargetInvalid = "savings_goal_target_invalid";
    public const string SavingsGoalStartInvalid = "savings_goal_start_invalid";
    public const string SavingsGoalBookingInvalid = "savings_goal_booking_invalid";
    public const string SavingsGoalHasBookings = "savings_goal_has_bookings";
    public const string CommitmentGoalInvalid = "commitment_goal_invalid";

    // Schulden und Kredite (Issue 31)
    public const string DebtNotFound = "debt_not_found";
    public const string DebtKindInvalid = "debt_kind_invalid";
    public const string DebtNameRequired = "debt_name_required";
    public const string DebtNameTooLong = "debt_name_too_long";
    public const string DebtCreditorRequired = "debt_creditor_required";
    public const string DebtCreditorTooLong = "debt_creditor_too_long";
    public const string DebtAmountInvalid = "debt_amount_invalid";
    public const string DebtNoteTooLong = "debt_note_too_long";
    public const string DebtBookingInvalid = "debt_booking_invalid";
    public const string DebtHasBookings = "debt_has_bookings";
    public const string CommitmentDebtInvalid = "commitment_debt_invalid";

    // Ratenplan einer Schuld (Issue 137)
    public const string DebtReferenceTooLong = "debt_reference_too_long";
    public const string DebtAdjustedOpenInvalid = "debt_adjusted_open_invalid";
    public const string DebtPlanInvalid = "debt_plan_invalid";
    public const string DebtPlanNothingOpen = "debt_plan_nothing_open";
    public const string DebtPlanTooManyInstallments = "debt_plan_too_many_installments";
    public const string DebtPlanFromMonthRequired = "debt_plan_from_month_required";
    public const string DebtPlanStartInvalid = "debt_plan_start_invalid";
    public const string CommitmentIsDebtPlan = "commitment_is_debt_plan";

    // Monatsplan
    public const string PlanNotFound = "plan_not_found";
    public const string PositionNotFound = "position_not_found";
    public const string PositionLabelRequired = "position_label_required";
    public const string PositionLabelTooLong = "position_label_too_long";
    public const string PositionDayInvalid = "position_day_invalid";
    public const string PositionPotInvalid = "position_pot_invalid";
    public const string PositionCategoryInvalid = "position_category_invalid";
    public const string PositionHasBookings = "position_has_bookings";
    public const string PositionPaid = "position_paid";
    public const string PositionAlreadyMoved = "position_already_moved";
    public const string PositionMoved = "position_moved";
    public const string PositionNotMoved = "position_not_moved";
    public const string PositionCopyPaid = "position_copy_paid";
    public const string PositionCopyMoved = "position_copy_moved";
    public const string PositionTargetInvalid = "position_target_invalid";
    public const string PositionIncomeWithTarget = "position_income_with_target";
    public const string LinePositionInvalid = "line_position_invalid";
    public const string PlanMonthInvalid = "plan_month_invalid";
    public const string QuotaInvalid = "quota_invalid";
    public const string CarryOverExists = "carry_over_exists";
    public const string CarryOverNotFound = "carry_over_not_found";
    public const string CarryOverBooking = "carry_over_booking";
    public const string CarryOverBalanceDuplicate = "carry_over_balance_duplicate";
    public const string DefaultAccountRequired = "default_account_required";

    // Haushalt
    public const string HouseholdNotFound = "household_not_found";
    public const string NoHousehold = "no_household";
    public const string AlreadyInHousehold = "already_in_household";
    public const string NotHouseholdFounder = "not_household_founder";
    public const string HouseholdNameRequired = "household_name_required";
    public const string HouseholdNameTooLong = "household_name_too_long";
    public const string MemberNotFound = "member_not_found";
    public const string FounderCannotLeave = "founder_cannot_leave";
    public const string CannotRemoveSelf = "cannot_remove_self";
    public const string TooManyInvitations = "too_many_invitations";
    public const string PublicUrlMissing = "public_url_missing";
    public const string InvitationNotFound = "invitation_not_found";
    public const string InvitationNotOpen = "invitation_not_open";
    public const string InvitationExpired = "invitation_expired";

    // Server und Registrierung (Spec 70)
    public const string SetupCodeInvalid = "setup_code_invalid";
    public const string InvitationRequired = "invitation_required";
    public const string InvitationInvalid = "invitation_invalid";
    public const string RegistrationClosed = "registration_closed";
    public const string AdminRequired = "admin_required";
    public const string LastAdmin = "last_admin";

    // Mails, Bestätigung und Passwort vergessen (Spec 53)
    public const string TokenInvalid = "token_invalid";
    public const string EmailNotConfirmed = "email_not_confirmed";
    public const string UserNotFound = "user_not_found";

    // Freigaben
    public const string CannotGrantSelf = "cannot_grant_self";
    public const string GrantLevelInvalid = "grant_level_invalid";
    public const string OwnerNotFound = "owner_not_found";
    public const string NoCreateGranted = "no_create_granted";
    public const string NoEditGranted = "no_edit_granted";
    public const string NoDeleteGranted = "no_delete_granted";

    /// <summary>
    /// Die Anmeldung ist fehlgeschlagen. Absichtlich derselbe Code für falsches
    /// Passwort, unbekannte E-Mail und gesperrtes Konto, damit niemand erfährt,
    /// welche Adressen registriert sind.
    /// </summary>
    public const string LoginFailed = "login_failed";
}
