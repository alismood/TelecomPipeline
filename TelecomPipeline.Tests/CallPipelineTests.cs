using TelecomPipeline;
using Xunit;

namespace TelecomPipeline.Tests;

public class CallPipelineTests
{
    // GROUP A: Tariff Correctness
    

    [Theory]    
    [InlineData("KZ", false, 4.0, 60.00)]
    [InlineData("KZ", true, 0.5, 50.00)]
    [InlineData("US", true, 10.0, 1200.00)]
    [InlineData("DE", false, 3.0, 135.00)]
    [InlineData("XX", false, 2.0, 90.00)]
    [InlineData("KZ", true, 1.0, 45.00)]
    public void GroupA_CalculateCost_ReturnsExpectedTariff(
        string country,
        bool isRoaming,
        double duration,
        decimal expectedCost)
    {
        var record = new CallRecord("REC-A", country, duration, isRoaming);

        decimal actualCost = CallPricing.CalculateCost(in record);

        Assert.Equal(expectedCost, actualCost);
    }

    [Fact]
    public void GroupA_CalculateCost_RoundsMidpointAwayFromZero()
    {
        // 0.005 * 45.00 = 0.225 -> rounds from zero to 0.23
        var record = new CallRecord("REC-ROUND", "US", 0.005, isRoaming: false);

        decimal actualCost = CallPricing.CalculateCost(in record);

        Assert.Equal(0.23m, actualCost);
    }
    
    // GROUP B: Invalid Inputs and Boundaries

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GroupB_Constructor_RejectsInvalidRecordId(string? invalidId)
    {
        Assert.Throws<ArgumentException>(() =>
            new CallRecord(invalidId!, "KZ", 5.0, isRoaming: false));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GroupB_Constructor_RejectsInvalidCountry(string? invalidCountry)
    {
        Assert.Throws<ArgumentException>(() =>
            new CallRecord("REC-1", invalidCountry!, 5.0, isRoaming: false));
    }

    [Theory]
    [InlineData(-0.0001)]
    [InlineData(-10.0)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(10_000.0001)]
    public void GroupB_Constructor_RejectsInvalidDurations(double invalidDuration)
    {
        Assert.Throws<ArgumentException>(() =>
            new CallRecord("REC-1", "KZ", invalidDuration, isRoaming: false));
    }

    [Fact]
    public void GroupB_CalculateCost_RejectsDefaultCallRecord()
    {
        CallRecord defaultRecord = default;

        Assert.Throws<ArgumentException>(() =>
            CallPricing.CalculateCost(in defaultRecord));
    }

    [Fact]
    public void GroupB_ZeroMinuteCalls_HandledCorrectly()
    {
        var roamingKzZero = new CallRecord("ZERO-1", "KZ", 0.0, isRoaming: true);
        var nonRoamingKzZero = new CallRecord("ZERO-2", "KZ", 0.0, isRoaming: false);
        var fallbackZero = new CallRecord("ZERO-3", "US", 0.0, isRoaming: true);

        Assert.Equal(50.00m, CallPricing.CalculateCost(in roamingKzZero));
        Assert.Equal(0.00m, CallPricing.CalculateCost(in nonRoamingKzZero));
        Assert.Equal(0.00m, CallPricing.CalculateCost(in fallbackZero));
    }

    [Fact]
    public void GroupB_TariffBoundaries_AreExact()
    {
        // Boundary 1: Roaming KZ < 1.0 (flat 50) vs == 1.0 (fallback 45/min)
        var justBelowOneMin = new CallRecord("B-1", "KZ", 0.9999, isRoaming: true);
        var exactlyOneMin = new CallRecord("B-2", "KZ", 1.0, isRoaming: true);

        Assert.Equal(50.00m, CallPricing.CalculateCost(in justBelowOneMin));
        Assert.Equal(45.00m, CallPricing.CalculateCost(in exactlyOneMin));

        // Boundary 2: Roaming < 10.0 (fallback 45/min) vs >= 10.0 (120/min)
        var justBelowTenMin = new CallRecord("B-3", "US", 9.9999, isRoaming: true);
        var exactlyTenMin = new CallRecord("B-4", "US", 10.0, isRoaming: true);
        var maxDuration = new CallRecord("B-5", "KZ", 10_000.0, isRoaming: false);

        // 9.9999 * 45.00 = 449.9955 -> 450.00
        Assert.Equal(450.00m, CallPricing.CalculateCost(in justBelowTenMin));
        Assert.Equal(1200.00m, CallPricing.CalculateCost(in exactlyTenMin));
        Assert.Equal(150_000.00m, CallPricing.CalculateCost(in maxDuration));
    }
    
    // GROUP C: Sequential–Parallel Agreement

    [Fact]
    public void GroupC_SequentialAndParallel_AgreeAcross100RunsOn1000Records()
    {
        const int recordCount = 1_000;
        var records = new CallRecord[recordCount];

        for (int i = 0; i < recordCount; i++)
        {
            records[i] = (i % 5) switch
            {
                0 => new CallRecord($"ID-{i}", "KZ", 0.5, isRoaming: true),     // Flat 50.00
                1 => new CallRecord($"ID-{i}", "KZ", 4.25, isRoaming: false),   // 15.00/min
                2 => new CallRecord($"ID-{i}", "US", 12.5, isRoaming: true),    // 120.00/min
                3 => new CallRecord($"ID-{i}", "DE", 3.75, isRoaming: false),   // 45.00/min fallback
                _ => new CallRecord($"ID-{i}", "KZ", 2.0, isRoaming: true)      // 45.00/min fallback
            };
        }

        // Snapshot copy to verify input records remain unchanged
        CallRecord[] snapshot = [..records];

        decimal sequentialTotal = CallProcessor.ProcessCallsSequential(records);

        for (int run = 0; run < 100; run++)
        {
            decimal parallelTotal = CallProcessor.ProcessCallsParallel(records);
            Assert.Equal(sequentialTotal, parallelTotal);
        }

        Assert.Equal(snapshot, records);
    }

    [Fact]
    public void GroupC_ParallelProcessor_HandlesEmptyAndRejectsOddOrNullArrays()
    {
        Assert.Equal(0.00m, CallProcessor.ProcessCallsParallel([]));

        var oddArray = new[]
        {
            new CallRecord("ODD-1", "KZ", 1.0, isRoaming: false),
            new CallRecord("ODD-2", "KZ", 2.0, isRoaming: false),
            new CallRecord("ODD-3", "KZ", 3.0, isRoaming: false)
        };

        Assert.Throws<ArgumentException>(() => CallProcessor.ProcessCallsParallel(oddArray));
        Assert.Throws<ArgumentException>(() => CallProcessor.ProcessCallsParallel(null!));
        Assert.Throws<ArgumentException>(() => CallProcessor.ProcessCallsSequential(null!));
    }

    [Fact]
    public void GroupC_ParallelProcessor_PropagatesWorkerExceptionForDefaultRecord()
    {
        // Contains a valid record and an uninitialized default(CallRecord)
        var recordsWithInvalidDefault = new CallRecord[]
        {
            new CallRecord("VALID-1", "KZ", 4.0, isRoaming: false),
            default
        };

        Assert.Throws<ArgumentException>(() =>
            CallProcessor.ProcessCallsParallel(recordsWithInvalidDefault));
    }
}