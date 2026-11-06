namespace Mercantec.Tech.Contracts;

/// <summary>Finite authoring plan. Every returned date becomes an independent quest occurrence.</summary>
public static class QuestRecurrence
{
    public static DateOnly[] Dates(QuestRepeatPlan plan)
    {
        var days = plan.EndsOn.DayNumber - plan.StartsOn.DayNumber;
        if (plan.StartsOn == default || days is < 0 or > 89 || plan.Frequency is not ("daily" or "weekdays" or "weekly"))
            throw new ArgumentException("Vælg en periode på højst 90 dage og en gyldig gentagelse.");
        return Enumerable.Range(0, days + 1).Select(plan.StartsOn.AddDays)
            .Where(date => plan.Frequency == "daily" || plan.Frequency == "weekly" && (date.DayNumber - plan.StartsOn.DayNumber) % 7 == 0
                || plan.Frequency == "weekdays" && date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday)).ToArray();
    }
}
