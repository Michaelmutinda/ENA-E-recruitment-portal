using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static Afidep_E_Recruitment.Controllers.HomeController;

namespace Afidep_E_Recruitment.Models.Screening
{
    // Longlisting and shortlisting, rule-based (no AI):
    // 1. Longlist: an applicant who meets every must-have of the job (education, years of experience, required qualifications
    //    and memberships, required documents) - each check recorded with what was found.
    // 2. Score out of 100: education 20, experience 30 (relevant years count fully, other years half), professional
    //    qualifications / memberships 15, how well the documents and application match the job's keywords 35.
    // 3. Rank the longlisted by score; a tie is broken, in order, by: must-haves met, relevant years of experience, education
    //    level, professional qualifications and memberships held, keywords matched, earlier submission - and the factor that
    //    decided it is written on both applicants.
    // 4. Shortlist the top N (or N% of the longlisted, rounded up), each with the reason and score.
    public static class ScreeningEngine
    {
        public static readonly string[] EducationLevels =
            { "Certificate", "Diploma", "Higher/Post Graduate Diploma", "Bachelors Degree", "Masters Degree", "Doctorate" };

        public static readonly string[] DocumentTypes = { "Curriculum Vitae", "Transcript", "Cover Letter" };

        public const decimal EducationPoints = 20, ExperiencePoints = 30, ProfessionalPoints = 15, DocumentPoints = 35;

        // ---------- criteria from the job advert ----------

        private static readonly HashSet<string> StopWords = new HashSet<string>(
            ("about above across after again against along among and annual are around based being below between both bring " +
             "complete conduct develop draft during each ensure etc every from have having into its keep lead maintain manage monthly " +
             "other over prepare process provide quarterly relevant review should such support supporting than that the their them " +
             "then there these they this those through time track under upon well were what when where which while with within " +
             "would your foundation foundations other others including include includes across work works ensure time line lines " +
             "partner partners staff day-to-day requirements report document documents filed officer officers consultants represent frame").Split(' '), StringComparer.OrdinalIgnoreCase);

        private static readonly HashSet<string> GenericTitleWords = new HashSet<string>(
            "officer assistant manager senior junior intern internship lead head associate coordinator principal chief deputy".Split(' '),
            StringComparer.OrdinalIgnoreCase);

        public static ScreeningCriteria FromAdvert(AvailableJobs job)
        {
            var c = new ScreeningCriteria();
            if (job == null) return c;

            // the highest level the advert asks for
            var levels = (job.acadquals ?? new List<AcademicQualification>())
                .Select(a => DetectLevel((a.type ?? "") + " " + (a.description ?? ""))).Where(l => l >= 0).ToList();
            if (levels.Any()) c.MinEducationLevel = EducationLevels[levels.Max()];

            c.MinYearsExperience = (job.experience ?? new List<JobExperience>()).Select(e => e.minyears).DefaultIfEmpty(0).Max();

            foreach (var q in job.profquals ?? new List<ProfessionalQualification>())
            {
                var term = MatchTerm(q.description);
                if (string.IsNullOrEmpty(term)) continue;
                if (IsOptional(q.level) || IsOptional(q.description)) c.DesirableQualifications.Add(term);
                else c.RequiredQualifications.Add(term);
            }
            foreach (var m in job.profmembs ?? new List<ProfessionalMembership>())
            {
                var body = (m.profbody ?? "").Trim();
                if (body.Length == 0) continue;
                if (IsOptional(m.description)) c.DesirableQualifications.Add(body);
                else c.RequiredMemberships.Add(body);
            }
            c.RequiredDocuments.Add("Curriculum Vitae");

            var advertText = string.Join(" ", (job.jobresponsibilities ?? new List<JobResponsibility>()).Select(r => r.responsibility))
                             + " " + (job.mainobjective ?? "");
            c.Keywords = SignificantWords(advertText).Take(12).ToList();
            return c;
        }

        private static bool IsOptional(string s) =>
            Regex.IsMatch(s ?? "", @"prefer|advantage|desir|optional|asset|nice to have", RegexOptions.IgnoreCase);

        // "CPA (K) – Certified Public Accountant" -> "CPA (K)"; "Project management certification (PMP/PRINCE2)" -> "PMP|PRINCE2"
        public static string MatchTerm(string description)
        {
            var d = (description ?? "").Trim();
            if (d.Length == 0) return "";
            var paren = Regex.Match(d, @"\(([^)]*/[^)]*)\)");
            if (paren.Success)
                return string.Join("|", paren.Groups[1].Value.Split('/').Select(x => x.Trim()).Where(x => x.Length > 0));
            var head = Regex.Split(d, @"\s+[–—-]\s+")[0];
            if (head.Contains("/")) return string.Join("|", head.Split('/').Select(x => x.Trim()).Where(x => x.Length > 0));
            head = Regex.Replace(head, @"\s*\((?!K\))[^)]*\)\s*$", "", RegexOptions.IgnoreCase).Trim();   // keep "(K)", drop other trailing notes
            return head;
        }

        public static IEnumerable<string> SignificantWords(string text)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match m in Regex.Matches(text ?? "", @"[A-Za-z][A-Za-z0-9+#]{4,}"))
            {
                var w = m.Value.ToLowerInvariant();
                if (StopWords.Contains(w) || !seen.Add(w)) continue;
                yield return w;
            }
        }

        // ---------- text matching ----------

        public static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return " ";
            var lower = s.ToLowerInvariant().Replace("’", "'").Replace("'s", "s");
            var sb = new StringBuilder(lower.Length + 2).Append(' ');
            var space = true;
            foreach (var ch in lower)
            {
                var keep = char.IsLetterOrDigit(ch) || ch == '+' || ch == '#';
                if (keep) { sb.Append(ch); space = false; }
                else if (!space) { sb.Append(' '); space = true; }
            }
            if (!space) sb.Append(' ');
            return sb.ToString();
        }

        // a term (alternatives separated by "|") found as whole words in normalized text; a final plural "s"/"es" allowed
        public static bool Contains(string normalizedText, string term)
        {
            foreach (var alt in (term ?? "").Split('|'))
            {
                var t = Normalize(alt).Trim();
                if (t.Length == 0) continue;
                var stem = t.EndsWith("es") && t.Length > 5 ? t.Substring(0, t.Length - 2) : t.EndsWith("s") && t.Length > 4 ? t.Substring(0, t.Length - 1) : t;
                if (Regex.IsMatch(normalizedText, " " + Regex.Escape(stem) + "(s|es)? ")) return true;
            }
            return false;
        }

        public static int DetectLevel(string text)
        {
            var t = Normalize(text);
            if (Regex.IsMatch(t, @" (phd|doctorate|doctor of) ")) return 5;
            if (Regex.IsMatch(t, @" (master|masters|msc|mba|llm|mph) ")) return 4;
            if (Regex.IsMatch(t, @" (bachelor|bachelors|bsc|bcom|ba|llb|bed|beng|degree|undergraduate) ")) return 3;
            if (Regex.IsMatch(t, @" (higher diploma|postgraduate diploma|post graduate diploma|pgd) ")) return 2;
            if (Regex.IsMatch(t, @" diploma ")) return 1;
            if (Regex.IsMatch(t, @" certificate ")) return 0;
            return -1;
        }

        public static int LevelIndex(string level) => Array.FindIndex(EducationLevels, l => string.Equals(l, level, StringComparison.OrdinalIgnoreCase));

        // ---------- experience ----------

        private static decimal Years(IEnumerable<ExperienceLine> lines)
        {
            // overlapping jobs counted once
            var spans = lines.Where(l => l.End > l.Start).OrderBy(l => l.Start).Select(l => new { l.Start, l.End }).ToList();
            double days = 0;
            DateTime? curStart = null, curEnd = null;
            foreach (var s in spans)
            {
                if (curEnd == null || s.Start > curEnd) { if (curEnd != null) days += (curEnd.Value - curStart.Value).TotalDays; curStart = s.Start; curEnd = s.End; }
                else if (s.End > curEnd) curEnd = s.End;
            }
            if (curEnd != null) days += (curEnd.Value - curStart.Value).TotalDays;
            return Math.Round((decimal)(days / 365.25), 1);
        }

        // ---------- screening ----------

        public static ScreeningOutcome Run(string jobTitle, ScreeningCriteria criteria, List<ApplicantFile> applicants, string shortlistMode, decimal? shortlistValue)
        {
            criteria = criteria ?? new ScreeningCriteria();
            var outcome = new ScreeningOutcome { ShortlistMode = shortlistMode, ShortlistValue = shortlistValue };
            var titleWords = SignificantWords(jobTitle).Where(w => !GenericTitleWords.Contains(w)).ToList();

            foreach (var a in applicants)
                outcome.Results.Add(Evaluate(a, criteria, titleWords));
            outcome.Considered = outcome.Results.Count;

            // rank the longlisted
            var longlisted = outcome.Results.Where(r => r.Longlisted).ToList();
            longlisted.Sort(Compare);
            for (var i = 0; i < longlisted.Count; i++) longlisted[i].Rank = i + 1;
            outcome.Longlisted = longlisted.Count;

            // tie notes between neighbours with the same score
            for (var i = 0; i + 1 < longlisted.Count; i++)
            {
                var above = longlisted[i];
                var below = longlisted[i + 1];
                if (above.Score != below.Score) continue;
                AddTieNote(above, $"Tied at {above.Score:0.0} with {below.ApplicantName}; placed above on {DecidingFactor(above, below, aboveView: true)}.");
                AddTieNote(below, $"Tied at {below.Score:0.0} with {above.ApplicantName}; placed below on {DecidingFactor(above, below, aboveView: false)}.");
            }

            // shortlist
            var take = 0;
            if (shortlistMode == "Number" && shortlistValue.HasValue) take = (int)Math.Floor(shortlistValue.Value);
            else if (shortlistMode == "Percent" && shortlistValue.HasValue) take = (int)Math.Ceiling(longlisted.Count * shortlistValue.Value / 100m);
            take = Math.Max(0, Math.Min(take, longlisted.Count));
            var shortlisting = shortlistMode == "Number" || shortlistMode == "Percent";

            for (var i = 0; i < longlisted.Count; i++)
            {
                var r = longlisted[i];
                r.Shortlisted = shortlisting && i < take;
                if (!shortlisting) { r.Reason = "Longlisted (rank " + r.Rank + " of " + longlisted.Count + ", score " + r.Score.ToString("0.0") + "/100): " + Strengths(r); continue; }
                if (r.Shortlisted)
                {
                    r.Reason = "Shortlisted - rank " + r.Rank + " of " + longlisted.Count + ", score " + r.Score.ToString("0.0") + "/100: " + Strengths(r);
                }
                else
                {
                    var last = take > 0 ? longlisted[take - 1] : null;
                    r.Reason = "Not shortlisted - rank " + r.Rank + " of " + longlisted.Count + ", score " + r.Score.ToString("0.0") + "/100; " +
                               (last == null ? "no places on the shortlist." :
                                   "the shortlist takes " + take + (take == 1 ? " applicant" : " applicants") + " (last place scored " + last.Score.ToString("0.0") + "). ") +
                               (last != null && last.Score == r.Score ? "Tied with " + last.ApplicantName + " at the cut-off; " + "placed below on " + DecidingFactor(last, r, aboveView: false) + ". " : "") +
                               "Strengths: " + Strengths(r);
                }
            }
            foreach (var r in outcome.Results.Where(x => !x.Longlisted))
                r.Reason = "Not longlisted - does not meet: " + string.Join("; ", r.Checks.Where(c => !c.Met).Select(c => c.Criterion + " (" + c.Detail + ")")) + ".";

            outcome.Shortlisted = longlisted.Count(r => r.Shortlisted);
            // longlisted first in rank order, then the rest by name
            outcome.Results = longlisted.Concat(outcome.Results.Where(r => !r.Longlisted).OrderBy(r => r.ApplicantName)).ToList();
            return outcome;
        }

        private static void AddTieNote(ScreeningResult r, string note) => r.TieNote = string.IsNullOrEmpty(r.TieNote) ? note : r.TieNote + " " + note;

        private static ScreeningResult Evaluate(ApplicantFile a, ScreeningCriteria c, List<string> titleWords)
        {
            var r = new ScreeningResult { ApplicationNo = a.ApplicationNo, ApplicantId = a.ApplicantId, ApplicantName = a.Name };
            var docs = a.Documents ?? new List<ApplicantDocument>();
            r.DocumentsUploaded = docs.Count;
            r.DocumentsRead = docs.Count(d => d.Text != null);
            r.DocumentProblems = docs.Where(d => d.Text == null).Select(d => d.Type + ": " + (d.ReadProblem ?? "could not be read")).ToList();

            var docText = Normalize(string.Join(" \n ", docs.Where(d => d.Text != null).Select(d => d.Text)));
            var formQualText = Normalize(string.Join(" \n ", a.ProfessionalQualifications.Concat(new[] { a.EducationText })));
            var formMembText = Normalize(string.Join(" \n ", a.Memberships));
            var allText = Normalize(string.Join(" \n ", new[] { docText, formQualText, formMembText, a.EducationText }
                .Concat(a.Experience.Select(e => e.Text))));

            // education: the form's highest level, else what the documents say
            var formLevel = a.EducationLevels.Select(LevelIndex).DefaultIfEmpty(-1).Max();
            var level = formLevel;
            var levelSource = "application";
            if (level < 0) { level = DetectLevel(docText); levelSource = "documents"; }
            var minLevel = LevelIndex(c.MinEducationLevel);
            var levelName = level >= 0 ? EducationLevels[level] : "none found";
            if (minLevel >= 0)
                r.Checks.Add(new CheckResult { Criterion = "Education: " + c.MinEducationLevel + " or higher", Met = level >= minLevel,
                    Detail = levelName + (level >= 0 ? " (from the " + levelSource + ")" : "") });

            // experience
            var total = Years(a.Experience);
            var relevantLines = a.Experience.Where(e =>
            {
                var t = Normalize(e.Text);
                return titleWords.Any(w => Contains(t, w)) || c.Keywords.Any(k => Contains(t, k));
            }).ToList();
            var relevant = Years(relevantLines);
            if (c.MinYearsExperience > 0)
                r.Checks.Add(new CheckResult { Criterion = "Experience: " + c.MinYearsExperience.ToString("0.#") + " years", Met = total >= c.MinYearsExperience,
                    Detail = total.ToString("0.0") + " years (" + relevant.ToString("0.0") + " relevant)" });

            // required qualifications and memberships: in the application or the documents
            foreach (var q in c.RequiredQualifications.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var inForm = Contains(formQualText, q) || Contains(formMembText, q);
                var inDocs = Contains(docText, q);
                r.Checks.Add(new CheckResult { Criterion = "Qualification: " + q.Replace("|", " or "), Met = inForm || inDocs,
                    Detail = inForm ? "in the application" : inDocs ? "found in the documents" : "not found" });
            }
            foreach (var m in c.RequiredMemberships.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var inForm = Contains(formMembText, m);
                var inDocs = Contains(docText, m);
                r.Checks.Add(new CheckResult { Criterion = "Membership: " + m.Replace("|", " or "), Met = inForm || inDocs,
                    Detail = inForm ? "in the application" : inDocs ? "found in the documents" : "not found" });
            }
            foreach (var d in c.RequiredDocuments.Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                var doc = docs.FirstOrDefault(x => string.Equals(x.Type, d, StringComparison.OrdinalIgnoreCase));
                r.Checks.Add(new CheckResult { Criterion = "Document: " + d, Met = doc != null,
                    Detail = doc == null ? "not uploaded" : doc.Text == null ? "uploaded, could not be read" : "uploaded" });
            }
            r.Longlisted = r.Checks.All(x => x.Met);

            // ---- score ----
            var b = r.Breakdown;
            if (level < 0) { b.Education = 0; b.EducationNote = "no education level found"; }
            else if (minLevel >= 0)
            {
                b.Education = level < minLevel ? 0 : Math.Min(EducationPoints, 12 + 4 * (level - minLevel));
                b.EducationNote = levelName + (level > minLevel ? " (" + (level - minLevel) + " level" + (level - minLevel > 1 ? "s" : "") + " above the minimum)" : level == minLevel ? " (the minimum)" : " (below the minimum)");
            }
            else { b.Education = Math.Round(EducationPoints * (level + 1) / EducationLevels.Length, 1); b.EducationNote = levelName; }

            var min = c.MinYearsExperience;
            var target = Math.Max(2 * min, min + 4);
            var effective = relevant + 0.5m * (total - relevant);
            b.Experience = Math.Round(ExperiencePoints * Math.Min(1m, target == 0 ? 1m : effective / target), 1);
            b.ExperienceNote = total.ToString("0.0") + " years, " + relevant.ToString("0.0") + " relevant (full marks at " + target.ToString("0.#") + " relevant years)";

            var desirable = c.DesirableQualifications.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var held = a.ProfessionalQualifications.Count + a.Memberships.Count;
            if (desirable.Any())
            {
                var found = desirable.Where(q => Contains(allText, q)).ToList();
                b.Professional = Math.Round(ProfessionalPoints * found.Count / desirable.Count, 1);
                b.ProfessionalNote = found.Any() ? "has " + string.Join(", ", found.Select(x => x.Replace("|", "/"))) + " (" + found.Count + " of " + desirable.Count + " desirable)" : "none of the desirable qualifications (" + string.Join(", ", desirable.Select(x => x.Replace("|", "/"))) + ")";
            }
            else
            {
                b.Professional = Math.Min(ProfessionalPoints, 5 * held);
                b.ProfessionalNote = held + " professional qualification" + (held == 1 ? "" : "s") + " / membership" + (held == 1 ? "" : "s");
            }

            var keywords = c.Keywords.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var matched = keywords.Where(k => Contains(allText, k)).ToList();
            b.DocumentMatch = keywords.Count == 0 ? 0 : Math.Round(DocumentPoints * matched.Count / keywords.Count, 1);
            b.DocumentNote = matched.Count + " of " + keywords.Count + " job keywords" + (matched.Any() ? " (" + string.Join(", ", matched.Take(8)) + (matched.Count > 8 ? "…" : "") + ")" : "") +
                             (r.DocumentsRead == 0 ? " - no readable documents, application only" : "");

            r.Score = Math.Round(b.Education + b.Experience + b.Professional + b.DocumentMatch, 1);
            r.Factors = new TieFactors
            {
                MustHavesMet = r.Checks.Count(x => x.Met), RelevantYears = relevant, EducationRank = level, ProfessionalItems = held,
                KeywordsMatched = matched.Count, DateApplied = a.DateApplied,
            };
            return r;
        }

        private static string Strengths(ScreeningResult r)
        {
            var b = r.Breakdown;
            return "education " + b.Education.ToString("0.#") + "/20 (" + b.EducationNote + "); experience " + b.Experience.ToString("0.#") + "/30 (" + b.ExperienceNote +
                   "); professional " + b.Professional.ToString("0.#") + "/15 (" + b.ProfessionalNote + "); documents " + b.DocumentMatch.ToString("0.#") + "/35 (" + b.DocumentNote + ").";
        }

        // higher score first, then the tie-break factors in order
        private static int Compare(ScreeningResult x, ScreeningResult y)
        {
            var c = y.Score.CompareTo(x.Score);
            if (c != 0) return c;
            foreach (var f in Factors)
            {
                c = f.Compare(x.Factors, y.Factors);
                if (c != 0) return c;
            }
            return string.CompareOrdinal(x.ApplicationNo, y.ApplicationNo);
        }

        private class Factor
        {
            public string Name;
            public Func<TieFactors, TieFactors, int> Compare;            // < 0 when the first ranks above
            public Func<TieFactors, string> Show;
        }

        private static readonly Factor[] Factors =
        {
            new Factor { Name = "must-have requirements met", Compare = (a, b) => b.MustHavesMet.CompareTo(a.MustHavesMet), Show = f => f.MustHavesMet.ToString() },
            new Factor { Name = "relevant experience", Compare = (a, b) => b.RelevantYears.CompareTo(a.RelevantYears), Show = f => f.RelevantYears.ToString("0.0") + " years" },
            new Factor { Name = "education level", Compare = (a, b) => b.EducationRank.CompareTo(a.EducationRank), Show = f => f.EducationRank >= 0 ? EducationLevels[f.EducationRank] : "none" },
            new Factor { Name = "professional qualifications and memberships", Compare = (a, b) => b.ProfessionalItems.CompareTo(a.ProfessionalItems), Show = f => f.ProfessionalItems.ToString() },
            new Factor { Name = "job keywords matched", Compare = (a, b) => b.KeywordsMatched.CompareTo(a.KeywordsMatched), Show = f => f.KeywordsMatched.ToString() },
            new Factor { Name = "earlier submission", Compare = (a, b) => (a.DateApplied ?? DateTime.MaxValue).CompareTo(b.DateApplied ?? DateTime.MaxValue),
                Show = f => f.DateApplied.HasValue ? f.DateApplied.Value.ToString("d MMM yyyy HH:mm", CultureInfo.InvariantCulture) : "not recorded" },
        };

        // the first factor that separates two tied applicants, each applicant's own figure first
        private static string DecidingFactor(ScreeningResult above, ScreeningResult below, bool aboveView)
        {
            foreach (var f in Factors)
                if (f.Compare(above.Factors, below.Factors) != 0)
                    return aboveView
                        ? f.Name + " (" + f.Show(above.Factors) + " vs " + f.Show(below.Factors) + ")"
                        : f.Name + " (" + f.Show(below.Factors) + " vs " + f.Show(above.Factors) + ")";
            return "application number (identical on every factor: " + above.ApplicationNo + " before " + below.ApplicationNo + ")";
        }
    }
}
