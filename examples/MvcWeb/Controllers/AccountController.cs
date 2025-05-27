using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Piranha.AspNetCore.Identity.Data;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using MvcWeb.Services;
using System.Diagnostics;

namespace MvcWeb.Controllers
{
    [Route("account")]
    public class AccountController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [HttpGet]
        [Route("login")]
        public IActionResult Login(string returnUrl = null)
        {
            using var activity = MetricsService.StartActivity("AccountController.Login.Get");
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                MetricsService.RecordPageView("login", "anonymous");
                MetricsService.RecordUserAction("login_page_visit", "authentication");
                
                activity?.SetTag("has_return_url", !string.IsNullOrEmpty(returnUrl));
                activity?.SetTag("outcome", "success");
                
                ViewData["ReturnUrl"] = returnUrl;
                
                stopwatch.Stop();
                MetricsService.RecordHttpRequest("GET", "/account/login", 200, stopwatch.ElapsedMilliseconds);
                
                return View();
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                MetricsService.RecordHttpRequest("GET", "/account/login", 500, stopwatch.ElapsedMilliseconds);
                MetricsService.RecordError("login_page_exception", "error", "AccountController");
                
                activity?.SetTag("outcome", "error");
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        [HttpPost]
        [Route("login")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string returnUrl = null)
        {
            using var activity = MetricsService.StartActivity("AccountController.Login.Post");
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                ViewData["ReturnUrl"] = returnUrl;
                
                // Record login attempt (NO PII - no username/email in metrics)
                MetricsService.RecordUserAction("login_attempt", "authentication");
                
                activity?.SetTag("has_return_url", !string.IsNullOrEmpty(returnUrl));
                activity?.SetTag("remember_me", model.RememberMe);
                activity?.SetTag("model_valid", ModelState.IsValid);
                
                if (ModelState.IsValid)
                {
                    // This doesn't count login failures towards account lockout
                    // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                    var result = await _signInManager.PasswordSignInAsync(model.Username, model.Password, model.RememberMe, lockoutOnFailure: false);
                    
                    if (result.Succeeded)
                    {
                        stopwatch.Stop();
                        
                        // Record successful authentication (NO username in metrics)
                        MetricsService.RecordAuthenticationAttempt(true, "password");
                        MetricsService.RecordUserAction("login_success", "authentication");
                        MetricsService.RecordHttpRequest("POST", "/account/login", 302, stopwatch.ElapsedMilliseconds);
                        
                        activity?.SetTag("outcome", "success");
                        activity?.SetTag("remember_me_used", model.RememberMe);
                        
                        // Log with username for application logs (not metrics)
                        _logger.LogInformation("User logged in successfully");
                        return RedirectToLocal(returnUrl);
                    }
                    
                    if (result.IsLockedOut)
                    {
                        stopwatch.Stop();
                        
                        // Record security event for lockout (NO username in metrics)
                        MetricsService.RecordAuthenticationAttempt(false, "password");
                        MetricsService.RecordSecurityEvent("account_locked_out", "high");
                        MetricsService.RecordHttpRequest("POST", "/account/login", 200, stopwatch.ElapsedMilliseconds);
                        
                        activity?.SetTag("outcome", "locked_out");
                        
                        // Log warning with username for application logs (not metrics)
                        _logger.LogWarning("Account locked out during login attempt");
                        ModelState.AddModelError(string.Empty, "Account locked out. Please try again later.");
                        return View(model);
                    }
                    
                    // Invalid credentials
                    stopwatch.Stop();
                    
                    MetricsService.RecordAuthenticationAttempt(false, "password");
                    MetricsService.RecordSecurityEvent("invalid_login_attempt", "medium");
                    MetricsService.RecordHttpRequest("POST", "/account/login", 200, stopwatch.ElapsedMilliseconds);
                    
                    activity?.SetTag("outcome", "invalid_credentials");
                    
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                    return View(model);
                }
                else
                {
                    // Model validation failed
                    stopwatch.Stop();
                    
                    MetricsService.RecordUserAction("login_validation_error", "authentication");
                    MetricsService.RecordHttpRequest("POST", "/account/login", 200, stopwatch.ElapsedMilliseconds);
                    
                    activity?.SetTag("outcome", "validation_error");
                }

                // If we got this far, something failed, redisplay form
                return View(model);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                MetricsService.RecordHttpRequest("POST", "/account/login", 500, stopwatch.ElapsedMilliseconds);
                MetricsService.RecordError("login_exception", "error", "AccountController");
                MetricsService.RecordSecurityEvent("login_system_error", "high");
                
                activity?.SetTag("outcome", "error");
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        [HttpPost]
        [Route("logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            using var activity = MetricsService.StartActivity("AccountController.Logout");
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                var wasAuthenticated = User.Identity?.IsAuthenticated ?? false;
                
                MetricsService.RecordUserAction("logout", "authentication");
                
                activity?.SetTag("user_was_authenticated", wasAuthenticated);
                
                await _signInManager.SignOutAsync();
                
                stopwatch.Stop();
                MetricsService.RecordHttpRequest("POST", "/account/logout", 302, stopwatch.ElapsedMilliseconds);
                
                // Record successful logout (no user identification in metrics)
                if (wasAuthenticated)
                {
                    MetricsService.RecordUserAction("logout_success", "authentication");
                }
                
                activity?.SetTag("outcome", "success");
                
                // Log for application logs (not metrics)
                _logger.LogInformation("User logged out");
                return RedirectToAction(nameof(Login));
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                MetricsService.RecordHttpRequest("POST", "/account/logout", 500, stopwatch.ElapsedMilliseconds);
                MetricsService.RecordError("logout_exception", "error", "AccountController");
                
                activity?.SetTag("outcome", "error");
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                throw;
            }
        }

        private IActionResult RedirectToLocal(string returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            else
            {
                return RedirectToAction("Index", "Cms");
            }
        }
    }

    public class LoginViewModel
    {
        [Required]
        public string Username { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }
}