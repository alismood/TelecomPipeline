namespace TelecomPipeline;

public static class CallPricing
{
    public static decimal CalculateCost(in CallRecord record)
    {
        decimal unroundedCost = record switch
        {
            // Order 1a: Explicit defensive check for NaN duration
            { DurationMinutes: double.NaN } =>
                throw new ArgumentException("DurationMinutes cannot be NaN.", nameof(record)),

            // Order 1b: Invalid record (including default(CallRecord)) or out-of-range/infinite duration
            var r when string.IsNullOrWhiteSpace(r.RecordId)
                       || string.IsNullOrWhiteSpace(r.DestinationCountry)
                       || double.IsInfinity(r.DurationMinutes)
                       || r.DurationMinutes < 0.0
                       || r.DurationMinutes > 10_000.0 =>
                throw new ArgumentException("CallRecord is invalid or out of range.", nameof(record)),

            // Order 2: Roaming + KZ + duration < 1.0 -> Flat 50.00 KZT
            { IsRoaming: true, DestinationCountry: "KZ", DurationMinutes: < 1.0 } =>
                50.00m,

            // Order 3: Non-roaming + KZ -> 15.00 KZT per minute
            { IsRoaming: false, DestinationCountry: "KZ" } r =>
                (decimal)r.DurationMinutes * 15.00m,

            // Order 4: Roaming + duration >= 10.0 -> 120.00 KZT per minute
            { IsRoaming: true, DurationMinutes: >= 10.0 } r =>
                (decimal)r.DurationMinutes * 120.00m,

            // Order 5: All remaining valid records (_ fallback) -> 45.00 KZT per minute
            _ =>
                (decimal)record.DurationMinutes * 45.00m
        };

        return Math.Round(unroundedCost, 2, MidpointRounding.AwayFromZero);
    }
}