import uuid
from datetime import datetime
from decimal import Decimal

from pydantic import Field

from app.models.enums import Block, Category, PaymentMethod, PlanStatus
from app.schemas.base import Schema


class PositionBase(Schema):
    label: str = Field(min_length=1, max_length=200)
    amount_planned: Decimal = Field(ge=0, max_digits=12, decimal_places=2)
    amount_actual: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)
    category: Category
    block: Block
    due_day: int = Field(ge=1, le=31)
    payment_method: PaymentMethod | None = None
    household_id: uuid.UUID | None = None


class PositionCreate(PositionBase):
    pass


class PositionUpdate(Schema):
    """Alles optional — `paid_at` wird über einen eigenen Endpunkt gesetzt."""

    label: str | None = Field(default=None, min_length=1, max_length=200)
    amount_planned: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)
    amount_actual: Decimal | None = Field(default=None, ge=0, max_digits=12, decimal_places=2)
    category: Category | None = None
    block: Block | None = None
    due_day: int | None = Field(default=None, ge=1, le=31)
    payment_method: PaymentMethod | None = None
    household_id: uuid.UUID | None = None


class PositionRead(PositionBase):
    id: uuid.UUID
    plan_id: uuid.UUID
    #: Leer bei Einmal-Posten, gesetzt wenn aus einer Verpflichtung erzeugt.
    commitment_id: uuid.UUID | None
    #: NULL = steht noch offen.
    paid_at: datetime | None


class PlanBase(Schema):
    target_needs: Decimal = Field(ge=0, le=100)
    target_wants: Decimal = Field(ge=0, le=100)
    target_savings: Decimal = Field(ge=0, le=100)
    buffer_percent: Decimal = Field(ge=0, le=100)


class PlanCreate(Schema):
    """Einen Monat anlegen.

    **Kein** „Vormonat übernehmen": das Wiederkehrende kommt aus den
    Verträgen, Einzelposten schreibt man von Hand.
    """

    year: int = Field(ge=2000, le=2100)
    month: int = Field(ge=1, le=12)


class PlanUpdate(Schema):
    target_needs: Decimal | None = Field(default=None, ge=0, le=100)
    target_wants: Decimal | None = Field(default=None, ge=0, le=100)
    target_savings: Decimal | None = Field(default=None, ge=0, le=100)
    buffer_percent: Decimal | None = Field(default=None, ge=0, le=100)


class BudgetTotals(Schema):
    needs: Decimal
    wants: Decimal
    savings: Decimal


class PlanSummary(PlanBase):
    """Eine Zeile in der Planübersicht.

    Die Summen kommen fertig aus dem Backend — die Übersicht darf nicht alle
    Posten aller Monate laden, nur um sie zu addieren.
    """

    year: int
    month: int
    status: PlanStatus

    #: Summe der Einnahme-Posten.
    income: Decimal
    #: Einnahmen minus Puffer — die Grundlage für die Quoten.
    #: **Nicht** „Verplanbar": das ist der noch freie Rest davon.
    budget: Decimal
    #: Verplant je Budget.
    spent: BudgetTotals
    #: Summe der Posten, die noch nicht abgehakt sind.
    unpaid: Decimal
    #: Haushalte, in die Posten dieses Plans einfließen. Leer = rein privat.
    household_ids: list[uuid.UUID]


class PlanRead(PlanSummary):
    id: uuid.UUID
    confirmed_at: datetime | None
    positions: list[PositionRead]
