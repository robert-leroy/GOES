using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using System.Net.Mail;
using System.Net;
using System.Data.Common;
using System.Threading;
using MySql.Data.MySqlClient;
using System.Windows.Forms;
using System.Collections.ObjectModel;

namespace GOES_Application
{
    public static class GoesCrawler
    {

        // Server Application entry point
        public static void AppCrawlSite(Object pObj)
        {
            Int32 pUser_id = (Int32)pObj;
            LogStatus(pUser_id, "Started processing for " + pUser_id.ToString());

            // lblLastCrawlTime.Text = DateTime.Now.ToShortTimeString();
            GoesUser goes_user = new GoesUser();
            if (goes_user.GetDataForUser(pUser_id) == true)
            {
                goes_user.mAppType = GoesUser.AppType.Server;
                CrawlSite(goes_user);
            }
            else
            {
                LogStatus(pUser_id, "The Userprofile is not complete. Check the profile page and/or https://www.globalentryinterview.com/interview-as-a-service/", "");
                RaiseCrawlComplete(null);
                return;
            }
        }

        // Desktop Application entry point
        public static void DesktopCrawlSite(Object pObj)
        {
            GoesUser goes_user = (GoesUser)pObj;
            goes_user.mAppType = GoesUser.AppType.Desktop;
            CrawlSite(goes_user);
        }

        // Main thread for the site crawl
        // It's a monolithic function but follows a simple pattern
        //     each step tries to find an HTML object and throws an exception when the object is not found
        public static void CrawlSite(GoesUser pUser)
        { 

            using (IWebDriver driver = new FirefoxDriver())
            {
                if (pUser.mAppType == GoesUser.AppType.Server)
                    LogStatus(pUser.mUser_id, "App Driver opened for " + pUser.mUsername);
                
                // Start the Browser
                driver.Navigate().GoToUrl("https://goes-app.cbp.dhs.gov/goes/");

                // Find the username object.  
                // When this fails, the site didn't load.  Most likely because the computer is offline.
                try
                {
                    driver.FindElement(By.Id("j_username")).SendKeys(pUser.mUsername);
                }
                catch (Exception ex)
                {
                    if (pUser.mAppType == GoesUser.AppType.Server)
                        LogStatus(pUser.mUser_id, "The GOES website didn't load as expected. Check your internet connection and retry.", ex.Message);
                    else
                        ShowMessage("The GOES website didn't load as expected. Check your internet connection and retry.");
                    RaiseCrawlComplete(null);
                    return;
                }

                // When we find the username object, we can then populate the other fields.
                driver.FindElement(By.Id("j_password")).Clear();
                driver.FindElement(By.Id("j_password")).SendKeys(pUser.mPassword);
                driver.FindElement(By.Id("SignIn")).Click();

                // Get past the "I'm a Human" checkbox thingy
                try
                {
                    driver.FindElement(By.Id("checkMe")).Click();
                } catch (Exception ex)
                {
                    String strErrorMessage = "GOES Username or password is not valid.";
                    if (pUser.mAppType == GoesUser.AppType.Server)
                    {
                        LogStatus(pUser.mUser_id, strErrorMessage, ex.Message);
                        SendErrorEmail(strErrorMessage, pUser.mEmail);
                    }
                    else
                        ShowMessage(strErrorMessage);
                    RaiseCrawlComplete(null);
                    return;
                }

                // Click the Manage Appointment button
                // Get past the "I'm a Human" thingy
                try
                {
                    driver.FindElement(By.Name("manageAptm")).Click();
                }
                catch (Exception ex)
                {
                    String strErrorMessage = "Manage Appointment button is not found.  Has your application been approved?";
                    if (pUser.mAppType == GoesUser.AppType.Server)
                    {
                        LogStatus(pUser.mUser_id, strErrorMessage, ex.Message);
                        SendErrorEmail(strErrorMessage, pUser.mEmail);
                    }
                    else
                        ShowMessage(strErrorMessage);
                    RaiseCrawlComplete(null);
                    return;
                }

                DateTime dateInterview;
                String CurrentInterview;

                // Capture my current Date of the interview
                try
                {
                    IWebElement elem = driver.FindElement(By.XPath("//*[contains(.,'Interview Date:')]"));
                    if (elem == null)
                    {
                        String strErrorMessage = "The current inteview date is not found.  Has your application been approved?";
                        if (pUser.mAppType == GoesUser.AppType.Server)
                        { 
                            LogStatus(pUser.mUser_id, strErrorMessage, "None");
                            SendErrorEmail(strErrorMessage, pUser.mEmail);
                        }
                        else
                            ShowMessage(strErrorMessage);
                        RaiseCrawlComplete(null);
                        return;
                    }
                    int pos = elem.Text.IndexOf("Interview Date:");
                    CurrentInterview = elem.Text.Substring(pos + 16, 12);
                    dateInterview = Convert.ToDateTime(CurrentInterview);
                }
                catch (Exception ex)
                {
                    String strErrorMessage = "The current inteview date is not found.  Has your application been approved?";
                    if (pUser.mAppType == GoesUser.AppType.Server)
                    { 
                        LogStatus(pUser.mUser_id, strErrorMessage, ex.Message);
                        SendErrorEmail(strErrorMessage, pUser.mEmail);
                    }
                    else
                        ShowMessage(strErrorMessage);
                    RaiseCrawlComplete(null);
                    return;
                }

                // Check for interview date in the past.  This happens when someone misses their appointment.
                if (DateTime.Compare(dateInterview, DateTime.Now) < 0)
                {
                    String strErrorMessage = "Your current inteview date is in the past. You need to manually schedule your interview the first time.";
                    if (pUser.mAppType == GoesUser.AppType.Server)
                    { 
                        LogStatus(pUser.mUser_id, strErrorMessage);
                        SendErrorEmail(strErrorMessage, pUser.mEmail);
                    }
                    else
                        ShowMessage(strErrorMessage);
                    RaiseCrawlComplete(null);
                    return;
                }


                // Click the Reschedule button
                try
                {
                    driver.FindElement(By.Name("reschedule")).Click();
                }
                catch (Exception ex)
                {
                    String strErrorMessage = "Reschedule button is not found.  Has your application been approved?";
                    if (pUser.mAppType == GoesUser.AppType.Server)
                    { 
                        LogStatus(pUser.mUser_id, strErrorMessage, ex.Message);
                        SendErrorEmail(strErrorMessage, pUser.mEmail);
                    }
                    else
                        ShowMessage(strErrorMessage);
                    RaiseCrawlComplete(null);
                    return;
                }

                // Pick the proper location
                try
                {
                    bool bLocationFound = false;

                    // new SelectElement(driver.FindElement(By.Id("selectedEnrollmentCenter"))).SelectByText(pUser.mLocation_Name);
                    ReadOnlyCollection<IWebElement> we = driver.FindElements(By.Name("selectedEnrollmentCenter"));
                    foreach (var title in we)
                    {
                        String text = title.GetAttribute("title");
                        if (text.Trim().CompareTo(pUser.mLocation_Name.Trim()) == 0)
                        {
                            title.Click();
                            bLocationFound = true;
                            break;
                        }
                    }

//                    new SelectElement(driver.FindElement(By.Name("selectedEnrollmentCenter"))).SelectByText(pUser.mLocation_Name);
//                    String strPath = "//*[@title='" + pUser.mLocation_Name + "']";
  //                  IWebElement we = driver.FindElement(By.XPath(strPath));
    //                new SelectElement(we);
                    if (bLocationFound)
                        driver.FindElement(By.Name("next")).Click();
                    else
                    {
                        String strErrorMessage = "Can''t find your selected Enrollment Center.  Usually means the site had an error.";
                        if (pUser.mAppType == GoesUser.AppType.Server)
                        {
                            LogStatus(pUser.mUser_id, strErrorMessage);
                            SendSupportEmail(strErrorMessage);
                        }
                        else
                            ShowMessage(strErrorMessage);
                        RaiseCrawlComplete(null);
                        return;
                    }
                }
                catch (Exception ex)
                {
                    String strErrorMessage = "Can''t find your selected Enrollment Center.  Usually means the site had an error.";
                    if (pUser.mAppType == GoesUser.AppType.Server)
                    { 
                        LogStatus(pUser.mUser_id, strErrorMessage, ex.Message);
                        SendSupportEmail(strErrorMessage);
                    }
                    else
                        ShowMessage(strErrorMessage);
                    RaiseCrawlComplete(null);
                    return;
                }

                // Find and parse the current next available date
                DateTime dateAvailable;
                try
                {
                    String CurrentDate = driver.FindElement(By.ClassName("header")).Text;
                    String[] stringDateParts = CurrentDate.Split('\r');
                    String stringModifiedDate = stringDateParts[0] + " " + stringDateParts[2].Substring(1);
                    dateAvailable = Convert.ToDateTime(stringModifiedDate);
                }
                catch (Exception ex)
                {
                    String strErrorMessage = "Cant find header with current interview date.  Check the site for changes.";
                    if (pUser.mAppType == GoesUser.AppType.Server)
                    {
                        LogStatus(pUser.mUser_id, strErrorMessage, ex.Message);
                        SendSupportEmail(strErrorMessage);
                    }
                    else
                        ShowMessage(strErrorMessage);
                    RaiseCrawlComplete(null);
                    return;
                }

                // Now compare the dates and book the new if earlier than current interview date
                if (DateTime.Compare(dateAvailable, dateInterview) < 0)
                {
                    // This block schedules the new appointment
                    driver.FindElement(By.ClassName("entry")).Click();
                    driver.FindElement(By.Id("comments")).Clear();
                    driver.FindElement(By.Id("comments")).SendKeys("Looking for an earlier interview");
                    driver.FindElement(By.Name("Confirm")).Click();

                    // Find the New Interview Time 
                    Int32 intTimePos = driver.PageSource.IndexOf("New Interview Time:");
                    String strNewTime = driver.PageSource.Substring(intTimePos + 20, 8);

                    SendEmail(dateAvailable.ToShortDateString(), strNewTime, pUser.mEmail);

                    if (pUser.mAppType == GoesUser.AppType.Server)
                    {
                        LogStatus(pUser.mUser_id, dateAvailable, "Congratulations! We found an earlier date. The process has stopped, please visit the site if you want to continue searching for an earlier date.");
                        SetUserStatus(pUser.mUser_id, 3);
                    }
                    else
                    {
                        ShowMessage("Congratulations! We found an earlier date. The process has stopped, please visit the site if you want to continue searching for an earlier date.");
                    }
                }
                else
                {
                    if (pUser.mAppType == GoesUser.AppType.Server)
                    {
                        LogStatus(pUser.mUser_id, dateAvailable, "Searched the site. Earliest date is " + dateAvailable.ToShortDateString());
                        SetUserStatus(pUser.mUser_id, 1);
                    }
                    GoesEventArgs ge = new GoesEventArgs();
                    ge.GoesResult = dateAvailable.ToShortDateString();
                    RaiseCrawlComplete(ge);
                    return;
                }

                return;
            }
        }

        // Displays an error message
        static void ShowMessage(String pMsg)
        {
            MessageBox.Show(pMsg, "GOES Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        // When a crawl completes, this notifies the UI
        public static event CrawlCompleteEventHandler CrawlComplete;
        private static void RaiseCrawlComplete(GoesEventArgs e)
        {
            if (CrawlComplete != null)
                CrawlComplete(null, e);
        }

        // The server application log is in the database, there are three implementations of this function
        private static Boolean LogStatus(int pUser_id, DateTime pDateAvailable, String pStatus)
        {
            MySqlConnection connMySQL;
            string myConnectionString;
            myConnectionString = DecryptSetting(Properties.Settings.Default.MySQL);

            try
            {
                connMySQL = new MySqlConnection();
                connMySQL.ConnectionString = myConnectionString;
                connMySQL.Open();
            }
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                ShowMessage(ex.Message);
                return false;
            }

            // SQL Statement to log the message
            String strSQL = "INSERT INTO goes_log (user_id, last_date_found, last_run_date, thread_id, server_id, status) ";
            strSQL += "VALUES ('" + pUser_id.ToString() + "', '" + pDateAvailable.ToString("yyyy-MM-dd HH:mm:ss") + "', '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "', '" + Thread.CurrentThread.ManagedThreadId + "', '" + System.Environment.MachineName + "', '" + pStatus + "')";

            // Execute the SQL
            MySqlCommand cmd = connMySQL.CreateCommand();
            cmd.CommandText = strSQL;
            cmd.ExecuteNonQuery();

            return true;

        }

        // The server application log is in the database, there are three implementations of this function
        private static Boolean LogStatus(int pUser_id, String pStatus)
        {
            MySqlConnection connMySQL;
            string myConnectionString;
            myConnectionString = DecryptSetting(Properties.Settings.Default.MySQL);

            try
            {
                connMySQL = new MySqlConnection();
                connMySQL.ConnectionString = myConnectionString;
                connMySQL.Open();
            }
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                ShowMessage(ex.Message);
                return false;
            }

            // SQL Statement to retrieve the Order Header
            String strSQL = "INSERT INTO goes_log (user_id, last_run_date, thread_id, server_id, status) ";
            strSQL += "VALUES ('" + pUser_id.ToString() + "', '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "', '" + Thread.CurrentThread.ManagedThreadId + "', '" + System.Environment.MachineName + "', '" + pStatus + "')";

            // Execute the SQL
            MySqlCommand cmd = connMySQL.CreateCommand();
            cmd.CommandText = strSQL;
            cmd.ExecuteNonQuery();

            return true;

        }

        // The server application log is in the database, there are three implementations of this function
        private static Boolean LogStatus(int pUser_id, String pStatus, String pException)
        {
            MySqlConnection connMySQL;
            string myConnectionString;
            myConnectionString = DecryptSetting(Properties.Settings.Default.MySQL);

            try
            {
                connMySQL = new MySqlConnection();
                connMySQL.ConnectionString = myConnectionString;
                connMySQL.Open();
            }
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                ShowMessage(ex.Message);
                return false;
            }

            // SQL Statement to Update the Log File
            String strSQL = "INSERT INTO goes_log (user_id, last_run_date, thread_id, server_id, status, exception) ";
            strSQL += "VALUES ('" + pUser_id.ToString() + "', '" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "', '" + Thread.CurrentThread.ManagedThreadId + "', '" + System.Environment.MachineName + "', '" + pStatus + "', '" + pException + "')";

            // Execute the SQL
            MySqlCommand cmd = connMySQL.CreateCommand();
            cmd.CommandText = strSQL;
            cmd.ExecuteNonQuery();

            SetUserStatus(pUser_id, 2);

            return true;

        }

        // When the server completes a crawl, we have to track the results as "UserStatus"
        // This function updates the status in the database
        private static Boolean SetUserStatus(int pUser_id, int pStatus)
        {
            MySqlConnection connMySQL;
            string myConnectionString;
            myConnectionString = DecryptSetting(Properties.Settings.Default.MySQL);

            try
            {
                connMySQL = new MySqlConnection();
                connMySQL.ConnectionString = myConnectionString;
                connMySQL.Open();
            }
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                ShowMessage(ex.Message);
                return false;
            }

            // Find if the status row exists
            String strSQL = "SELECT count(umeta_id) FROM wp_xleb_usermeta WHERE meta_key = 'goes_status' and user_id = " + pUser_id;
            // Execute the SQL
            MySqlCommand cmd = connMySQL.CreateCommand();
            cmd.CommandText = strSQL;
            long statusCount = (long)cmd.ExecuteScalar();

            if (statusCount < 1)
            {
                strSQL = "INSERT INTO wp_xleb_usermeta (user_id, meta_key, meta_value) VALUES (" + pUser_id + ", 'goes_status', '" + pStatus +"');";
            }
            else
            {
                // SQL Statement to Change User Status
                strSQL = "UPDATE wp_xleb_usermeta SET meta_value = '" + pStatus + "' WHERE meta_key = 'goes_status' and user_id = " + pUser_id;
            }

            // Execute the SQL
            cmd.CommandText = strSQL;
            cmd.ExecuteNonQuery();

            return true;

        }

        public static String DecryptSetting(String pValue)
        {
            // Create a new TripleDESCryptoServiceProvider object
            // to generate a key and initialization vector (IV).
            TrippleDESCSPCoder coder = new TrippleDESCSPCoder();
            byte[] mEncryptedData = System.Convert.FromBase64String(pValue);
            String myString = TrippleDESCSPCoder.DecryptTextFromMemory(mEncryptedData, coder.mEncryptKey, coder.mEncryptVector);

            return myString;

        }

        // When we get a hit, we need to notify the user. 
        // This function sends mail to the user.
        public static Boolean SendEmail(String pDate, String pTime, String pAddress)
        {

            MailMessage mailMsg = new MailMessage();


            //Setting From , To and CC
            mailMsg.From = new MailAddress(Properties.Settings.Default.MailFromAddress, Properties.Settings.Default.MailFromName);
            mailMsg.To.Add(new MailAddress(pAddress));
            mailMsg.Bcc.Add(new MailAddress("goes@globalentryinterview.com"));

            mailMsg.Subject = Properties.Settings.Default.MailSubject;

            mailMsg.Body = Properties.Settings.Default.MailHeader;
            String msg = Properties.Settings.Default.MailBodyHit.Replace("<==here==>", pDate);
            //            mailMsg.Body += msg.Replace("<==date==>", pDate + " at " + pTime);
            mailMsg.Body += msg.Replace("<==date==>", pDate);
            mailMsg.Body += Properties.Settings.Default.MailFooter;
            mailMsg.IsBodyHtml = true;

            //gmailClient.Send(mail);
            using (SmtpClient smtpClient = new SmtpClient(Properties.Settings.Default.MailSMTPServer, Properties.Settings.Default.MailPort))
            {

                smtpClient.EnableSsl = false;
                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtpClient.UseDefaultCredentials = true;
                smtpClient.Credentials = new NetworkCredential(Properties.Settings.Default.MailUsername, DecryptSetting(Properties.Settings.Default.MailPassword));

                try
                {
                    smtpClient.Send(mailMsg);
                }
                catch(Exception ex)
                {
                    SendErrorEmail("Error Sending Email", "bob@leroynet.com");
                }
            }

            return true;
        }


        // When we get a hit, we need to notify the user. 
        // This function sends mail to the user.
        public static Boolean SendErrorEmail(String pMessage, String pAddress)
        {

            MailMessage mailMsg = new MailMessage();


            //Setting From , To and CC
            mailMsg.From = new MailAddress(Properties.Settings.Default.MailFromAddress, Properties.Settings.Default.MailFromName);
            mailMsg.To.Add(new MailAddress(pAddress));
            mailMsg.Bcc.Add(new MailAddress("goes@globalentryinterview.com"));

            mailMsg.Subject = "Global Entry Interview Tool -- Error";

            mailMsg.Body = Properties.Settings.Default.MailHeader;
            String msg = Properties.Settings.Default.MailBodyError.Replace("<==here==>", pMessage);
            mailMsg.Body += msg;
            mailMsg.Body += Properties.Settings.Default.MailFooter;
            mailMsg.IsBodyHtml = true;

            //gmailClient.Send(mail);
            using (SmtpClient smtpClient = new SmtpClient(Properties.Settings.Default.MailSMTPServer, Properties.Settings.Default.MailPort))
            {

                smtpClient.EnableSsl = false;
                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtpClient.UseDefaultCredentials = true;
                smtpClient.Credentials = new NetworkCredential(Properties.Settings.Default.MailUsername, DecryptSetting(Properties.Settings.Default.MailPassword));

                smtpClient.Send(mailMsg);
            }

            return true;
        }


        // When we get a hit, we need to notify the user. 
        // This function sends mail to the user.
        public static Boolean SendSupportEmail(String pMessage)
        {

            MailMessage mailMsg = new MailMessage();


            //Setting From , To and CC
            mailMsg.From = new MailAddress(Properties.Settings.Default.MailFromAddress, Properties.Settings.Default.MailFromName);
            mailMsg.Bcc.Add(new MailAddress("goes@globalentryinterview.com"));

            mailMsg.Subject = "Global Entry Interview Tool -- Support";

            mailMsg.Body = Properties.Settings.Default.MailHeader;
            String msg = Properties.Settings.Default.MailBodySupport.Replace("<==here==>", pMessage);
            mailMsg.Body += msg;
            mailMsg.Body += Properties.Settings.Default.MailFooter;
            mailMsg.IsBodyHtml = true;

            //gmailClient.Send(mail);
            using (SmtpClient smtpClient = new SmtpClient(Properties.Settings.Default.MailSMTPServer, Properties.Settings.Default.MailPort))
            {

                smtpClient.EnableSsl = false;
                smtpClient.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtpClient.UseDefaultCredentials = true;
                smtpClient.Credentials = new NetworkCredential(Properties.Settings.Default.MailUsername, DecryptSetting(Properties.Settings.Default.MailPassword));

                smtpClient.Send(mailMsg);
            }

            return true;
        }
    }
}
