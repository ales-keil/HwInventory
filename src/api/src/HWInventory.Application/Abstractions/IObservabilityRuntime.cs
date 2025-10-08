using System.Threading;

namespace HWInventory.Application.Abstractions;

public interface IObservabilityRuntime
{
    Task<ObservabilityConfigurationModel> GetAsync(CancellationToken cancellationToken = default);
    void Invalidate();
}
