namespace Nestly.DataTrimmer;

/// <summary>What a run did, so the caller can print it and a reader can check the README numbers.</summary>
internal sealed record TrimReport(int Read, int Complete, int Eligible, int Written, long Bytes);
