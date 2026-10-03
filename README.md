telecom_pipeline_readme
Assignment 2: Multithreaded Telecom Call Processing Pipeline
Course: FP 3222 – Introduction to Functional Programming
Platform: C# 12 / .NET 8
1. Project Overview & Structure
This project implements a deterministic, functional-first telecom call processing pipeline in C# 12 / .NET 8. It models Call Data Records (CDRs) as immutable value types, calculates call tariffs via a pure pattern-matching switch expression, and processes batches in parallel across two worker threads using isolated array partitioning without locks or shared mutable counters.
Assignment2_TelecomPipeline/
├── Assignment2.sln
├── README.md
├── TelecomPipeline/
│   ├── TelecomPipeline.csproj
│   ├── CallRecord.cs
│   ├── CallPricing.cs
│   ├── CallProcessor.cs
│   └── Program.cs
└── TelecomPipeline.Tests/
    ├── TelecomPipeline.Tests.csproj
    └── CallPipelineTests.cs
​
2. How to Build, Run, and Test
Open a terminal in the root solution directory and run the following commands:
# 1. Restore dependencies and compile the solution
dotnet build

# 2. Run the main console pipeline demonstration
dotnet run --project TelecomPipeline

# 3. Execute the automated xUnit test suite
dotnet test
​
If using JetBrains Rider:
Open the solution file (.sln).
Press Shift + F10 (or click the green Run button) to run TelecomPipeline.
Open the Unit Tests window and click Run All Tests to execute CallPipelineTests.
3. Functional Architecture: Pure Core vs. Impure Shell (Task 5)
The system is structured around the Functional Core, Imperative Shell architectural pattern:
Pure Functional Core
CallRecord (readonly record struct): Models an immutable value. Once constructed, its properties (RecordId, DestinationCountry, DurationMinutes, IsRoaming) cannot be mutated.
CallPricing.CalculateCost(in CallRecord record): A pure function.
Deterministic: Given identical CallRecord values, it always computes and returns the exact same decimal cost.
Side-Effect Free: It performs zero Console I/O, does not read or modify any static or global variables, and uses the in parameter modifier to pass the struct by readonly reference without mutating the caller's data.
Impure Imperative Shell
CallProcessor.ProcessCallsParallel: Coordinates OS threads (new Thread(...), Start(), Join()) and writes results into partition-local output arrays (leftOutput[i], rightOutput[i]). While array writes are local mutations, they are strictly confined to disjoint memory buffers per thread and never exposed until both threads join.
Program.Main: Handles all external side effects, such as Console.WriteLine, sample data generation, and top-level reporting.
4. Why readonly record struct Does Not Guarantee Validity (Task 1 Note)
Declaring CallRecord as a readonly record struct guarantees immutability after construction—meaning properties only have { get; } accessors and cannot be modified once initialized.
However, in C# and the .NET runtime, every value type (struct) automatically possesses an implicit, parameterless zero-initializing state accessible via:
default(CallRecord) or default
new CallRecord()
Uninitialized array elements (e.g., new CallRecord[10])
When default(CallRecord) is used, the .NET runtime zeroes out the struct's memory without executing the custom validating constructor. This produces an instance where:
RecordId is null
DestinationCountry is null
DurationMinutes is 0.0
IsRoaming is false
Because default(CallRecord) bypasses constructor validation, readonly alone does not guarantee that every instance in memory is valid. To protect the pipeline, CallPricing.CalculateCost includes defensive guard checks at the top of its switch expression to reject null/whitespace strings and invalid durations with an ArgumentException.
5. Data Race Analysis & Lost-Update Interleaving (Task 3)
In an unsafe imperative implementation where two threads increment a shared static counter (globalCallCounter++) without synchronization, a data race occurs because ++ is not an atomic operation. At the CPU/IL level, globalCallCounter++ expands into three separate steps:
Read: Load the current value of globalCallCounter from shared memory into a thread-local CPU register.
Modify: Add 
1
1 to the register value.
Write: Store the updated register value back into globalCallCounter in shared memory.
Concrete Lost-Update Interleaving
Assume globalCallCounter starts at 
100
100, and Thread 1 and Thread 2 process a call at the same time:
Step
Thread 1 Action
Thread 2 Action
Shared Memory (globalCallCounter)
1
Reads globalCallCounter (
100
100) into register 
𝑅
1
=
100
R 
1
​
 =100​
—
100
100​
2
—
Reads globalCallCounter (
100
100) into register 
𝑅
2
=
100
R 
2
​
 =100​
100
100​
3
Computes 
𝑅
1
=
100
+
1
=
101
R 
1
​
 =100+1=101​
—
100
100​
4
—
Computes 
𝑅
2
=
100
+
1
=
101
R 
2
​
 =100+1=101​
100
100​
5
Writes 
𝑅
1
R 
1
​
  (
101
101) back to globalCallCounter
—
101
101​
6
—
Writes 
𝑅
2
R 
2
​
  (
101
101) back to globalCallCounter
101
101 (1 update lost!)
Although 
2
2 calls were processed, the final counter is 
101
101 instead of 
102
102.
Why sequential execution does not race: In a single-threaded loop, each Read–Modify–Write cycle finishes completely before the next iteration starts.
How our pipeline eliminates the race without locks: Instead of using lock or Interlocked.Increment on a shared counter, CallProcessor partitions the input array at mid = records.Length / 2 into two independent slices (leftInput and rightInput) and allocates separate output arrays (leftOutput and rightOutput). Because Thread 1 only writes to leftOutput and Thread 2 only writes to rightOutput, no memory address is ever written by both threads concurrently.
6. Tariff Rules & Rounding Policy (Task 2)
CallPricing.CalculateCost evaluates rules using a C# pattern-matching switch expression and rounds the final cost to 
2
2 decimal places using MidpointRounding.AwayFromZero:
Condition
Rate per Minute
Formula
DurationMinutes == 0 (Valid record)
0.00
m
0.00m​
0.00
m
0.00m​
Domestic (KZ), Not Roaming
15.00
m
15.00m​
Duration
×
15.00
m
Duration×15.00m​
Domestic (KZ), Roaming
35.00
m
35.00m​
Duration
×
35.00
m
Duration×35.00m​
International (US, DE, GB), Not Roaming
50.00
m
50.00m​
Duration
×
50.00
m
Duration×50.00m​
International (US, DE, GB), Roaming
80.00
m
80.00m​
Duration
×
80.00
m
Duration×80.00m​
Other Country, Not Roaming
65.00
m
65.00m​
Duration
×
65.00
m
Duration×65.00m​
Other Country, Roaming
100.00
m
100.00m​
Duration
×
100.00
m
Duration×100.00m​
7. Design Limitations & Trade-Offs
Fixed Two-Thread Partitioning: Using two explicit Thread objects (leftThread and rightThread) is ideal for demonstrating deterministic partitioning and thread joining, but it does not automatically scale to 
8
8 or 
16
16 CPU cores and incurs OS thread creation overhead on small arrays.
Array Copying Overhead: Slicing with records[..mid] and records[mid..] allocates sub-arrays and copies struct elements before processing, trading extra memory allocation for complete thread isolation.
