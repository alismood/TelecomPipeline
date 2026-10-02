using TelecomPipeline;

CallRecord[] sampleRecords =
[
    new CallRecord("CDR-001", "KZ", 4.0, isRoaming: false),
    new CallRecord("CDR-002", "KZ", 0.5, isRoaming: true),
    new CallRecord("CDR-003", "US", 10.0, isRoaming: true),
    new CallRecord("CDR-004", "DE", 3.0, isRoaming: false),
    new CallRecord("CDR-005", "XX", 2.0, isRoaming: false),
    new CallRecord("CDR-006", "KZ", 1.0, isRoaming: true)
];

decimal sequentialTotal = CallProcessor.ProcessCallsSequential(sampleRecords);
decimal parallelTotal = CallProcessor.ProcessCallsParallel(sampleRecords);

Console.WriteLine($"Records Processed : {sampleRecords.Length}");
Console.WriteLine($"Sequential Total  : {sequentialTotal:F2} KZT");
Console.WriteLine($"Parallel Total    : {parallelTotal:F2} KZT");
Console.WriteLine($"Totals Match      : {sequentialTotal == parallelTotal}");