import uuid
from datetime import date
from decimal import Decimal

from pydantic import Field, model_validator

from app.models.enums import Block, Category, CommitmentType, PaymentMethod, Rhythm
from app.schemas.base import Schema


class CommitmentBase(Schema):
    name: str = Field(min_length=1, max_length=200)
    amount: Decimal = Field(ge=0, max_digits=12, decimal_places=2)
    category: Category
    block: Block
    household_id: uuid.UUID | None = None
    rhythm: Rhythm
    first_due_date: date | None = None
    due_day: int = Field(ge=1, le=31)
    active: bool = True
    #: Wird in die erzeugten Posten kopiert, dort je Monat überschreibbar.
    payment_method: PaymentMethod | None = None

    # nur bei savings_goal
    target_amount: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)
    target_date: date | None = None

    # nur bei debt
    remaining_debt: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)


class CommitmentCreate(CommitmentBase):
    type: CommitmentType

    @model_validator(mode="after")
    def check_shape(self) -> "CommitmentCreate":
        """Dieselben Regeln, die auch die Datenbank erzwingt — nur früher.

        Ein CHECK-Constraint liefert einen Datenbankfehler. Hier kommt
        stattdessen ein Fehler-**Code** heraus, den das Frontend übersetzen
        kann.
        """
        if self.rhythm is not Rhythm.MONTHLY and self.first_due_date is None:
            raise ValueError("first_due_date_required")

        if self.type is not CommitmentType.SAVINGS_GOAL and (
            self.target_amount is not None or self.target_date is not None
        ):
            raise ValueError("target_only_for_savings_goal")

        if self.type is not CommitmentType.DEBT and self.remaining_debt is not None:
            raise ValueError("remaining_debt_only_for_debt")

        # Bei nicht-monatlich muss der Fälligkeitstag zum Startdatum passen,
        # sonst widersprechen sich zwei Angaben über dasselbe.
        if self.first_due_date is not None and self.due_day != self.first_due_date.day:
            raise ValueError("due_day_must_match_first_due_date")

        return self


class CommitmentUpdate(Schema):
    """Alles optional. Der Typ lässt sich nicht ändern — dafür neu anlegen."""

    name: str | None = Field(default=None, min_length=1, max_length=200)
    amount: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)
    category: Category | None = None
    block: Block | None = None
    household_id: uuid.UUID | None = None
    rhythm: Rhythm | None = None
    first_due_date: date | None = None
    due_day: int | None = Field(default=None, ge=1, le=31)
    active: bool | None = None
    payment_method: PaymentMethod | None = None
    target_amount: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)
    target_date: date | None = None
    remaining_debt: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)


class CommitmentRead(CommitmentBase):
    id: uuid.UUID
    type: CommitmentType
    owner_id: uuid.UUID
