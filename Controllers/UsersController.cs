using AcxiomCRM.Data;
using AcxiomCRM.Models;
using AcxiomCRM.Services;
using AcxiomCRM.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AcxiomCRM.Controllers;

[Authorize(Roles = "Admin")]
public class UsersController : BaseController
{
    private readonly UserManager<ApplicationUser> _um;
    public UsersController(ApplicationDbContext db, IAuditService audit, UserManager<ApplicationUser> um) : base(db, audit) { _um = um; }

    public async Task<IActionResult> Index(string q, int page = 1)
    {
        var query = Db.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) { q = q.Trim().ToLower(); query = query.Where(u => u.FullName.ToLower().Contains(q) || u.Email.ToLower().Contains(q)); }
        var paged = await PagedList<ApplicationUser>.CreateAsync(query.OrderBy(u => u.FullName), page, PageSize);
        var items = new List<UserListItem>();
        foreach (var u in paged.Items)
            items.Add(new UserListItem
            {
                Id = u.Id, FullName = u.FullName, Email = u.Email, IsActive = u.IsActive,
                Role = (await _um.GetRolesAsync(u)).FirstOrDefault(),
                IsLockedOut = await _um.IsLockedOutAsync(u)
            });
        ViewBag.Q = q; ViewBag.Page = paged.Page; ViewBag.TotalPages = paged.TotalPages;
        return View(items);
    }

    public IActionResult Create() { FillRoles(); return View(new UserFormVm { Role = Models.Roles.SalesExecutive }); }

    [HttpPost]
    public async Task<IActionResult> Create(UserFormVm vm)
    {
        if (string.IsNullOrEmpty(vm.Password)) ModelState.AddModelError(nameof(vm.Password), "Password is required.");
        if (!Models.Roles.All.Contains(vm.Role)) ModelState.AddModelError(nameof(vm.Role), "Select a valid role.");
        if (!ModelState.IsValid) { FillRoles(vm.Role); return View(vm); }

        var u = new ApplicationUser { UserName = vm.Email.Trim(), Email = vm.Email.Trim(), FullName = vm.FullName.Trim(), IsActive = vm.IsActive, EmailConfirmed = true };
        var res = await _um.CreateAsync(u, vm.Password);
        if (!res.Succeeded)
        {
            foreach (var e in res.Errors) ModelState.AddModelError("", e.Description);
            FillRoles(vm.Role); return View(vm);
        }
        await _um.AddToRoleAsync(u, vm.Role);
        await Audit.LogAsync("Create", "User", u.Id, newValue: $"{{\"Email\":\"{u.Email}\",\"Role\":\"{vm.Role}\",\"IsActive\":{vm.IsActive.ToString().ToLower()}}}");
        TempData["Success"] = "User created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var u = await _um.FindByIdAsync(id);
        if (u == null) return NotFound();
        var role = (await _um.GetRolesAsync(u)).FirstOrDefault();
        FillRoles(role);
        return View(new UserFormVm { Id = u.Id, FullName = u.FullName, Email = u.Email, Role = role, IsActive = u.IsActive });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(UserFormVm vm)
    {
        var u = await _um.FindByIdAsync(vm.Id);
        if (u == null) return NotFound();
        if (!Models.Roles.All.Contains(vm.Role)) ModelState.AddModelError(nameof(vm.Role), "Select a valid role.");
        var oldRole = (await _um.GetRolesAsync(u)).FirstOrDefault();
        if (u.Id == UserId && (vm.Role != oldRole || !vm.IsActive))
            ModelState.AddModelError("", "You cannot change your own role or deactivate your own account.");
        if (!ModelState.IsValid) { FillRoles(vm.Role); return View(vm); }

        var changedEmail = !string.Equals(u.Email, vm.Email.Trim(), StringComparison.OrdinalIgnoreCase);
        u.FullName = vm.FullName.Trim();
        if (changedEmail) { u.Email = vm.Email.Trim(); u.UserName = vm.Email.Trim(); }
        var wasActive = u.IsActive;
        u.IsActive = vm.IsActive;
        var res = await _um.UpdateAsync(u);
        if (!res.Succeeded)
        {
            foreach (var e in res.Errors) ModelState.AddModelError("", e.Description);
            FillRoles(vm.Role); return View(vm);
        }
        if (oldRole != vm.Role)
        {
            if (oldRole != null) await _um.RemoveFromRoleAsync(u, oldRole);
            await _um.AddToRoleAsync(u, vm.Role);
            await Audit.LogAsync("RoleChange", "User", u.Id, $"{{\"Role\":\"{oldRole}\"}}", $"{{\"Role\":\"{vm.Role}\"}}");
        }
        if (wasActive != u.IsActive)
        {
            await Audit.LogAsync(u.IsActive ? "UserActivated" : "UserDeactivated", "User", u.Id);
        }
        await _um.UpdateSecurityStampAsync(u);   // invalidates existing sessions so role/status changes apply promptly
        await Audit.LogAsync("Update", "User", u.Id, newValue: $"{{\"Email\":\"{u.Email}\",\"FullName\":\"{u.FullName}\"}}");
        TempData["Success"] = "User updated.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> ResetPassword(string id)
    {
        var u = await _um.FindByIdAsync(id);
        if (u == null) return NotFound();
        return View(new ResetPasswordVm { Id = u.Id, Email = u.Email });
    }

    [HttpPost]
    public async Task<IActionResult> ResetPassword(ResetPasswordVm vm)
    {
        var u = await _um.FindByIdAsync(vm.Id);
        if (u == null) return NotFound();
        vm.Email = u.Email;
        if (!ModelState.IsValid) return View(vm);
        var token = await _um.GeneratePasswordResetTokenAsync(u);
        var res = await _um.ResetPasswordAsync(u, token, vm.NewPassword);   // enforces password policy
        if (!res.Succeeded)
        {
            foreach (var e in res.Errors) ModelState.AddModelError("", e.Description);
            return View(vm);
        }
        await Audit.LogAsync("PasswordReset", "User", u.Id, newValue: "Password reset by administrator");   // never log the password
        TempData["Success"] = "Password reset.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> Unlock(string id)
    {
        var u = await _um.FindByIdAsync(id);
        if (u == null) return NotFound();
        await _um.SetLockoutEndDateAsync(u, null);
        await _um.ResetAccessFailedCountAsync(u);
        await Audit.LogAsync("AccountUnlock", "User", u.Id);
        TempData["Success"] = "Account unlocked.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var u = await _um.FindByIdAsync(id);
        if (u == null) return NotFound();
        if (u.Id == UserId) { TempData["Error"] = "You cannot deactivate your own account."; return RedirectToAction(nameof(Index)); }
        u.IsActive = !u.IsActive;
        await _um.UpdateAsync(u);
        await _um.UpdateSecurityStampAsync(u);
        await Audit.LogAsync(u.IsActive ? "UserActivated" : "UserDeactivated", "User", u.Id);
        TempData["Success"] = u.IsActive ? "User activated." : "User deactivated.";
        return RedirectToAction(nameof(Index));
    }

    [ActionName("Roles")]
    public async Task<IActionResult> RoleList()
    {
        var counts = new Dictionary<string, int>();
        foreach (var r in Models.Roles.All) counts[r] = (await _um.GetUsersInRoleAsync(r)).Count;
        return View(counts);
    }

    public IActionResult Permissions() => View();

    private void FillRoles(string selected = null) => ViewBag.Roles = new SelectList(Models.Roles.All, selected);
}
