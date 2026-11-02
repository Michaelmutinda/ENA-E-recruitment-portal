using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using UglyToad.PdfPig;

namespace Afidep_E_Recruitment.Models.Screening
{
    // The text of an uploaded document, for screening: PDF (text-based), Word .docx and plain text. Scanned images and old
    // .doc files cannot be read - the screening says so instead of guessing.
    public static class DocumentTextReader
    {
        public static string Read(string path, out string problem)
        {
            problem = null;
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) { problem = "file not found"; return null; }
                var ext = Path.GetExtension(path).ToLowerInvariant();
                string text;
                switch (ext)
                {
                    case ".pdf": text = ReadPdf(path); break;
                    case ".docx": text = ReadDocx(path); break;
                    case ".txt": text = File.ReadAllText(path); break;
                    default: problem = ext + " files cannot be read (upload PDF or Word .docx)"; return null;
                }
                if (string.IsNullOrWhiteSpace(text)) { problem = "no text in the file (a scanned image?)"; return null; }
                return text;
            }
            catch (Exception ex)
            {
                problem = "could not be read: " + ex.Message;
                return null;
            }
        }

        private static string ReadPdf(string path)
        {
            var sb = new StringBuilder();
            using (var doc = PdfDocument.Open(path))
                foreach (var page in doc.GetPages())
                    sb.AppendLine(string.Join(" ", page.GetWords().Select(w => w.Text)));
            return sb.ToString();
        }

        private static string ReadDocx(string path)
        {
            using (var zip = ZipFile.OpenRead(path))
            {
                var entry = zip.GetEntry("word/document.xml");
                if (entry == null) return null;
                XNamespace w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
                using (var s = entry.Open())
                {
                    var xml = XDocument.Load(s);
                    return string.Join("\n", xml.Descendants(w + "p").Select(p => string.Concat(p.Descendants(w + "t").Select(t => t.Value))));
                }
            }
        }
    }
}
