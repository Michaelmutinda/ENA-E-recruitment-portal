using System.Web.Mvc;
using Afidep_E_Recruitment.Models; // adjust namespace if different

namespace Afidep_E_Recruitment.Controllers
{
    public class AccountController : Controller
    {
        // GET: Account/EditProfile
        public ActionResult EditProfile()
        {
            // Example: Load current logged-in user from Session or database
            var model = new EditProfileViewModel
            {
                FullName = Session["MemberName"]?.ToString(),
                Email = Session["MemberEmail"]?.ToString()
            };

            return View(model);
        }

        // POST: Account/EditProfile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditProfile(EditProfileViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Save updated profile to DB (pseudo-code)
                // var user = db.Users.Find(Session["MemberId"]);
                // user.FullName = model.FullName;
                // user.Email = model.Email;
                // db.SaveChanges();

                Session["MemberName"] = model.FullName;
                Session["MemberEmail"] = model.Email;

                TempData["Success"] = "Profile updated successfully!";
                return RedirectToAction("EditProfile");
            }

            return View(model);
        }
    }
}
