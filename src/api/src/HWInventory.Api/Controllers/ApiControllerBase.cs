using HWInventory.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HWInventory.Api.Controllers;

[ApiController]
[Authorize]
[Produces("application/json")]
[Route("api/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ApiControllerBase(IAppDbContext dbContext)
    {
        DbContext = dbContext;
    }

    protected IAppDbContext DbContext { get; }
}
