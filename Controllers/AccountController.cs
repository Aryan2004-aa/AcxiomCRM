using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AcxiomCRM.Controllers;

[AllowAnonymous]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly UserManager<ApplicationUser> _users;
    private readonly IAuditService _audit;

    public AccountController(SignInManager<ApplicationUser> signIn, UserManager<ApplicationUser> users, IAuditService audit)
    { _signIn = signIn; _users = users; _audit = audit; }

    [HttpGet]
    public IActionResult Login(string returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Home");
        ViewBag.ReturnUrl = returnUrl;
        return View(new LoginVm());
    }

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginVm vm, string returnUrl = null)
    {
        ViewBag.ReturnUrl = returnUrl;
        if (!ModelState.IsValid) return View(vm);

        var input = vm.Email.Trim();
        var user = await _users.FindByEmailAsync(input) ?? await _users.FindByNameAsync(input);
        const string generic = "Invalid login attempt.";

        if (user == null)
        {
            await _audit.LogAsync("LoginFailed", "User", null, newValue: "Unknown account", result: "Failure", userName: input);
            ModelState.AddModelError("", generic);
            return View(vm);
        }
        if (!user.IsActive)
        {
            await _audit.LogAsync("LoginFailed", "User", user.Id, newValue: "Inactive account", result: "Failure", userId: user.Id, userName: user.Email);
            ModelState.AddModelError("", "This account has been deactivated. Contact an administrator.");
            return View(vm);
        }

        var res = await _signIn.PasswordSignInAsync(user, vm.Password, vm.RememberMe, lockoutOnFailure: true);
        if (res.Succeeded)
        {
            await _audit.LogAsync("Login", "User", user.Id, userId: user.Id, userName: user.Email);
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return LocalRedirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }
        if (res.IsLockedOut)
        {
            await _audit.LogAsync("AccountLockout", "User", user.Id, newValue: "Locked after repeated failed logins", result: "Failure", userId: user.Id, userName: user.Email);
            ModelState.AddModelError("", "Account locked after too many failed attempts. Try again in 15 minutes or contact an administrator.");
            return View(vm);
        }
        await _audit.LogAsync("LoginFailed", "User", user.Id, newValue: "Invalid password", result: "Failure", userId: user.Id, userName: user.Email);
        ModelState.AddModelError("", generic);
        return View(vm);
    }

    [HttpGet]
    public IActionResult Register() => User.Identity?.IsAuthenticated == true ? RedirectToAction("Index", "Home") : View(new RegisterVm());

    [HttpPost, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterVm vm)
    {
        if (!ModelState.IsValid) return View(vm);
        var user = new ApplicationUser { UserName = vm.Email.Trim(), Email = vm.Email.Trim(), FullName = vm.FullName.Trim(), EmailConfirmed = true };
        var res = await _users.CreateAsync(user, vm.Password);   // Identity applies the password policy + hashing
        if (!res.Succeeded)
        {
            foreach (var e in res.Errors) ModelState.AddModelError("", e.Description);
            return View(vm);
        }
        // Self-registration always receives the lowest-privilege role.
        await _users.AddToRoleAsync(user, Roles.SalesExecutive);
        await _audit.LogAsync("Register", "User", user.Id, newValue: $"{{\"Email\":\"{user.Email}\",\"Role\":\"{Roles.SalesExecutive}\"}}", userId: user.Id, userName: user.Email);
        await _signIn.SignInAsync(user, isPersistent: false);
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _audit.LogAsync("Logout", "User", null);
        await _signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    public IActionResult AccessDenied() => View();
}
