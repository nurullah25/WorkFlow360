using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WorkFlow360.Application.Common;
using WorkFlow360.Application.Users;
using WorkFlow360.Domain.Entities;

namespace WorkFlow360.API.Controllers;

[ApiController]
[Route("api/users")]
[Authorize(Roles = RoleNames.Admin)]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> List([FromQuery] UserQuery query, CancellationToken cancellationToken)
    {
        return Ok(await _userService.ListAsync(query, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<UserListItemDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await _userService.CreateAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, user);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserListItemDto>> Update(int id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        return Ok(await _userService.UpdateAsync(id, request, cancellationToken));
    }

    [HttpPut("{id:int}/password")]
    public async Task<IActionResult> ResetPassword(int id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        await _userService.ResetPasswordAsync(id, request, cancellationToken);
        return NoContent();
    }
}
