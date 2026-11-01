using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;
using static Afidep_E_Recruitment.Models.LRClasses;

namespace Afidep_E_Recruitment.Controllers
{
    public class LoginController : Controller
    {
        // GET: Login
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LoginForm()
        {
            UserService usersrv = new UserService();
            UserService.LoginResult loginresult = new UserService.LoginResult();

            loginresult = usersrv.AuthenticateUser(Request.Form["idnumber"], Request.Form["password"]);

            switch (loginresult)
            {
                case UserService.LoginResult.Success:
                    {
                        Session["userid"] = Request.Form["idnumber"];
                        Session["loggedin"] = Guid.NewGuid().ToString();
                        TempData["SuccessMessage"] = "Logged in Successfully!";
                        return RedirectToAction("Index", "Home");
                    }
                case UserService.LoginResult.InvalidCredentials:
                    {
                        TempData["ErrorMessage"] = "The provided credentials are incorrect!";
                        return RedirectToAction("Index");
                    }
                case UserService.LoginResult.UserDoesnotExist:
                    {
                        TempData["ErrorMessage"] = "No account is associated with the provided credentials. Kindly register first!";
                        return RedirectToAction("Index");
                    }
                case UserService.LoginResult.EmailNotVerified:
                    {
                        TempData["ErrorMessage"] = "Please verify your email first to log in.";
                        return RedirectToAction("Index");
                    }
            }
            TempData["ErrorMessage"] = "Something went wrong. Please try again later";
            return RedirectToAction("Index", "Error");
        }

        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            Session.Abandon();
            return RedirectToAction("Index", "Login");
        }
    }
}