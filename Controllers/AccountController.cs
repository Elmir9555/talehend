﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Threading.Tasks;
using OganiProject.Entities;
using OganiProject.Models;
using OganiProject.ViewModels.Accaunt;
using MailKit.Security;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MimeKit;
using MimeKit.Text;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace OganiProject.Controllers
{
    public class AccountController : Controller
    {
        readonly UserManager<AppUser> _userManager;
        readonly SignInManager<AppUser> _signInManager;
        readonly RoleManager<AppRole> _roleManager;
        readonly IWebHostEnvironment _env;


        public AccountController(RoleManager<AppRole> roleManager, SignInManager<AppUser> singInManager, UserManager<AppUser> userManager, IWebHostEnvironment env)
        {
            _roleManager = roleManager;
            _signInManager = singInManager;
            _userManager = userManager;
            _env = env;
        }


        public IActionResult SignUp()
        {
            return View(new UserCreatedModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignUp(UserCreatedModel model)
        {
            if (ModelState.IsValid)
            {
                var user = new AppUser()
                {
                    UserName = model.Username,
                    Email = model.Email
                };

                var identity = await _userManager.CreateAsync(user, model.Password);

                if (identity.Succeeded)
                {
                    await _roleManager.CreateAsync(new AppRole
                    {
                        Name = "Member"
                    });

                    await _userManager.AddToRoleAsync(user, "Member");


                    AppUser appUser = await _userManager.FindByEmailAsync(model.Email);

                    if (appUser == null)
                        return View();

                    var message = new MimeMessage();

                    message.From.Add(new MailboxAddress("Ogani", "elmirustayev9@gmail.com"));

                    message.To.Add(new MailboxAddress(appUser.UserName, appUser.Email));

                    message.Subject = "Confirm Email";

                    string emailbody = string.Empty;

                    using (StreamReader streamReader = new StreamReader(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/Templates", "Confirm.html")))
                    {
                        emailbody = streamReader.ReadToEnd();
                    }


                    var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                    var url = Url.Action(nameof(VerifyEmail), "Account", new { userId = user.Id, token = code }, Request.Scheme, Request.Host.ToString());


                    emailbody = emailbody.Replace("{{username}}", $"{appUser.UserName}").Replace("{{code}}", $"{url}");

                    message.Body = new TextPart(TextFormat.Html) { Text = emailbody };

                    using var smtp = new SmtpClient();

                    smtp.Connect("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
                    smtp.Authenticate("elmirustayev9@gmail.com", "iibetythnxwkhwli");
                    smtp.Send(message);
                    smtp.Disconnect(true);


                    return RedirectToAction("SignUpEmail", "Account");
                }
                foreach (var error in identity.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

            }
            return View(model);
        }

        public async Task<IActionResult> VerifyEmail(string userId, string token)
        {
            if (userId == null || token == null) return BadRequest();

            AppUser user = await _userManager.FindByIdAsync(userId);

            if (user is null) return BadRequest();


            await _userManager.ConfirmEmailAsync(user, token);

            await _signInManager.SignInAsync(user, false);

            return RedirectToAction("HomePage", "Home");
        }

        public IActionResult SignUpEmail()
        {
            return View();
        }


        public IActionResult Login()
        {
            return View(new UserLoginModel());
        }
        [HttpPost]
        public async Task<IActionResult> Login(UserLoginModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByNameAsync(model.Username);

                var result = await _signInManager.PasswordSignInAsync(model.Username, model.Password, model.RememberMe, true);

                if (result.Succeeded)
                {
                    return RedirectToAction("HomePage", "Home");
                }
                else if (result.IsLockedOut)
                {
                    var logouttime = await _userManager.GetLockoutEndDateAsync(user);
                    var minute = (logouttime.Value.UtcDateTime - DateTime.UtcNow).Minutes;

                    ModelState.AddModelError("", $"Hesabiniz {minute} deqiqeliyine muveqqeti olaraq baglanmisdir.");
                }
                else
                {
                    string message = string.Empty;

                    if (user != null)
                    {
                        var failedcount = await _userManager.GetAccessFailedCountAsync(user);
                        var count = _userManager.Options.Lockout.MaxFailedAccessAttempts - failedcount;

                        message = $"{count} defe de yalnis giris etseniz hesabiniz muveqqeti olarag baglanacaq.";
                    }

                    else if (user == null)
                    {
                        message = "Username ve password sehvdir.";
                    }

                    ModelState.AddModelError("", message);
                }
            }
            return View(model);
        }


        public async Task<IActionResult> LogOut()
        {
            await _signInManager.SignOutAsync();

            return RedirectToAction("Login");
        }

        public IActionResult AccessDenied()
        {
            return View();
        }







        #region ForgotPassword
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordVM forgotPasswordVM)
        {
            if (!ModelState.IsValid) return View(forgotPasswordVM);

            var user = await _userManager.FindByEmailAsync(forgotPasswordVM.Email);

            if (user is null)
            {
                ModelState.AddModelError("", "This email hasn't been registrated");
                return View(forgotPasswordVM);
            }

            var message = new MimeMessage();

            message.From.Add(new MailboxAddress("Ogani", "elmirustayev9@gmail.com"));

            message.To.Add(new MailboxAddress(user.UserName, user.Email));
            message.Subject = "Reset Password";

            string emailbody = string.Empty;

            using (StreamReader streamReader = new StreamReader(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/Templates", "Reset.html")))
            {
                emailbody = streamReader.ReadToEnd();
            }

            string forgotpasswordtoken = await _userManager.GeneratePasswordResetTokenAsync(user);
            string url = Url.Action(nameof(ResetPassword), "Account", new { email = user.Email, Id = user.Id, token = forgotpasswordtoken, }, Request.Scheme);

            emailbody = emailbody.Replace("{{fullname}}", $"{user.UserName}").Replace("{{code}}", $"{url}");

            message.Body = new TextPart(TextFormat.Html) { Text = emailbody };

            using var smtp = new SmtpClient();

            smtp.Connect("smtp.gmail.com", 587, SecureSocketOptions.StartTls);
            smtp.Authenticate("elmirustayev9@gmail.com", "iibetythnxwkhwli");
            smtp.Send(message);
            smtp.Disconnect(true);

            return View();
        }
        #endregion


        #region Reset Password
        [HttpGet]
        public IActionResult ResetPassword(string email, string token)
        {
            var resetPasswordModel = new ResetPasswordVM { Email = email, Token = token };
            return View(resetPasswordModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordVM resetPasswordVM)
        {
            if (!ModelState.IsValid) return View(resetPasswordVM);

            var user = await _userManager.FindByEmailAsync(resetPasswordVM.Email);

            if (user is null) return NotFound();

            IdentityResult result = await _userManager.ResetPasswordAsync(user, resetPasswordVM.Token, resetPasswordVM.Password);

            if (!result.Succeeded)
            {
                foreach (var item in result.Errors)
                {
                    ModelState.AddModelError("", item.Description);
                }
                return View(resetPasswordVM);

            }


            return RedirectToAction(nameof(Login));

        }
        #endregion
    }
}
