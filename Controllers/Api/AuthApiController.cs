using AcxiomCRM.Dtos;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AcxiomCRM.Controllers.Api;

[ApiController, Route("api/auth"), IgnoreAntiforgeryToken]
public class AuthApiController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditService _audit;
    public AuthApiController(SignInManager<ApplicationUser> s, UserManager<ApplicationUser> u, IAuditService a) { _signIn = s; _users = u; _audit = a; }

    /// <summary>Authenticates and issues the secure session cookie. Rate limited (10/min per IP).</summary>
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginDto dto)
    {
        var user = await _users.FindByEmailAsync(dto.Email.Trim()) ?? await _users.FindByNameAsync(dto.Email.Trim());
        if (user == null || !user.IsActive)
        {
            await _audit.LogAsync("LoginFailed", "User", user?.Id, newValue: "API login failed", result: "Failure", userId: user?.Id, userName: dto.Email);
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Invalid credentials." });
        }
        var res = await _signIn.PasswordSignInAsync(user, dto.Password, false, lockoutOnFailure: true);
        if (res.IsLockedOut)
        {
            await _audit.LogAsync("AccountLockout", "User", user.Id, result: "Failure", userId: user.Id, userName: user.Email);
            return StatusCode(423, new ProblemDetails { Status = 423, Title = "Account locked. Try again later." });
        }
        if (!res.Succeeded)
        {
            await _audit.LogAsync("LoginFailed", "User", user.Id, newValue: "API login failed", result: "Failure", userId: user.Id, userName: user.Email);
            return Unauthorized(new ProblemDetails { Status = 401, Title = "Invalid credentials." });
        }
        await _audit.LogAsync("Login", "User", user.Id, newValue: "API login", userId: user.Id, userName: user.Email);
        var roles = await _users.GetRolesAsync(user);
        return Ok(new { user.Email, user.FullName, Role = roles.FirstOrDefault() });   // no hashes / tokens returned
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout()
    {
        await _audit.LogAsync("Logout", "User", null, newValue: "API logout");
        await _signIn.SignOutAsync();
        return NoContent();
    }
}
