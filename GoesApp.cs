using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

// Requires reference to WebDriver.Support.dll
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using System.Net.Mail;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Net.Security;
using System.Globalization;
using System.Data.Common;
using System.Threading;
using MySql.Data.MySqlClient;
using GOES_Application;
using System.Security.Cryptography;

namespace GOES_Application
{
    public partial class frmGOES : Form
    {
        private int mThreadCount;
        private int mCrawlCount;
        private byte[] mEncryptedData;

        public frmGOES()
        {
            InitializeComponent();
            mThreadCount = mCrawlCount = 0;
            GoesCrawler.CrawlComplete += HandleCrawlCompleted;
        }

        // When the Go button is clicked we need to start the Timer and start the first crawl.
        private void cmdGo_Click(object sender, EventArgs e)
        {

            timer1.Enabled = true;
            lblStatus.Text = "Running";

            StartMultipleSiteCrawls();

        }

        // This function is called when a crawl is complete
        public void HandleCrawlCompleted(Object sender, GoesEventArgs e)
        {
            mThreadCount--;
            mCrawlCount++;
            MethodInvoker inv = delegate
            {
                GoesEventArgs g = (GoesEventArgs)e;
                lblThreadCount.Text = mThreadCount.ToString();
                lblCrawlCount.Text = mCrawlCount.ToString();
                lblLastCrawlTime.Text = DateTime.Now.ToLongTimeString();
                
            };

            this.Invoke(inv);
        }

        // This is how we start multiple, parallel crawls; each on its own thread
        private bool StartMultipleSiteCrawls()
        {
            // Reset the Thrad Count
            mThreadCount = 0;

            // Get the users than are ready for crawls
            MySqlDataReader drUserList = GetCrawlList();

            // If there are users to crawl, we start processing.
            if (drUserList != null)
            {
                if (drUserList.HasRows)
                {
                    while (drUserList.Read())
                    {
                        // Start the new Thread
                        ParameterizedThreadStart ptsd = new ParameterizedThreadStart(GoesCrawler.AppCrawlSite);
                        Thread thread = new Thread(ptsd);
                        Int32 x = drUserList.GetInt32(0);
                        thread.Start(drUserList.GetInt32(0));

                        // Update the UI
                        mThreadCount++;
                        lblThreadCount.Text = mThreadCount.ToString();
                    }
                }
            }

            drUserList.Close();

            return false;
        }

        // Search the database for users that need to be processed.
        public MySqlDataReader GetCrawlList()
        {

            // Open the connection
            MySqlConnection connMySQL;
            string myConnectionString;

            // Create a new TripleDESCryptoServiceProvider object
            // to generate a key and initialization vector (IV).
            TrippleDESCSPCoder coder = new TrippleDESCSPCoder();
            mEncryptedData = System.Convert.FromBase64String(Properties.Settings.Default.MySQL);
            myConnectionString = TrippleDESCSPCoder.DecryptTextFromMemory(mEncryptedData, coder.mEncryptKey, coder.mEncryptVector);

            try
            {
                connMySQL = new MySqlConnection();
                connMySQL.ConnectionString = myConnectionString;
                connMySQL.Open();
            }
            catch (MySqlException ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }

            // SQL Statement to retrieve the users who need to be processed
            String strSQL = "select distinct u.ID from wp_xleb_users u ";
            strSQL += "left join wp_xleb_usermeta m on m.user_id = u.ID and m.meta_key = 'goes_status' ";
            strSQL += "left join goes_log l on u.ID = l.user_id ";
            strSQL += "where (m.meta_value = '1') ";
            strSQL += "and(l.last_run_date is null or(minute(timediff(last_run_date, now())) > 15)) ";
//            strSQL += "and l.user_id = 61";

            // TODO: Remove this.
            // strSQL += "ORDER BY ID Desc LIMIT 1;";

            // Run the Query
            MySqlCommand cmd = new MySqlCommand();
            cmd.Connection = connMySQL;
            cmd.CommandType = CommandType.Text;
            cmd.CommandText = strSQL;
            MySqlDataReader dr;
            try
            {
                dr = cmd.ExecuteReader();
            }
            catch (MySqlException ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }

            return dr;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            StartMultipleSiteCrawls();
        }

        private void cmdStop_Click(object sender, EventArgs e)
        {
            timer1.Enabled = false;
            lblStatus.Text = "Stopped";
        }

        private void label3_Click(object sender, EventArgs e)
        {

        }

        private void frmGOES_Load(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            GoesCrawler.SendEmail(DateTime.Now.ToShortDateString(), DateTime.Now.ToShortTimeString(), "bob@leroynet.com");
            GoesCrawler.SendErrorEmail("Processing Error Message", "bob@leroynet.com");
            GoesCrawler.SendSupportEmail("this is the Support error message");
        }

        private void cmdEncrypt_Click(object sender, EventArgs e)
        {
            // Create a new TrippleDESCPCoder object 
            // to generate my private key and initialization vector (IV).
            TrippleDESCSPCoder coder = new TrippleDESCSPCoder();

            mEncryptedData = TrippleDESCSPCoder.EncryptTextToMemory(txtEncrypt.Text, coder.mEncryptKey, coder.mEncryptVector);
            txtDecrypt.Text = System.Convert.ToBase64String(mEncryptedData);
            txtEncrypt.Text = "";
        }

        private void cmdDecrypt_Click(object sender, EventArgs e)
        {

            // Create a new TripleDESCryptoServiceProvider object
            // to generate a key and initialization vector (IV).
            TrippleDESCSPCoder coder = new TrippleDESCSPCoder();

            mEncryptedData = System.Convert.FromBase64String(txtDecrypt.Text);
            txtEncrypt.Text = TrippleDESCSPCoder.DecryptTextFromMemory(mEncryptedData, coder.mEncryptKey, coder.mEncryptVector);
            txtDecrypt.Text = "";

        }
    }
}
