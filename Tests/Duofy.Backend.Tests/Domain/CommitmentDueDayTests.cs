

namespace Duofy.Domain.Tests
{
    public class CommitmentDueDayTests
    {
        // Alle Beispielverträge gehören demselben ausgedachten Benutzer.
        private static readonly Guid Owner = Guid.Parse("11111111-1111-1111-1111-111111111111");

        [Fact]
        public void Due_day_31_moves_to_the_28th_in_february()
        {
            var rent = new Commitment
            {
                Name = "Test",
                Amount = 100.00m,
                Type = CommitmentType.Contract,
                OwnerId = Owner,
                IntervalMonths = 1,
                FirstDueDate = new DateOnly(2026, 1, 31)
            };
            Assert.Equal(28, rent.EffectiveDueDay(2026, 2));
        }
        [Fact]
        public void Due_day_15_not_moved()
        {
            var rent = new Commitment
            {
                Name = "Test",
                Amount = 100.00m,
                Type = CommitmentType.Contract,
                OwnerId = Owner,
                IntervalMonths = 1,
                FirstDueDate = new DateOnly(2026, 2, 15)

            };
            Assert.Equal(15, rent.EffectiveDueDay(2026, 3));
        }
        [Fact]
        public void Due_day_29_in_leap_year()
        {
            var rent = new Commitment
            {
                Name = "Test",
                Amount = 100.00m,
                Type = CommitmentType.Contract,
                OwnerId = Owner,
                IntervalMonths = 1,
                FirstDueDate = new DateOnly(2024, 3, 30)

            };
            Assert.Equal(29, rent.EffectiveDueDay(2028, 2));
        }
    }
}
