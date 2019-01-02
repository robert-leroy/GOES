using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;

namespace GOES_Application
{
    public class GoesUser
    {
        public enum AppType {Desktop, Server};
        public Int32 mUser_id;
        public String mUsername;
        public String mPassword;
        public String mLocation_id;
        public String mLocation_Name;
        public String mEmail;
        public AppType mAppType;

        public Boolean GetDataForUser(Int32 pUser)
        {
            MySqlConnection connMySQL;
            string myConnectionString;

            // Create a new TripleDESCryptoServiceProvider object
            // to generate a key and initialization vector (IV).
            TrippleDESCSPCoder coder = new TrippleDESCSPCoder();
            byte[] mEncryptedData = System.Convert.FromBase64String(Properties.Settings.Default.MySQL);
            myConnectionString = TrippleDESCSPCoder.DecryptTextFromMemory(mEncryptedData, coder.mEncryptKey, coder.mEncryptVector);


            try
            {
                connMySQL = new MySqlConnection();
                connMySQL.ConnectionString = myConnectionString;
                connMySQL.Open();
            }
            catch (MySql.Data.MySqlClient.MySqlException ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }

            // SQL Statement to retrieve the Order Header
            String strSQL = "SELECT ";
            strSQL += "MAX(CASE WHEN meta_key = 'goes_username' THEN meta_value ELSE NULL END) AS goes_username, ";
            strSQL += "MAX(CASE WHEN meta_key = 'goes_password' THEN meta_value ELSE NULL END) AS goes_password, ";
            strSQL += "MAX(CASE WHEN meta_key = 'goes_location' THEN meta_value ELSE NULL END) AS goes_location, ";
            strSQL += "user_email ";
            strSQL += "from wp_xleb_usermeta m ";
            strSQL += "join wp_xleb_users u on m.user_id = u.ID ";
            strSQL += "where user_id = " + pUser;
            strSQL += " and meta_key in ('goes_username', 'goes_password', 'goes_location'); ";

            // Execute the SQL
            MySqlCommand cmd = connMySQL.CreateCommand();
            cmd.CommandText = strSQL;
            MySqlDataReader dr;

            try
            {
                dr = cmd.ExecuteReader();
            }
            catch (MySqlException ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }


            if (dr.HasRows)
            {
                dr.Read();
                mUsername = dr.GetString(0);
                mUser_id = pUser;
                try
                {
                    mPassword = dr.GetString(1);
                    mLocation_id = dr.GetString(2);
                    mEmail = dr.GetString(3);
                }
                catch(Exception ex)
                {
                    return false;
                }
            }
            dr.Close();

            strSQL = "SELECT goes_location FROM goes_location WHERE location_id = " + mLocation_id;
            cmd.CommandText = strSQL;
            
            try
            {
                dr = cmd.ExecuteReader();
            }
            catch (MySqlException ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }


            if (dr.HasRows)
            {
                dr.Read();
                mLocation_Name = dr.GetString(0);
            }
            dr.Close();

            return true;
        }

        public Boolean CheckUserRegistration()
        {

            string strStatus = "https://www.globalentryinterview.com/CheckUserRegistration.php?user_id=" + mUsername;
            XmlDocument myXmlDocument = new XmlDocument();
            myXmlDocument.Load(strStatus); //Load NOT LoadXml

            return true;
            /*
            if (myXmlDocument.InnerText == "1")
                return true;
            else
                return false;
            */
        }

    }
}
