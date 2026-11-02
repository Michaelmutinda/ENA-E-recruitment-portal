using System;
using System.Collections.Generic;

namespace Afidep_E_Recruitment.Models.Screening
{
    // What HR screens a job's applicants against. Pre-filled from the job advert (ScreeningCriteria.FromAdvert), editable by HR.
    public class ScreeningCriteria
    {
        // must-haves: an applicant who misses any of them is not longlisted
        public string MinEducationLevel { get; set; }               // one of ScreeningEngine.EducationLevels, or empty
        public decimal MinYearsExperience { get; set; }
        public List<string> RequiredQualifications { get; set; } = new List<string>();   // one per line; alternatives with "|", e.g. "CPA|ACCA"
        public List<string> RequiredMemberships { get; set; } = new List<string>();
        public List<string> RequiredDocuments { get; set; } = new List<string>();        // document types: Curriculum Vitae, Transcript, Cover Letter

        // scored: what makes one longlisted applicant stronger than another
        public List<string> DesirableQualifications { get; set; } = new List<string>();
        public List<string> Keywords { get; set; } = new List<string>();                 // looked for in the documents and the application
    }

    // An applicant's submitted application, as screening reads it
    public class ApplicantFile
    {
        public string ApplicationNo { get; set; }
        public string ApplicantId { get; set; }
        public string Name { get; set; }
        public string Status { get; set; }
        public DateTime? DateApplied { get; set; }
        public List<string> EducationLevels { get; set; } = new List<string>();
        public string EducationText { get; set; } = "";              // courses, fields of study, institutions
        public List<ExperienceLine> Experience { get; set; } = new List<ExperienceLine>();
        public List<string> ProfessionalQualifications { get; set; } = new List<string>();
        public List<string> Memberships { get; set; } = new List<string>();
        public List<ApplicantDocument> Documents { get; set; } = new List<ApplicantDocument>();
    }

    public class ExperienceLine
    {
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public string Text { get; set; } = "";                        // job title, employer, industry, responsibilities
        public string Title { get; set; } = "";
    }

    public class ApplicantDocument
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string FileName { get; set; }
        public string Text { get; set; }                              // null when the file could not be read
        public string ReadProblem { get; set; }
    }

    public class CheckResult
    {
        public string Criterion { get; set; }
        public bool Met { get; set; }
        public string Detail { get; set; }
    }

    public class ScoreBreakdown
    {
        public decimal Education { get; set; }
        public decimal Experience { get; set; }
        public decimal Professional { get; set; }
        public decimal DocumentMatch { get; set; }
        public string EducationNote { get; set; }
        public string ExperienceNote { get; set; }
        public string ProfessionalNote { get; set; }
        public string DocumentNote { get; set; }
    }

    // The tie-break factors, in order (after the score)
    public class TieFactors
    {
        public int MustHavesMet { get; set; }
        public decimal RelevantYears { get; set; }
        public int EducationRank { get; set; }
        public int ProfessionalItems { get; set; }
        public int KeywordsMatched { get; set; }
        public DateTime? DateApplied { get; set; }
    }

    public class ScreeningResult
    {
        public string ApplicationNo { get; set; }
        public string ApplicantId { get; set; }
        public string ApplicantName { get; set; }
        public bool Longlisted { get; set; }
        public List<CheckResult> Checks { get; set; } = new List<CheckResult>();
        public decimal Score { get; set; }
        public ScoreBreakdown Breakdown { get; set; } = new ScoreBreakdown();
        public TieFactors Factors { get; set; } = new TieFactors();
        public int? Rank { get; set; }                                 // among the longlisted
        public bool Shortlisted { get; set; }
        public string Reason { get; set; }
        public string TieNote { get; set; }
        public int DocumentsRead { get; set; }
        public int DocumentsUploaded { get; set; }
        public List<string> DocumentProblems { get; set; } = new List<string>();
    }

    public class ScreeningOutcome
    {
        public List<ScreeningResult> Results { get; set; } = new List<ScreeningResult>();
        public int Considered { get; set; }
        public int Longlisted { get; set; }
        public int Shortlisted { get; set; }
        public string ShortlistMode { get; set; }                      // "Number" / "Percent" / null (longlist only)
        public decimal? ShortlistValue { get; set; }
    }
}
