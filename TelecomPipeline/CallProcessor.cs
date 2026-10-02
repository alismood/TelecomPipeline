using System.Runtime.ExceptionServices;

namespace TelecomPipeline;

public static class CallProcessor
{
    // Task 5: Sequential baseline
    public static decimal ProcessCallsSequential(CallRecord[] records)
    {
        if (records is null)
        {
            throw new ArgumentException("Input records array cannot be null.", nameof(records));
        }

        decimal total = 0.00m;
        for (int i = 0; i < records.Length; i++)
        {
            total += CallPricing.CalculateCost(in records[i]);
        }

        return total;
    }

    // Task 4: Partitioned two-thread pipeline
    public static decimal ProcessCallsParallel(CallRecord[] records)
    {
        if (records is null)
        {
            throw new ArgumentException("Input records array cannot be null.", nameof(records));
        }

        if (records.Length == 0)
        {
            return 0.00m;
        }

        if (records.Length % 2 != 0)
        {
            throw new ArgumentException(
                "Input records array must have an even length for two-way partitioning.",
                nameof(records));
        }

        int mid = records.Length / 2;

        // Split the input array into two equal arrays using C# range syntax
        CallRecord[] leftInput = records[..mid];
        CallRecord[] rightInput = records[mid..];

        // Allocate two independent output arrays before starting either worker
        decimal[] leftOutput = new decimal[leftInput.Length];
        decimal[] rightOutput = new decimal[rightInput.Length];

        Exception? leftException = null;
        Exception? rightException = null;

        Thread leftWorker = new Thread(() =>
        {
            try
            {
                for (int i = 0; i < leftInput.Length; i++)
                {
                    leftOutput[i] = CallPricing.CalculateCost(in leftInput[i]);
                }
            }
            catch (Exception ex)
            {
                leftException = ex;
            }
        });

        Thread rightWorker = new Thread(() =>
        {
            try
            {
                for (int i = 0; i < rightInput.Length; i++)
                {
                    rightOutput[i] = CallPricing.CalculateCost(in rightInput[i]);
                }
            }
            catch (Exception ex)
            {
                rightException = ex;
            }
        });

        // Start both threads
        leftWorker.Start();
        rightWorker.Start();

        // Join both threads
        leftWorker.Join();
        rightWorker.Join();

        // Rethrow captured failures on the calling thread; never return partial totals
        if (leftException is not null && rightException is not null)
        {
            throw new AggregateException("Both worker threads encountered errors.", leftException, rightException);
        }

        if (leftException is not null)
        {
            ExceptionDispatchInfo.Capture(leftException).Throw();
        }

        if (rightException is not null)
        {
            ExceptionDispatchInfo.Capture(rightException).Throw();
        }

        // Aggregate isolated output arrays on the calling thread
        decimal total = 0.00m;
        for (int i = 0; i < leftOutput.Length; i++)
        {
            total += leftOutput[i];
        }

        for (int i = 0; i < rightOutput.Length; i++)
        {
            total += rightOutput[i];
        }

        return total;
    }
}