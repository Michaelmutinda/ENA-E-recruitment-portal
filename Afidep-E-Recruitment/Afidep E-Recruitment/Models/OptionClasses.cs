using Antlr.Runtime.Misc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Afidep_E_Recruitment.Models
{
    public class OptionClasses
    {
        public List<SelectListItem> GenderOptions { get; set; } = new List<SelectListItem>
            {
                new SelectListItem { Text = "Male", Value = "Male" },
                new SelectListItem { Text = "Female", Value = "Female" },
                new SelectListItem { Text = "Other", Value = "Other" }
            };

        public List<SelectListItem> YesorNo { get; set; } = new List<SelectListItem>
            {
                new SelectListItem { Text = "Yes", Value = "Yes" },
                new SelectListItem { Text = "No", Value = "No" },
            };

        public List<SelectListItem> TrueorFalse { get; set; } = new List<SelectListItem>
            {
                new SelectListItem { Text = "True", Value = "true" },
                new SelectListItem { Text = "False", Value = "false" },
            };


        public List<SelectListItem> EducationTypes { get; set; } = new List<SelectListItem>
            {
                new SelectListItem { Text = "Primary", Value = "Primary" },
                new SelectListItem { Text = "High School", Value = "High School" },
                new SelectListItem { Text = "Professional", Value = "Professional" },
                new SelectListItem { Text = "Vocational", Value = "Vocational" },
                new SelectListItem { Text = "College", Value = "College" },
                new SelectListItem { Text = "University", Value = "University" },
            };


        public List<SelectListItem> EducationLevels { get; set; } = new List<SelectListItem>
            {
                new SelectListItem { Text = "Certificate", Value = "Certificate" },
                new SelectListItem { Text = "Diploma", Value = "Diploma" },
                new SelectListItem { Text = "Higher/Post Graduate Diploma", Value = "Higher/Post Graduate Diploma" },
                new SelectListItem { Text = "Bachelors Degree", Value = "Bachelors Degree" },
                new SelectListItem { Text = "Masters Degree", Value = "Masters Degree" },
                new SelectListItem { Text = "Doctorate", Value = "Doctorate" },
            };

        public List<SelectListItem> HierarchyLevels { get; set; } = new List<SelectListItem>
            {
                new SelectListItem { Text = "Consultant", Value = "Consultant" },
                new SelectListItem { Text = "Executive/Director", Value = "Executive/Director" },
                new SelectListItem { Text = "First-Level Manager", Value = "First-Level Manager" },
                new SelectListItem { Text = "Intern", Value = "Intern" },
                new SelectListItem { Text = "Middle-Level Manager", Value = "Middle-Level Manager" },
                new SelectListItem { Text = "Supervisor", Value = "Supervisor" },
                new SelectListItem { Text = "Support Staff", Value = "Support Staff" },
                new SelectListItem { Text = "Top-Level Manager", Value = "Top-Level Manager" },
                new SelectListItem { Text = "Trainee/Attachee", Value = "Trainee/Attachee" },
                new SelectListItem { Text = "Officer", Value = "Officer" },
            };

        public List<SelectListItem> DocumentTypes { get; set; } = new List<SelectListItem>
            {
                new SelectListItem { Text = "Curriculum Vitae", Value = "Curriculum Vitae" },
                new SelectListItem { Text = "Transcript", Value = "Transcript" },
                new SelectListItem { Text = "Cover Letter", Value = "Cover Letter" },
            };
    }
}