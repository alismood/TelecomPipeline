namespace TelecomPipeline;

public readonly record struct CallRecord
{
    public string RecordId { get; }
    public string DestinationCountry { get; }
    public double DurationMinutes { get; }
    public bool IsRoaming { get; }

    public CallRecord(
        string recordId,
        string destinationCountry,
        double durationMinutes,
        bool isRoaming)
    {
        if (string.IsNullOrWhiteSpace(recordId))
        {
            throw new ArgumentException(
                "RecordId cannot be null, empty, or whitespace.",
                nameof(recordId));
        }

        if (string.IsNullOrWhiteSpace(destinationCountry))
        {
            throw new ArgumentException(
                "DestinationCountry cannot be null, empty, or whitespace.",
                nameof(destinationCountry));
        }

        if (double.IsNaN(durationMinutes) ||
            double.IsInfinity(durationMinutes) ||
            durationMinutes < 0.0 ||
            durationMinutes > 10_000.0)
        {
            throw new ArgumentException(
                "DurationMinutes must be a finite number between 0.0 and 10,000.0.",
                nameof(durationMinutes));
        }

        RecordId = recordId;
        DestinationCountry = destinationCountry;
        DurationMinutes = durationMinutes;
        IsRoaming = isRoaming;
    }
}