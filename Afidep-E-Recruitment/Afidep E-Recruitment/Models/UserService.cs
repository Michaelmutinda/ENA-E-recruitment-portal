using Afidep_E_Recruitment.Controllers;
using Afidep_E_Recruitment.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Net.Mail;
using System.Security.Policy;
using System.Text;
using System.Web.Mvc;
using static Afidep_E_Recruitment.Models.LRClasses;

public class UserService
{
    private string connectionString = ConfigurationManager.ConnectionStrings["DefaultConnection"].ConnectionString;
    private string clientemail = ConfigurationManager.AppSettings["ClientEmail"];
    private string clientsecret = ConfigurationManager.AppSettings["ClientSecret"];
    private string clientname = ConfigurationManager.AppSettings["ClientName"];
    private string clientAddress1 = ConfigurationManager.AppSettings["ClientAddress1"];
    private string clientAddress2 = ConfigurationManager.AppSettings["ClientAddress2"];
    private string clientAddress3 = ConfigurationManager.AppSettings["ClientAddress3"];
    private string clientsite = ConfigurationManager.AppSettings["Clientsite"];
    private string clientSMTP = ConfigurationManager.AppSettings["ClientSMTP"];
    private string clientPort = ConfigurationManager.AppSettings["ClientPort"];

    public RegisterResult RegisterUser(RegistrationUserClass registrationclass, UrlHelper urlHelper)
    {
        if (ConfirmUserExists(registrationclass) == RegisterResult.UserDoesntExist)
        {
            return (InsertUserDetails(registrationclass, urlHelper));
        }
        return (ConfirmUserExists(registrationclass));
    }

    public RegisterResult ConfirmUserExists(RegistrationUserClass registrationclass)
    {
        //if(IDNumberExists(registrationclass.id_number))
        //{
        //    return RegisterResult.IDNumberexists;
        //}
        if (PhoneNumberExists(registrationclass.phone_number))
        {
            return RegisterResult.PhoneNumberExists;
        }
        if (EmailExists(registrationclass.email_address))
        {
            return RegisterResult.EmailExists;
        }

        return RegisterResult.UserDoesntExist;
    }

    public RegisterResult InsertUserDetails(RegistrationUserClass registrationclass, UrlHelper urlHelper)
    {
        string password = "gecejfoiejfewfoewijje";
        string salt = PasswordHashers.GenerateSalt();
        string hashedPassword = PasswordHashers.HashPassword(password, salt);
        string verificationToken = Guid.NewGuid().ToString();
        DateTime tokenExpiry = DateTime.Now.AddHours(6);

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string query = @"INSERT INTO Recruitment_DummyData (IDNumber, Email, PhoneNumber, FirstName, MiddleName, LastName, PasswordHash, Salt, PasswordTrials, IsEmailVerified, EmailVerificationToken, EmailVerificationTokenExpiry)
                             VALUES (@IDNumber, @Email, @PhoneNumber, @FirstName, @MiddleName, @LastName, @PasswordHash, @Salt, @PasswordTrials, @IsEmailVerified, @EmailVerificationToken, @EmailVerificationTokenExpiry)";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@IDNumber", registrationclass.id_number);
                cmd.Parameters.AddWithValue("@Email", registrationclass.email_address);
                cmd.Parameters.AddWithValue("@PhoneNumber", registrationclass.phone_number);
                cmd.Parameters.AddWithValue("@FirstName", registrationclass.first_name);
                cmd.Parameters.AddWithValue("@MiddleName", registrationclass.middle_name);
                cmd.Parameters.AddWithValue("@LastName", registrationclass.last_name);
                cmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                cmd.Parameters.AddWithValue("@Salt", salt);
                cmd.Parameters.AddWithValue("@PasswordTrials", 0);
                cmd.Parameters.AddWithValue("@IsEmailVerified", false);
                cmd.Parameters.AddWithValue("@EmailVerificationToken", verificationToken);
                cmd.Parameters.AddWithValue("@EmailVerificationTokenExpiry", tokenExpiry);

                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        return (SendVerificationEmail(registrationclass.email_address, registrationclass.first_name, verificationToken, urlHelper));
    }

    public RegisterResult SetUserPassword(RegistrationUserClass registrationclass, string password)
    {
        int rowsAffected = 0;
        if (!EmailExists(registrationclass.email_address))
        {
            return RegisterResult.UserDoesntExist;
        }
        string salt = PasswordHashers.GenerateSalt();
        string hashedPassword = PasswordHashers.HashPassword(password, salt);

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string query = @"UPDATE Recruitment_DummyData
                             SET PasswordHash = @PasswordHash, Salt = @Salt
                             WHERE Email = @Email";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@PasswordHash", hashedPassword);
                cmd.Parameters.AddWithValue("@Salt", salt);
                cmd.Parameters.AddWithValue("@Email", registrationclass.email_address);
                conn.Open();
                rowsAffected = cmd.ExecuteNonQuery();
            }
        }
        if (rowsAffected > 0)
        {
            return RegisterResult.PasswordSet;
        }
        return RegisterResult.PasswordSet;
    }


    public LoginResult AuthenticateUser(string username, string password)
    {
        bool isemail = false;
        bool isidnumber = false;

        if (!EmailExists(username))
        {
            return LoginResult.UserDoesnotExist;
        }

        if (EmailExists(username))
        {
            isemail = true;
        }

        //if (IDNumberExists(username) && !EmailExists(username))
        //{
        //    isidnumber = true;
        //}
        string storedPasswordHash = null;
        string storedSalt = null;
        bool isEmailVerified = false;
        bool twoFactorEnabled = false;
        string userId = null;

        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string query = "";

            if (isidnumber)
            {
                query = "SELECT IDNumber, PasswordHash, Salt, IsEmailVerified, TwoFactorEnabled FROM Recruitment_DummyData WHERE IDNumber = @IDNumber";
            }
            if (isemail)
            {
                query = "SELECT IDNumber, PasswordHash, Salt, IsEmailVerified, TwoFactorEnabled FROM Recruitment_DummyData WHERE Email = @Email";
            }
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                if (isidnumber)
                {
                    cmd.Parameters.AddWithValue("@IDNumber", username);
                }
                if (isemail)
                {
                    cmd.Parameters.AddWithValue("@Email", username);
                }
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        userId = reader["IDNumber"].ToString();
                        storedPasswordHash = reader["PasswordHash"].ToString();
                        storedSalt = reader["Salt"].ToString();
                        isEmailVerified = (bool)reader["IsEmailVerified"];
                        twoFactorEnabled = (bool)reader["TwoFactorEnabled"];
                    }
                }
            }
        }

        if (string.IsNullOrEmpty(storedPasswordHash) || string.IsNullOrEmpty(storedSalt))
        {
            return LoginResult.InvalidCredentials;
        }


        if (!PasswordHashers.VerifyPassword(password, storedPasswordHash))
        {
            return LoginResult.InvalidCredentials;
        }

        if (!isEmailVerified)
        {
            return LoginResult.EmailNotVerified;
        }

        if (twoFactorEnabled)
        {
            return LoginResult.TwoFactorEnabled;
        }

        return LoginResult.Success;

    }

    public ResetResult ResetUserPassword(string email, UrlHelper urlHelper)
    {
        string membername = null;
        string verificationToken = Guid.NewGuid().ToString();
        DateTime tokenExpiry = DateTime.Now.AddMinutes(45);

        int rowsAffected = 0;

        if (!EmailExists(email))
        {
            return ResetResult.EmailDoesNotExist;
        }

        
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string query = @"UPDATE Recruitment_DummyData
                             SET ResetVerificationToken = @ResetVerificationToken, ResetVerificationTokenExpiry = @ResetVerificationTokenExpiry
                             WHERE Email = @Email";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@ResetVerificationToken", verificationToken);
                cmd.Parameters.AddWithValue("@ResetVerificationTokenExpiry", tokenExpiry);
                cmd.Parameters.AddWithValue("@Email", email);
                conn.Open();
                rowsAffected = cmd.ExecuteNonQuery();
            }

            if (rowsAffected > 0)
            {
                string query1 = "SELECT * FROM Recruitment_DummyData WHERE Email = @Email";

                using (SqlCommand cmd = new SqlCommand(query1, conn))
                {
                    cmd.Parameters.AddWithValue("@Email", email);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            membername = reader["FirstName"].ToString();
                            return (SendPasswordResetEmail(email, membername, verificationToken, urlHelper));
                        }
                    }
                }
            }
        }
        return ResetResult.EmailDoesNotExist;

    }

    public JobController.PersonalInformation GetUserDetails(string identifier, string identifiertype)
    {
        JobController.PersonalInformation userdetails = new JobController.PersonalInformation();
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string query = "";
            if (identifiertype == "email")
            {
                query = "SELECT * FROM Recruitment_DummyData WHERE Email = @Email";
            }
            if (identifiertype == "idnumber")
            {
                query = "SELECT * FROM Recruitment_DummyData WHERE IDNumber = @IDNumber";
            }
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                if (identifiertype == "email")
                {
                    cmd.Parameters.AddWithValue("@Email", identifier);
                }
                if (identifiertype == "idnumber")
                {
                    cmd.Parameters.AddWithValue("@IDNumber", identifier);
                }

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        userdetails.idnumber = reader["IDNumber"].ToString();
                        userdetails.firstname = reader["FirstName"].ToString();
                        userdetails.middlename = (reader["MiddleName"] == DBNull.Value) ? "" : reader["MiddleName"].ToString();
                        userdetails.lastname = reader["LastName"].ToString();
                        userdetails.gender = (reader["Gender"] == DBNull.Value) ? "" : reader["Gender"].ToString();
                        userdetails.citizenship = (reader["Citizenship"] == DBNull.Value) ? "" : reader["Citizenship"].ToString();
                        userdetails.specialneeds = (reader["Disability"] == DBNull.Value) ? "" : reader["Disability"].ToString();
                        userdetails.disabilitydetails = (reader["DisablingDetails"] == DBNull.Value) ? "" : reader["DisablingDetails"].ToString();
                        userdetails.email = reader["Email"].ToString();
                        userdetails.telephone = reader["PhoneNumber"].ToString();
                    }
                }
            }
        }

        return userdetails;
    }

    public void UpdateUserDetails(JobController.PersonalInformation userdetails)
    {
        int rowsAffected = 0;
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string query = @"UPDATE Recruitment_DummyData
                         SET MiddleName = @MiddleName, Gender = @Gender, Citizenship = @Citizenship, Disability = @Disability, DisablingDetails = @DisablingDetails, PhoneNumber = @PhoneNumber
                         WHERE Email = @Email";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Email", userdetails.email);
                cmd.Parameters.AddWithValue("@MiddleName", userdetails.middlename ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Gender", string.IsNullOrEmpty(userdetails.gender) ? (object)DBNull.Value : userdetails.gender);
                cmd.Parameters.AddWithValue("@Citizenship", userdetails.citizenship ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@Disability", userdetails.specialneeds ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DisablingDetails", userdetails.disabilitydetails ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@PhoneNumber", userdetails.telephone ?? (object)DBNull.Value);

                try
                {
                    conn.Open();
                    rowsAffected = cmd.ExecuteNonQuery();
                }
                catch (SqlException ex)
                {
                    Console.WriteLine($"Database error updating user {userdetails.idnumber}: {ex.Message}");
                    throw; // Re-throw to propagate the error
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An unexpected error occurred updating user {userdetails.idnumber}: {ex.Message}");
                    throw; // Re-throw to propagate the error
                }
            }
        }
    }



    private bool EmailExists(string email)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string query = "SELECT COUNT(*) FROM Recruitment_DummyData WHERE Email = @Email";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Email", email);
                conn.Open();
                return (int)cmd.ExecuteScalar() > 0;
            }
        }
    }

    //private bool IDNumberExists(string idnumber)
    //{
    //    using (SqlConnection conn = new SqlConnection(connectionString))
    //    {
    //        string query = "SELECT COUNT(*) FROM Recruitment_DummyData WHERE IDNumber = @IDNumber";
    //        using (SqlCommand cmd = new SqlCommand(query, conn))
    //        {
    //            cmd.Parameters.AddWithValue("@IDNumber", idnumber);
    //            conn.Open();
    //            return (int)cmd.ExecuteScalar() > 0;
    //        }
    //    }
    //}


    private bool PhoneNumberExists(string phonenumber)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string query = "SELECT COUNT(*) FROM Recruitment_DummyData WHERE PhoneNumber = @PhoneNumber";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@PhoneNumber", phonenumber);
                conn.Open();
                //return (int)cmd.ExecuteScalar() > 0;
                return false;
            }
        }
    }


    private RegisterResult SendVerificationEmail(string email, string name, string token, UrlHelper urlHelper)
    {
        string protocol = urlHelper.RequestContext.HttpContext.Request.Url.Scheme;
        string verificationLink = urlHelper.Action("Index", "VerifyEmail", new { Token = token }, protocol);
        string subject = $"Verify Your Account - {clientname} Recruitment";

        StringBuilder bodyBuilder = new StringBuilder();
        bodyBuilder.Append("<!DOCTYPE html>");
        bodyBuilder.Append("<html lang=\"en\">");
        bodyBuilder.Append("<head>");
        bodyBuilder.Append("    <meta charset=\"UTF-8\">");
        bodyBuilder.Append("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        bodyBuilder.Append($"    <title>{clientname} Recruitment | Email Verification</title>");
        bodyBuilder.Append("    <style>");
        bodyBuilder.Append("        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f4f4; margin: 0; padding: 0; }");
        bodyBuilder.Append("        .container { max-width: 600px; margin: 20px auto; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 0 10px rgba(0, 0, 0, 0.1); }");
        bodyBuilder.Append("        .header { background-color: #4CAF50; color: #ffffff; padding: 20px; text-align: center; font-size: 24px; border-top-left-radius: 8px; border-top-right-radius: 8px; }");
        bodyBuilder.Append("        .content { padding: 30px; line-height: 1.6; color: #333333; }");
        bodyBuilder.Append("        .button-container { text-align: center; margin: 25px 0; }");
        bodyBuilder.Append("        .button { display: inline-block; padding: 12px 25px; background-color: #007bff; color: #ffffff; text-decoration: none; border-radius: 5px; font-size: 16px; font-weight: bold; }");
        bodyBuilder.Append("        .button:hover { background-color: #0056b3; }");
        bodyBuilder.Append("        .expiration { text-align: center; font-size: 14px; color: #666666; margin-top: 20px; }");
        bodyBuilder.Append("        .footer { background-color: #f0f0f0; padding: 20px; text-align: center; font-size: 12px; color: #777777; border-bottom-left-radius: 8px; border-bottom-right-radius: 8px; margin-top: 30px; }");
        bodyBuilder.Append("        .footer a { color: #007bff; text-decoration: none; }");
        bodyBuilder.Append("        .system-notice { font-size: 11px; color: #999999; text-align: center; margin-top: 15px; }");
        bodyBuilder.Append("    </style>");
        bodyBuilder.Append("</head>");
        bodyBuilder.Append("<body>");
        bodyBuilder.Append("    <div class=\"container\">");
        bodyBuilder.Append("        <div class=\"header\">");
        bodyBuilder.Append("            Email Verification");
        bodyBuilder.Append("        </div>");
        bodyBuilder.Append("        <div class=\"content\">");
        bodyBuilder.Append($"            <p>Dear {name},</p>");
        bodyBuilder.Append($"            <p>Thank you for registering with {clientname} Recruitment! To complete your registration and activate your account, please verify your email address by clicking the button below:</p>");
        bodyBuilder.Append("            <div class=\"button-container\">");
        bodyBuilder.Append($"                <a href=\"{verificationLink}\" class=\"button\">Verify Your Email</a>");
        bodyBuilder.Append("            </div>");
        bodyBuilder.Append("            <p class=\"expiration\">This verification link will expire in <strong>6 hours</strong> for security reasons.</p>");
        bodyBuilder.Append($"            <p>If you did not create an account with {clientname} recruitment, please ignore this email.</p>");
        bodyBuilder.Append("        </div>");
        bodyBuilder.Append("        <div class=\"footer\">");
        bodyBuilder.Append($"            <p>&copy; {DateTime.Now.Year} {clientname} Recruitment. All rights reserved.</p>");
        bodyBuilder.Append($"            <p>{clientAddress1}</p>");
        bodyBuilder.Append($"            <p>{clientAddress2}</p>");
        bodyBuilder.Append($"            <p>{clientAddress3}</p>");
        bodyBuilder.Append($"            <p><a href=\"{clientsite}\">{clientsite}</a></p>");
        bodyBuilder.Append("        </div>");
        bodyBuilder.Append("        <div class=\"system-notice\">");
        bodyBuilder.Append("            <p>This is a system-generated email. Please do not reply to this message.</p>");
        bodyBuilder.Append("        </div>");
        bodyBuilder.Append("    </div>");
        bodyBuilder.Append("</body>");
        bodyBuilder.Append("</html>");

        string body = bodyBuilder.ToString();

        using (SmtpClient smtpClient = new SmtpClient(clientSMTP))
        {
            smtpClient.Port = int.Parse(clientPort);
            smtpClient.Credentials = new System.Net.NetworkCredential(clientemail, clientsecret);//Live
            smtpClient.EnableSsl = true;

            MailMessage mail = new MailMessage();
            string mailtextcustom = $"{clientname} Recruitment"; 
            mail.From = new MailAddress(clientemail, mailtextcustom);
            mail.To.Add(email); // Live
            //mail.To.Add("scholicks093@gmail.com");// Testing
            mail.Subject = subject;
            mail.Body = body;
            mail.IsBodyHtml = true;

            try
            {
                smtpClient.Send(mail);
                return RegisterResult.EmailSent;
            }
            catch (SmtpException ex)
            {
                return RegisterResult.EmailNotSent;
            }
            catch (Exception ex)
            {
                return RegisterResult.EmailNotSent;
            }
        }
    }

    private ResetResult SendPasswordResetEmail(string email, string name, string token, UrlHelper urlHelper)
    {
        string protocol = urlHelper.RequestContext.HttpContext.Request.Url.Scheme;
        string verificationLink = urlHelper.Action("SetNewPassword", "ResetPassword", new { Token = token }, protocol);
        string subject = $"Reset Account Password - {clientname} Recruitment";

        StringBuilder bodyBuilder = new StringBuilder();
        bodyBuilder.Append("<!DOCTYPE html>");
        bodyBuilder.Append("<html lang=\"en\">");
        bodyBuilder.Append("<head>");
        bodyBuilder.Append("    <meta charset=\"UTF-8\">");
        bodyBuilder.Append("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        bodyBuilder.Append($"    <title>{clientname} Recruitment | Reset Account Password</title>");
        bodyBuilder.Append("    <style>");
        bodyBuilder.Append("        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #f4f4f4; margin: 0; padding: 0; }");
        bodyBuilder.Append("        .container { max-width: 600px; margin: 20px auto; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 0 10px rgba(0, 0, 0, 0.1); }");
        bodyBuilder.Append("        .header { background-color: #0e426f; color: #ffffff; padding: 20px; text-align: center; font-size: 24px; border-top-left-radius: 8px; border-top-right-radius: 8px; }");
        bodyBuilder.Append("        .content { padding: 30px; line-height: 1.6; color: #333333; }");
        bodyBuilder.Append("        .button-container { text-align: center; margin: 25px 0; }");
        bodyBuilder.Append("        .button { display: inline-block; padding: 12px 25px; background-color: #1FA700; color: #ffffff; text-decoration: none; border-radius: 5px; font-size: 16px; font-weight: bold; }");
        bodyBuilder.Append("        .button { color: #fff !important;}");
        bodyBuilder.Append("        .button:hover { background-color: #0e426f; }");
        bodyBuilder.Append("        .expiration { text-align: center; font-size: 14px; color: #666666; margin-top: 20px; }");
        bodyBuilder.Append("        .footer { background-color: #f0f0f0; padding: 20px; text-align: center; font-size: 12px; color: #777777; border-bottom-left-radius: 8px; border-bottom-right-radius: 8px; margin-top: 30px; }");
        bodyBuilder.Append("        .footer a { color: #007bff; text-decoration: none; }");
        bodyBuilder.Append("        .system-notice { font-size: 11px; color: #999999; text-align: center; margin-top: 15px; }");
        bodyBuilder.Append("    </style>");
        bodyBuilder.Append("</head>");
        bodyBuilder.Append("<body>");
        bodyBuilder.Append("    <div class=\"container\">");
        bodyBuilder.Append("        <div class=\"header\">");
        bodyBuilder.Append("            Password Reset");
        bodyBuilder.Append("        </div>");
        bodyBuilder.Append("        <div class=\"content\">");
        bodyBuilder.Append($"            <p>Dear {name},</p>");
        bodyBuilder.Append("            <p>We have received a password reset request for your account. Kindly click the link below to set yor new password</p>");
        bodyBuilder.Append("            <div class=\"button-container\">");
        bodyBuilder.Append($"                <a href=\"{verificationLink}\" class=\"button\">Set New Password</a>");
        bodyBuilder.Append("            </div>");
        bodyBuilder.Append("            <p class=\"expiration\">This verification link will expire in <strong>45 minutes</strong> for security reasons.</p>");
        bodyBuilder.Append("            <p>If you did not request for this password reset, please ignore this email.</p>");
        bodyBuilder.Append("        </div>");
        bodyBuilder.Append("        <div class=\"footer\">");
        bodyBuilder.Append($"            <p>&copy; {DateTime.Now.Year} {clientname} Recruitment. All rights reserved.</p>");
        bodyBuilder.Append($"            <p>{clientAddress1}</p>");
        bodyBuilder.Append($"            <p>{clientAddress2}</p>");
        bodyBuilder.Append($"            <p>{clientAddress3}</p>");
        bodyBuilder.Append($"            <p><a href=\"{clientsite}\">{clientsite}</a></p>");
        bodyBuilder.Append("        </div>");
        bodyBuilder.Append("        <div class=\"system-notice\">");
        bodyBuilder.Append("            <p>This is a system-generated email. Please do not reply to this message.</p>");
        bodyBuilder.Append("        </div>");
        bodyBuilder.Append("    </div>");
        bodyBuilder.Append("</body>");
        bodyBuilder.Append("</html>");

        string body = bodyBuilder.ToString();

        using (SmtpClient smtpClient = new SmtpClient(clientSMTP))
        {
            smtpClient.Port = int.Parse(clientPort);
            smtpClient.Credentials = new System.Net.NetworkCredential(clientemail, clientsecret);//Live
            smtpClient.EnableSsl = true;

            MailMessage mail = new MailMessage();
            string mailtextcustom = $"{clientname} Recruitment";
            mail.From = new MailAddress(clientemail, mailtextcustom);
            mail.To.Add(email); // Live - Send to member
            //mail.To.Add("scholicks093@gmail.com");// Testing - Send to self
            mail.Subject = subject;
            mail.Body = body;
            mail.IsBodyHtml = true;

            try
            {
                smtpClient.Send(mail);
                return ResetResult.EmailSent;
            }
            catch (SmtpException ex)
            {
                return ResetResult.EmailNotSent;
            }
            catch (Exception ex)
            {
                return ResetResult.EmailNotSent;
            }
        }
    }

    public VerifyEmailClass VerifyEmailToken(string token, string verificationtype)
    {
        VerifyEmailClass verifyEmailClass = new VerifyEmailClass();
        int rowsAffected = 0;
        verifyEmailClass.status = false;
        int passwordtrials = 0;
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string query1 = null;

            if (verificationtype == "email_verification")
            {
                query1 = @"SELECT Email, PasswordTrials FROM Recruitment_DummyData WHERE EmailVerificationToken = @Token AND EmailVerificationTokenExpiry > GETDATE()";
            }
            if (verificationtype == "password_verification")
            {
                query1 = @"SELECT Email, PasswordTrials FROM Recruitment_DummyData WHERE ResetVerificationToken = @Token AND ResetVerificationTokenExpiry > GETDATE()";
            }
            using (SqlCommand cmd = new SqlCommand(query1, conn))
            {
                cmd.Parameters.AddWithValue("@Token", token);
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        verifyEmailClass.email = reader["Email"].ToString();
                        passwordtrials = reader.GetInt32(reader.GetOrdinal("PasswordTrials"));
                    }
                }
            }

            string query = null;


            if (verificationtype == "email_verification")
            {
                if (passwordtrials > 1)
                {
                    query = @"UPDATE Recruitment_DummyData
                             SET IsEmailVerified = 1, EmailVerificationToken = NULL, EmailVerificationTokenExpiry = NULL, CreatedDate = GETDATE()
                             WHERE EmailVerificationToken = @Token AND EmailVerificationTokenExpiry > GETDATE()";
                }
                else
                {
                    query = @"UPDATE Recruitment_DummyData
                             SET PasswordTrials = PasswordTrials+1
                             WHERE EmailVerificationToken = @Token AND EmailVerificationTokenExpiry > GETDATE()";
                }
            }
            if (verificationtype == "password_verification")
            {
                if (passwordtrials > 1)
                {
                    query = @"UPDATE Recruitment_DummyData
                             SET ResetVerificationToken = NULL, ResetVerificationTokenExpiry = NULL
                             WHERE ResetVerificationToken = @Token AND ResetVerificationTokenExpiry > GETDATE()";
                }
                else
                {
                    query = @"UPDATE Recruitment_DummyData
                             SET PasswordTrials = PasswordTrials+1
                             WHERE ResetVerificationToken = @Token AND ResetVerificationTokenExpiry > GETDATE()";
                }
            }
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@Token", token);
                rowsAffected = cmd.ExecuteNonQuery();
            }
        }
        if (rowsAffected > 0)
        {
            verifyEmailClass.status = true;
        }

        return verifyEmailClass;
    }


    /*Document Saving and Retrieval Section*/

    public List<JobController.Attachment> GetJobAttachments(string applicationNumber)
    {
        List<JobController.Attachment> attachments = new List<JobController.Attachment>();

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            string query = "SELECT * FROM Recruitment_DocumentData WHERE JOBID = @JOBID ORDER BY AmendDate DESC";

            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@JOBID", applicationNumber);

                try
                {
                    connection.Open();

                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            JobController.Attachment attachment = new JobController.Attachment();

                            attachment.attachmentid = reader.GetInt32(reader.GetOrdinal("Id"));
                            attachment.type = reader["DocType"] as string;
                            attachment.description = reader["DocDescription"] as string;
                            attachment.documentpath = reader["DocLink"] as string;
                            int uploaddateindex = reader.GetOrdinal("AmendDate");
                            attachment.amend_date = reader.GetDateTime(uploaddateindex);

                            attachments.Add(attachment);
                        }
                    }
                }
                catch (SqlException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"SQL Error retrieving job attachments: {ex.Message} - {ex.ErrorCode}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error retrieving job attachments: {ex.Message}");
                }
            }
        }


        return attachments;
    }

    public string GetFilePathFromDatabase(int attachmentId)
    {
        string actualPath = null;
        //string connectionString = ConfigurationManager.ConnectionStrings["YourDbConnection"].ConnectionString;
        string query = "SELECT DocLink FROM Recruitment_DocumentData WHERE Id = @AttachmentId";

        using (SqlConnection connection = new SqlConnection(connectionString))
        {
            using (SqlCommand command = new SqlCommand(query, connection))
            {
                command.Parameters.AddWithValue("@AttachmentId", attachmentId);
                connection.Open();
                object result = command.ExecuteScalar();
                if (result != null && result != DBNull.Value)
                {
                    actualPath = result.ToString();
                }
            }
        }
        return actualPath;
    }
    public bool FilesBelongsToUser(int attachmentId, string userid)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string query = @"SELECT COUNT(*) FROM Recruitment_DocumentData 
                         WHERE Id = @AttachmentId AND IDNumber = @IDNumber";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@AttachmentId", attachmentId);
                cmd.Parameters.AddWithValue("@IDNumber", userid);
                conn.Open();
                return (int)cmd.ExecuteScalar() > 0;
            }
        }
    }
    public bool SendDocumentToDatabase(JobController.DocumentAttachmentViewModel attachment)
    {

        if (!DocExists(attachment))
        {
            return UploadDocument(attachment);
        }

        return UpdateDocument(attachment);
    }

    public bool UpdateDocument(JobController.DocumentAttachmentViewModel attachment)
    {
        int rowsAffected = 0;
        DateTime currentdate = DateTime.Now;
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string query = @"UPDATE Recruitment_DocumentData
                     SET DocDescription = @DocDescription, DocLink = @DocLink, AmendDate = @AmendDate
                     WHERE JOBID = @JOBID AND DocType = @DocType AND IDNumber = @IDNumber";

            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@JOBID", attachment.ApplicationNumber);
                cmd.Parameters.AddWithValue("@DocType", attachment.DocumentType);
                cmd.Parameters.AddWithValue("@DocDescription", attachment.DocumentDescription ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DocLink", attachment.DocumentPath ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@AmendDate", currentdate);
                cmd.Parameters.AddWithValue("@IDNumber", attachment.ApplicantID); // ✅ Added

                conn.Open();
                rowsAffected = cmd.ExecuteNonQuery();
            }
        }
        return rowsAffected > 0;
    }


    public bool UploadDocument(JobController.DocumentAttachmentViewModel attachment)
    {
        DateTime currentdate = DateTime.Now;
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            string query = @"INSERT INTO Recruitment_DocumentData (IDNumber, JOBID, DocType, DocDescription, DocLink, UploadDate)
                             VALUES (@IDNumber, @JOBID, @DocType, @DocDescription, @DocLink, @UploadDate)";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@IDNumber", attachment.ApplicantID);
                cmd.Parameters.AddWithValue("@JOBID", attachment.ApplicationNumber);
                cmd.Parameters.AddWithValue("@DocType", attachment.DocumentType);
                cmd.Parameters.AddWithValue("@DocDescription", attachment.DocumentDescription);
                cmd.Parameters.AddWithValue("@DocLink", attachment.DocumentPath);
                cmd.Parameters.AddWithValue("@UploadDate", currentdate);

                
                try
                {
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                catch (SqlException ex)
                {
                    Console.WriteLine($"Database error updating user {ex.Message}");
                    throw; // Re-throw to propagate the error
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An unexpected error occurred updating user {ex.Message}");
                    throw; // Re-throw to propagate the error
                }
            }
        }
        return true;
    }

    public bool DocExists(JobController.DocumentAttachmentViewModel attachment)
    {
        using (SqlConnection conn = new SqlConnection(connectionString))
        {
            // ✅ Add IDNumber check so one user can't overwrite another's doc
            string query = @"SELECT COUNT(*) FROM Recruitment_DocumentData 
                         WHERE JOBID = @JOBID AND DocType = @DocType AND IDNumber = @IDNumber";
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@JOBID", attachment.ApplicationNumber);
                cmd.Parameters.AddWithValue("@DocType", attachment.DocumentType);
                cmd.Parameters.AddWithValue("@IDNumber", attachment.ApplicantID); // ✅ Added
                conn.Open();
                return (int)cmd.ExecuteScalar() > 0;
            }
        }
    }
    public string GetRequisitionJDAttachmentPath(string requisitionNo)
    {
        using (var client = new ProxyClient())
        {
            return client.GetRequisitionJDAttachmentPath(requisitionNo);
        }
    }
    public enum LoginResult
    {
        Success,
        InvalidCredentials,
        EmailNotVerified,
        TwoFactorEnabled,
        TwoFactorDisabled,
        UserDoesnotExist
    }

    public enum RegisterResult
    {
        PhoneNumberExists,
        EmailExists,
        IDNumberexists,
        UserDoesntExist,
        EmailSent,
        EmailNotSent,
        PasswordSet,
        PasswordNotSet
    }

    public enum ResetResult
    {
        EmailDoesNotExist,
        EmailSent,
        EmailNotSent
    }
}

public class VerifyEmailClass
{
    public string email { get; set; }
    public string idnumber { get; set; }
    public bool status { get; set; }
}

