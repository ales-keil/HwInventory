namespace HWInventory.Application.Abstractions;

public interface ISmsGateway
{
    Task SendAsync(string destination, string message, CancellationToken cancellationToken = default);
}
