using System.Threading;
using System.Threading.Tasks;

namespace HWInventory.Application.Abstractions;

public interface IUpdateConfigurationStore
{
    Task<UpdateDeploymentConfigurationModel> GetAsync(CancellationToken cancellationToken = default);
    Task<UpdateDeploymentConfigurationModel> SaveAsync(UpdateDeploymentConfigurationUpdate update, CancellationToken cancellationToken = default);
}

public record UpdateDeploymentConfigurationModel(
    string DeploymentRootPath,
    string? WebRootPath,
    bool UseAppOfflineFile,
    bool RunMigrations,
    string? PostDeploymentScript);

public record UpdateDeploymentConfigurationUpdate(
    string DeploymentRootPath,
    string? WebRootPath,
    bool UseAppOfflineFile,
    bool RunMigrations,
    string? PostDeploymentScript);
