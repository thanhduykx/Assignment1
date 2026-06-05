using System.Security.Claims;
using DataAccessLayer;
using DataAccessLayer.Entities;
using DataAccessLayer.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PresentationLayer.Models;
using PresentationLayer.Security;

namespace PresentationLayer.Controllers;

public sealed class AccountController : Controller
{
    private const string AccountProvisioningMessage = "Tài khoản được cấp bởi Nhà trường. Vui lòng liên hệ Nhà trường để xin cấp tài khoản.";

    private readonly IUserRepository _users;
    private readonly IAuthenticationSchemeProvider _schemes;

    public AccountController(IUserRepository users, IAuthenticationSchemeProvider schemes)
    {
        _users = users;
        _schemes = schemes;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToDefaultDashboard(User.FindFirstValue(ClaimTypes.Role));
        }

        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl,
            IsGoogleLoginEnabled = await IsGoogleLoginEnabledAsync()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            model.IsGoogleLoginEnabled = await IsGoogleLoginEnabledAsync();
            return View(model);
        }

        var user = await _users.GetByEmailAsync(model.Email, cancellationToken);
        if (user is null)
        {
            ModelState.AddModelError(string.Empty, AccountProvisioningMessage);
            model.IsGoogleLoginEnabled = await IsGoogleLoginEnabledAsync();
            return View(model);
        }

        if (!_users.VerifyPassword(user, model.Password))
        {
            ModelState.AddModelError(string.Empty, "The email or password is incorrect.");
            model.IsGoogleLoginEnabled = await IsGoogleLoginEnabledAsync();
            return View(model);
        }

        await SignInAsync(user);
        return RedirectAfterSignIn(user, model.ReturnUrl);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Register()
    {
        TempData["AuthError"] = AccountProvisioningMessage;
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public IActionResult Register(RegisterViewModel model, CancellationToken cancellationToken)
    {
        TempData["AuthError"] = AccountProvisioningMessage;
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLogin(string? returnUrl = null)
    {
        if (!await IsGoogleLoginEnabledAsync())
        {
            TempData["AuthError"] = "Google sign-in is not configured.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(GoogleCallback), new { returnUrl })
        };

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleCallback(string? returnUrl = null, CancellationToken cancellationToken = default)
    {
        var result = await HttpContext.AuthenticateAsync("External");
        if (!result.Succeeded || result.Principal is null)
        {
            TempData["AuthError"] = "Google sign-in failed.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var email = result.Principal.FindFirstValue(ClaimTypes.Email);
        if (string.IsNullOrWhiteSpace(email))
        {
            TempData["AuthError"] = "Google did not return an email address for this account.";
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        var user = await _users.GetByEmailAsync(email, cancellationToken);
        if (user is null)
        {
            TempData["AuthError"] = AccountProvisioningMessage;
            return RedirectToAction(nameof(Login), new { returnUrl });
        }

        await SignInAsync(user);
        await HttpContext.SignOutAsync("External");
        return RedirectAfterSignIn(user, returnUrl);
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return RedirectToAction("Login", "Account");
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return RedirectToAction("Login", "Account");
        }

        var model = new ProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email,
            Role = AppRoles.Normalize(user.Role)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Profile(ProfileViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userIdString) || !Guid.TryParse(userIdString, out var userId))
        {
            return RedirectToAction("Login", "Account");
        }

        var user = await _users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
        {
            return RedirectToAction("Login", "Account");
        }

        // Update FullName only
        await _users.UpdateFullNameAsync(userId, model.FullName, cancellationToken);

        // Re-sign in to refresh the claims (so the new name appears in the UI)
        user.FullName = model.FullName;
        await SignInAsync(user);

        TempData["ProfileSuccess"] = "Profile updated successfully.";
        return RedirectToAction("Profile");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Account");
    }

    private async Task SignInAsync(KnowledgeSqlUser user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.FullName),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, AppRoles.Normalize(user.Role))
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            });
    }

    private IActionResult RedirectAfterSignIn(KnowledgeSqlUser user, string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl)
            && Url.IsLocalUrl(returnUrl)
            && CanAccessReturnUrl(AppRoles.Normalize(user.Role), returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToDefaultDashboard(user.Role);
    }

    private IActionResult RedirectToDefaultDashboard(string? role)
    {
        return AppRoles.Normalize(role) switch
        {
            AppRoles.Admin => RedirectToAction("Index", "Research"),
            AppRoles.Lecturer => RedirectToAction("Index", "Home"),
            _ => RedirectToAction("Chat", "Home")
        };
    }

    private static bool CanAccessReturnUrl(string role, string returnUrl)
    {
        if (role == AppRoles.Admin)
        {
            return true;
        }

        var path = returnUrl.Split('?', '#')[0].TrimEnd('/');
        if (string.IsNullOrWhiteSpace(path))
        {
            path = "/";
        }

        if (role == AppRoles.Lecturer)
        {
            return !path.StartsWith("/Research", StringComparison.OrdinalIgnoreCase)
                   && !path.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase);
        }

        return path.Equals("/Home/Chat", StringComparison.OrdinalIgnoreCase)
               || path.StartsWith("/Home/Chat/", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<bool> IsGoogleLoginEnabledAsync()
    {
        return await _schemes.GetSchemeAsync(GoogleDefaults.AuthenticationScheme) is not null;
    }
}