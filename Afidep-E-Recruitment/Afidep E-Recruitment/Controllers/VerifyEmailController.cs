using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Web;
using System.Web.Mvc;
using static Afidep_E_Recruitment.Models.LRClasses;

namespace Afidep_E_Recruitment.Controllers
{
    public class VerifyEmailController : Controller
    {
        // GET: VerifyEmail
        public ActionResult Index(string Token)
        {
            if (Token != null)
            {
                UserService usersrv = new UserService();
                VerifyEmailClass verificationclass = new VerifyEmailClass();

                verificationclass = usersrv.VerifyEmailToken(Token, "email_verification");

                if (verificationclass.email != null && verificationclass.status == true)
                {
                    Session["userid"] = verificationclass.email;
                    return RedirectToAction("Success");
                }


                TempData["ErrorMessage"] = "Verification token has expired. Kindly try again.";
                return RedirectToAction("Index", "Error");
            }
            TempData["ErrorMessage"] = "";
            return RedirectToAction("Index", "Error");
        }

        public ActionResult Success()
        {
            if (Session["userid"] != null)
            {
                RegistrationUserClass pagemodel = new RegistrationUserClass();
                pagemodel.id_number = Session["userid"].ToString();
                return View(pagemodel);
            }

            return RedirectToAction("Error");

        }

    }

}