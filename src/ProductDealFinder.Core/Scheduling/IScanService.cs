using System.Threading;
using System.Threading.Tasks;

namespace ProductDealFinder.Core.Scheduling;

public interface IScanService
{
    /// <summary>
    /// Executes a single scan run across all active product targets that have active thresholds.
    /// </summary>
    Task RunScanOnceAsync(CancellationToken cancellationToken = default);
}

