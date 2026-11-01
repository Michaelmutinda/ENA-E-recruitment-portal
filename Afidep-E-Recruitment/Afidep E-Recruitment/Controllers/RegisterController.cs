using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using static Afidep_E_Recruitment.Models.LRClasses;

namespace Afidep_E_Recruitment.Controllers
{
    public class RegisterController : Controller
    {
        // GET: Register
        public ActionResult Index()
        {
            return View();
        }
        public ActionResult SetNewPassword(string userid)
        {
            RegistrationUserClass pagemodel = new RegistrationUserClass();
            if (userid == null)
            {
                TempData["ErrorMessage"] = "Kindly register first to set a new password";
                return RedirectToAction("Index", "Error");
            }

            pagemodel.id_number = userid;
            return View(pagemodel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RegisterForm()
        {
            UserService usersrv = new UserService();
            RegistrationUserClass registrationclass = new RegistrationUserClass();
            UserService.RegisterResult registerresult = new UserService.RegisterResult();

            registrationclass.first_name = Request.Form["firstname"];
            registrationclass.middle_name = Request.Form["middlename"];
            registrationclass.last_name = Request.Form["lastname"];
            registrationclass.email_address = Request.Form["email"];
            registrationclass.phone_number = $"{Request.Form["countryCode"]}{Request.Form["telephone"]}";
            registrationclass.id_number = Request.Form["idnumber"];

            registerresult = usersrv.RegisterUser(registrationclass, this.Url);

            switch (registerresult)
            {
                case UserService.RegisterResult.EmailSent:
                    {
                        TempData["SuccessMessage"] = "Kindly click the link sent to your email to verify your email and set up your new password.";
                        return View("Index");
                    }
                case UserService.RegisterResult.EmailNotSent:
                    {
                        TempData["ErrorMessage"] = "Your request could not be processed at the moment. Kindly try again later.";
                        return View("Index");
                    }
                case UserService.RegisterResult.IDNumberexists:
                    {
                        TempData["ErrorMessage"] = "The details provided exist. Kindly login in or reset your password";
                        return View("Index");
                    }
                case UserService.RegisterResult.PhoneNumberExists:
                    {
                        TempData["ErrorMessage"] = "The details provided exist. Kindly login in or reset your password";
                        return View("Index");
                    }
                case UserService.RegisterResult.EmailExists:
                    {
                        TempData["ErrorMessage"] = "The details provided exist. Kindly login in or reset your password";
                        return View("Index");
                    }
            }

            TempData["ErrorMessage"] = "Your request could not be processed at the moment. Kindly try again later.";
            return View("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetNewPasswordForm()
        {
            if (Request.Form["password"] !=
                Request.Form["npassword"])
            {
                TempData["ErrorMessage"] =
                    "The passwords do not match.";

                return RedirectToAction(
                    "SetNewPassword",
                    new { userid = Request.Form["username"] });
            }

            string userid = Request.Form["username"];

            // 🔥 NULL GUARD
            if (string.IsNullOrEmpty(userid))
            {
                TempData["ErrorMessage"] =
                    "Invalid password reset request.";

                return RedirectToAction("Index", "Error");
            }

            UserService usersrv = new UserService();
            RegistrationUserClass registrationclass =
                new RegistrationUserClass();

            // 🔥 Try ID first
            var userdetails =
                usersrv.GetUserDetails(userid, "idnumber");

            // 🔥 Fallback to Email
            if (userdetails == null ||
                string.IsNullOrEmpty(userdetails.email))
            {
                userdetails =
                    usersrv.GetUserDetails(userid, "email");
            }

            if (userdetails == null ||
                string.IsNullOrEmpty(userdetails.email))
            {
                TempData["ErrorMessage"] =
                    "Unable to resolve user email.";

                return RedirectToAction("Index", "Error");
            }

            // ✅ Set resolved email
            registrationclass.email_address =
                userdetails.email;

            var registerresult =
                usersrv.SetUserPassword(
                    registrationclass,
                    Request.Form["password"]);

            if (registerresult ==
                UserService.RegisterResult.PasswordSet)
            {
                Session["userid"] = userid;
                Session["loggedin"] =
                    Guid.NewGuid().ToString();

                TempData["SuccessMessage"] =
                    "Password set successfully!";

                return RedirectToAction("Index", "Home");
            }

            return RedirectToAction("Index");
        }

    }
}
