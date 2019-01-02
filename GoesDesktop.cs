using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
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
using GOES_Application;

namespace GOES_Application
{
    public partial class frmGOESDesktop : Form
    {
        private int mCrawlCount;
        private bool bGoButtonFlag;

        public frmGOESDesktop()
        {
            InitializeComponent();
            GoesCrawler.CrawlComplete += HandleCrawlCompleted;
            mCrawlCount = 0;
            bGoButtonFlag = true;

            txtUsername.Text = Properties.Settings.Default.GoesUserName;
            txtPassword.Text = Properties.Settings.Default.GoesPassword;
            cmboLocation.SelectedItem = Properties.Settings.Default.GoesLocation;

            if (Properties.Settings.Default.FirstTime != "1")
            { 
                FirefoxWarning dlg = new FirefoxWarning();
                dlg.ShowDialog();
                if (dlg.chkDismissMessage.Checked)
                {
                    Properties.Settings.Default["FirstTime"] = "1";
                    Properties.Settings.Default.Save();
                }
            }

            timerCrawl.Interval = trackBarTimer.Value * 60000;
        }

        // When the Go button is clicked we need to start the Timer and start the first crawl.
        private void cmdGo_Click(object sender, EventArgs e)
        {
            
            if (bGoButtonFlag == true)
            {

                if (txtUsername.Text.Trim().Length == 0)
                {
                    MessageBox.Show("Please enter your Global Entry website username.");
                    return;
                }

                if (txtPassword.Text.Trim().Length == 0)
                {
                    MessageBox.Show("Please enter your Global Entry website password.");
                    return;
                }

                if (cmboLocation.SelectedIndex == -1)
                {
                    MessageBox.Show("Please select your preferred interview location.");
                    return;
                }

                bGoButtonFlag = false;
                timerCrawl.Enabled = true;
                trackBarTimer.Enabled = false;
                txtPassword.Enabled = false;
                txtUsername.Enabled = false;
                cmboLocation.Enabled = false;
                btnViewPassword.Enabled = false;
                lblStatus.Text = "Running";
                cmdGo.Text = "Stop";

                StartSingleSiteCrawl();

            }
            else
            {
                bGoButtonFlag = true;
                timerCrawl.Enabled = false;
                timerVisuals.Enabled = false;
                trackBarTimer.Enabled = true;
                txtPassword.Enabled = true;
                txtUsername.Enabled = true;
                cmboLocation.Enabled = true;
                btnViewPassword.Enabled = true;
                lblStatus.Text = "Stopped";
                cmdGo.Text = "Go";
                barTime.Minimum = barTime.Maximum = barTime.Value = 0;
            }

        }

        // This function is called when a crawl is complete
        public void HandleCrawlCompleted(Object sender, GoesEventArgs e)
        {
            MethodInvoker inv = delegate
            {
                mCrawlCount++;
                lblCrawlCount.Text = mCrawlCount.ToString();
                lblLastCrawlTime.Text = DateTime.Now.ToLongTimeString();
                if (e != null)
                    lblLastDateFound.Text = e.GoesResult;
                lblStatus.Text = "Waiting for timer";
                barTime.Minimum = 0;
                barTime.Maximum = timerCrawl.Interval;
                barTime.Value = barTime.Maximum;
                timerVisuals.Enabled = true;
            };

               this.Invoke(inv);
        }

        // This is how we start multiple, parallel crawls; each on its own thread
        public bool StartSingleSiteCrawl()
        {

            // Validate the user info
            GoesUser pUser = new GoesUser();
            pUser.mUser_id = 0;
            pUser.mUsername = txtUsername.Text;
            pUser.mPassword = txtPassword.Text;
            pUser.mLocation_Name = cmboLocation.SelectedItem.ToString();

            // Check the registration database
            if (pUser.CheckUserRegistration())
            {
                // Start the new Thread
                ParameterizedThreadStart ptsd = new ParameterizedThreadStart(GoesCrawler.DesktopCrawlSite);
                Thread thread = new Thread(ptsd);

                thread.Start(pUser);

                lblStatus.Text = "Running";
                timerVisuals.Enabled = false;

                return false;
            }
            else
            {
                // Invalid user. 
                MessageBox.Show("The username isn not valid.  You need to regiter at http://www.globalEntryInterview.com.", "Validation Error");
                return false;
            }
        }

        private void trackBarTimer_ValueChanged(object sender, EventArgs e)
        {
            timerCrawl.Interval = trackBarTimer.Value * 60000;
        }

        private void btnViewPassword_Click(object sender, EventArgs e)
        {
            if (txtPassword.UseSystemPasswordChar == true)
            {
                txtPassword.UseSystemPasswordChar = false;
                btnViewPassword.Text = "H";
            }
            else
            {
                txtPassword.UseSystemPasswordChar = true;
                btnViewPassword.Text = "V";
            }
        }
        
        private void timer1_Tick(object sender, EventArgs e)
        {
            StartSingleSiteCrawl();
        }

        private void frmGOESDesktop_FormClosed(object sender, FormClosedEventArgs e)
        {
            Properties.Settings.Default.GoesUserName = txtUsername.Text;
            Properties.Settings.Default.GoesPassword = txtPassword.Text;
            if (cmboLocation.SelectedIndex > 0)
                Properties.Settings.Default.GoesLocation = cmboLocation.SelectedItem.ToString();
            Properties.Settings.Default.Save();
        }

        private void timerVisuals_Tick(object sender, EventArgs e)
        {
            barTime.Value -= 1000;
        }
    }
}
