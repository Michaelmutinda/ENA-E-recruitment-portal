using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using static Afidep_E_Recruitment.Models.LRClasses;

namespace Afidep_E_Recruitment.Controllers
{
    public class ResetPasswordController : Controller
    {
        // GET: ResetPassword
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPasswordForm()
        {
            UserService usersrv = new UserService();
            UserService.ResetResult resetresult = new UserService.ResetResult();

            resetresult = usersrv.ResetUserPassword(Request.Form["email"], this.Url);

            switch (resetresult)
            {
                case UserService.ResetResult.EmailSent:
                    {
                        TempData["SuccessMessage"] = "Kindly click the link sent to your email to set your new password.";
                        return RedirectToAction("EmailSent");
                    }
                case UserService.ResetResult.EmailDoesNotExist:
                    {
                        TempData["ErrorMessage"] = "We do not have any records associated with the provided email address. Kindly create a new account";
                        return RedirectToAction("Index");
                    }
                case UserService.ResetResult.EmailNotSent:
                    {
                        TempData["ErrorMessage"] = "We could not process your request at the moment. Kindly try again later";
                        return RedirectToAction("Index");
                    }
            }
            TempData["ErrorMessage"] = "We could not process your request at the moment. Kindly try again later";
            return RedirectToAction("Index");
        }

        public ActionResult EmailSent()
        {
            return View();
        }

        public ActionResult SetNewPassword(string Token)
        {
            if (Token == null)
            {
                return RedirectToAction("Index", "Error");
            }
            UserService usersrv = new UserService();
            VerifyEmailClass verificationclass = new VerifyEmailClass();
            RegistrationUserClass pagemodel = new RegistrationUserClass();

            verificationclass = usersrv.VerifyEmailToken(Token, "password_verification");

            if (verificationclass.email != null && verificationclass.status == true)
            {
                pagemodel.id_number = verificationclass.email;
                return View(pagemodel);
            }

            TempData["ErrorMessage"] = "Verification token has expired. Kindly try again.";
            return RedirectToAction("Index", "Error");
        }
    }
}