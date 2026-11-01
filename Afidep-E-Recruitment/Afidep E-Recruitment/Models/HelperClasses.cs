using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Afidep_E_Recruitment.Models
{
    public static class HelperClasses
    {
        public class JsonResultResponse
        {
            public bool status { get; set; }
            public string message { get; set; }
            public string requestno { get; set; }

        }
        public class RequestFilters
        {
            public string userid { get; set; }
            public string requestid { get; set; }
            public string applicationnumber { get; set; }
            public string jobid { get; set; }
        }

        public class Country
        {
            public string code { get; set; }
            public string name { get; set; }
        }


        private static string GetDaySuffix(int day)
        {
            if (day >= 11 && day <= 13)
            {
                return "th";
            }
            switch (day % 10)
            {
                case 1: return "st";
                case 2: return "nd";
                case 3: return "rd";
                default: return "th";
            }
        }
    }

    public class LRClasses
    {
        public class RegistrationUserClass
        {
            public string first_name { get; set; }
            public string middle_name { get; set; }
            public string last_name { get; set; }
            public string id_number { get; set; }
            public string phone_number { get; set; }
            public string email_address { get; set; }
        }
    }
}