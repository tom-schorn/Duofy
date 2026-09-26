from decimal import Decimal

QUOTA_FIELDS = ("target_needs", "target_wants", "target_savings")


def check_quotas(needs: Decimal | None, wants: Decimal | None, savings: Decimal | None) -> None:
    """The three quotas describe the whole income after the buffer: they add up to 100.

    On an update they are sent together or not at all — one alone could only be
    checked against what is stored, and a guideline that shifts silently under a
    partial edit is not one a person can reason about.
    """
    values = (needs, wants, savings)
    if all(value is None for value in values):
        return
    if any(value is None for value in values):
        raise ValueError("quotas_incomplete")
    if sum(values) != Decimal(100):
        raise ValueError("quotas_must_sum_to_100")
