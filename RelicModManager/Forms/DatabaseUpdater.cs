﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Linq;

namespace RelhaxModpack
{
    public partial class DatabaseUpdater : RelhaxForum
    {
        private enum AuthLevel
        {
            None=0,
            View=1,
            UpdateDatabase=2,
            Admin=3
        }
        private AuthLevel CurrentAuthLevel = AuthLevel.None;
        private WebClient downloader;
        private List<Dependency> globalDependencies;
        private List<Dependency> dependencies;
        private List<LogicalDependency> logicalDependencies;
        private List<Category> parsedCatagoryList;
        private StringBuilder globalDepsSB = new StringBuilder();
        private StringBuilder dependenciesSB = new StringBuilder();
        private StringBuilder logicalDependenciesSB = new StringBuilder();
        private StringBuilder packagesSB = new StringBuilder();
        private StringBuilder filesNotFoundSB = new StringBuilder();
        private Hashtable HelpfullMessages = new Hashtable();
        private string level1Password = "RelhaxReadOnly2018";
        private const string L2keyFileName = "L2Key.txt";
        private const string L3keyFileName = "L3key.txt";
        private const string L2KeyAddress = "aHR0cDovL3dvdG1vZHMucmVsaGF4bW9kcGFjay5jb20vUmVsaGF4TW9kcGFjay9SZXNvdXJjZXMvZXh0ZXJuYWwvTDJLZXkudHh0";
        private const string L3KeyAddress = "aHR0cDovL3dvdG1vZHMucmVsaGF4bW9kcGFjay5jb20vUmVsaGF4TW9kcGFjay9SZXNvdXJjZXMvZXh0ZXJuYWwvTDNrZXkudHh0";
        private const string ModpackUsername = "modpack@wotmods.relhaxmodpack.com";
        private const string ModpackPassword = "QjFZLi0zaGxsTStY";
        private string ModpackPasswordDecoded = "";
        private const string backupFoldersLocation = "ftp://wotmods.relhaxmodpack.com/RelhaxModpack/Resources/modInfoBackups/";
        private const string modInfosLocation = "ftp://wotmods.relhaxmodpack.com/RelhaxModpack/Resources/modInfo/";
        private const string ftpModpackRoot = "ftp://wotmods.relhaxmodpack.com/RelhaxModpack/";
        private const string ftpRoot = "ftp://wotmods.relhaxmodpack.com/";
        private NetworkCredential credentials;
        private string savedDatabaseVersion = "";

        public DatabaseUpdater()
        {
            InitializeComponent();
        }

        private void OnAuthLevelChange(AuthLevel al)
        {
            CurrentAuthLevel = al;
            //disable everything first, then enable via falling case statements
            foreach (Control c in UpdateDatabaseTab.Controls)
                c.Enabled = false;
            foreach (Control c in UpdateApplicationTab.Controls)
                c.Enabled = false;
            foreach (Control c in CleanOnlineFolders.Controls)
                c.Enabled = false;
            if((int)CurrentAuthLevel > 1)
            {
                foreach (Control c in UpdateDatabaseTab.Controls)
                    c.Enabled = true;
                UpdateDatabaseStep3Advanced.Enabled = false;
            }
            if((int)CurrentAuthLevel > 2)
            {
                UpdateDatabaseStep3Advanced.Enabled = true;
                foreach (Control c in UpdateApplicationTab.Controls)
                    c.Enabled = true;
                foreach (Control c in CleanOnlineFolders.Controls)
                    c.Enabled = true;
            }
            AuthStatusLabel.Text = CurrentAuthLevel.ToString();
        }

        private void CRCFileSizeUpdate_Load(object sender, EventArgs e)
        {
            loadDatabaseDialog.InitialDirectory = Application.StartupPath;
            ModpackPasswordDecoded = Utils.Base64Decode(ModpackPassword);
            credentials = new NetworkCredential(ModpackUsername, ModpackPasswordDecoded);
            FillHashTable();
            //DEBUG ONLY
            OnAuthLevelChange(AuthLevel.Admin);
        }

        #region Database Updating
        private void UpdateDatabaseStep1_Click(object sender, EventArgs e)
        {
            if (loadDatabaseDialog.ShowDialog() == DialogResult.Cancel)
                return;
            DatabaseLocationTextBox.Text = loadDatabaseDialog.FileName;
        }

        private void UpdateDatabaseStep2_Click(object sender, EventArgs e)
        {
            ScriptLogOutput.Text = "Running script CreateDatabase.php...";
            using (WebClient client = new WebClient())
            {
                client.DownloadStringCompleted += Client_DownloadStringCompleted;
                //ScriptLogOutput.Text = client.DownloadString("http://wotmods.relhaxmodpack.com/scripts/CreateDatabase.php").Replace("<br />", "\n");
                client.DownloadStringAsync(new Uri("http://wotmods.relhaxmodpack.com/scripts/CreateDatabase.php"));
            }
        }

        private void UpdateDatabaseStep3_Click(object sender, EventArgs e)
        {
            // check for database
            if (!File.Exists(DatabaseLocationTextBox.Text))
                return;
            // read onlineFolder of the selected local modInfo.xml to get the right online database.xml
            Settings.TanksOnlineFolderVersion = XMLUtils.GetXMLElementAttributeFromFile(DatabaseLocationTextBox.Text, "//modInfoAlpha.xml/@onlineFolder");
            // read gameVersion of the selected local modInfo.xml
            Settings.TanksVersion = XMLUtils.GetXMLElementAttributeFromFile(DatabaseLocationTextBox.Text, "//modInfoAlpha.xml/@version");
            Logging.Manager(string.Format("working with game version: {0}, located at online Folder: {1}", Settings.TanksVersion, Settings.TanksOnlineFolderVersion));
            ScriptLogOutput.AppendText(string.Format("working with game version: {0}, located at online Folder: {1}\n", Settings.TanksVersion, Settings.TanksOnlineFolderVersion));
            // download online database.xml
            try
            {
                using (downloader = new WebClient())
                {
                    string address = string.Format("http://wotmods.relhaxmodpack.com/WoT/{0}/database.xml", Settings.TanksOnlineFolderVersion);
                    string fileName = Path.Combine(Application.StartupPath, "RelHaxTemp", Settings.OnlineDatabaseXmlFile);
                    downloader.DownloadFile(address, fileName);
                }
            }
            catch (Exception ex)
            {
                Utils.ExceptionLog("loadZipFilesButton_Click", string.Format("http://wotmods.relhaxmodpack.com/WoT/{0}/database.xml", Settings.TanksOnlineFolderVersion), ex);
                MessageBox.Show("FAILED to download online file database");
                Application.Exit();
            }
            ScriptLogOutput.AppendText("database.xml downloaded\n");
            // set this flag, so getMd5Hash and getFileSize should parse downloaded online database.xml
            Program.databaseUpdateOnline = true;
            filesNotFoundSB.Clear();
            globalDepsSB.Clear();
            dependenciesSB.Clear();
            logicalDependenciesSB.Clear();
            packagesSB.Clear();
            //load database
            globalDependencies = new List<Dependency>();
            parsedCatagoryList = new List<Category>();
            dependencies = new List<Dependency>();
            logicalDependencies = new List<LogicalDependency>();
            XMLUtils.CreateModStructure(DatabaseLocationTextBox.Text, globalDependencies, dependencies, logicalDependencies, parsedCatagoryList);
            //check for duplicates
            int duplicatesCounter = 0;
            if (Utils.Duplicates(parsedCatagoryList) && Utils.DuplicatesPackageName(parsedCatagoryList, ref duplicatesCounter ))
            {
                MessageBox.Show(string.Format("{0} duplicates found !!!",duplicatesCounter));
                Program.databaseUpdateOnline = false;
                return;
            }
            ScriptLogOutput.AppendText("Updating database...\n");
            Application.DoEvents();
            filesNotFoundSB.Append("FILES NOT FOUND:\n");
            globalDepsSB.Append("\nGlobal Dependencies updated:\n");
            dependenciesSB.Append("\nDependencies updated:\n");
            logicalDependenciesSB.Append("\nLogical Dependencies updated:\n");
            packagesSB.Append("\nPackages updated:\n");
            string hash;
            //foreach zip file name
            foreach (Dependency d in globalDependencies)
            {
                if (d.ZipFile.Trim().Equals(""))
                {
                    d.CRC = "";
                }
                else
                {
                    hash = XMLUtils.GetMd5Hash(d.ZipFile);
                    if (!d.CRC.Equals(hash))
                    {
                        d.CRC = hash;
                        if (!hash.Equals("f"))
                        {
                            globalDepsSB.Append(d.ZipFile + "\n");
                        }
                    }
                    if (hash.Equals("f"))
                    {
                        filesNotFoundSB.Append(d.ZipFile + "\n");
                    }
                }
            }
            foreach (Dependency d in dependencies)
            {
                if (d.ZipFile.Trim().Equals(""))
                {
                    d.CRC = "";
                }
                else
                {
                    hash = XMLUtils.GetMd5Hash(d.ZipFile);
                    if (!d.CRC.Equals(hash))
                    {
                        d.CRC = hash;
                        if (!hash.Equals("f"))
                        {
                            dependenciesSB.Append(d.ZipFile + "\n");
                        }
                    }
                    if (hash.Equals("f"))
                    {
                        filesNotFoundSB.Append(d.ZipFile + "\n");
                    }
                }
            }
            foreach (LogicalDependency d in logicalDependencies)
            {
                if (d.ZipFile.Trim().Equals(""))
                {
                    d.CRC = "";
                }
                else
                {
                    hash = XMLUtils.GetMd5Hash(d.ZipFile);
                    if (!d.CRC.Equals(hash))
                    {
                        d.CRC = hash;
                        if (!hash.Equals("f"))
                        {
                            logicalDependenciesSB.Append(d.ZipFile + "\n");
                        }
                    }
                    if (hash.Equals("f"))
                    {
                        filesNotFoundSB.Append(d.ZipFile + "\n");
                    }
                }
            }
            foreach (Category c in parsedCatagoryList)
            {
                foreach (SelectablePackage m in c.Packages)
                {
                    if (m.ZipFile.Trim().Equals(""))
                    {
                        m.CRC = "";
                    }
                    else
                    {
                        m.Size = this.getFileSize(m.ZipFile);
                        hash = XMLUtils.GetMd5Hash(m.ZipFile);
                        if (!m.CRC.Equals(hash))
                        {
                            m.CRC = hash;

                            if (!hash.Equals("f"))
                            {
                                packagesSB.Append(m.ZipFile + "\n");
                            }
                        }
                        if (hash.Equals("f"))
                        {
                            filesNotFoundSB.Append(m.ZipFile + "\n");
                        }
                    }
                    if (m.Packages.Count > 0)
                    {
                        this.processConfigsCRCUpdate(m.Packages);
                    }
                }
            }
            //update the CRC value
            //update the file size
            //save config file
            XMLUtils.SaveDatabase(DatabaseLocationTextBox.Text, Settings.TanksVersion, Settings.TanksOnlineFolderVersion, globalDependencies, dependencies, logicalDependencies, parsedCatagoryList);
            //MessageBox.Show(filesNotFoundSB.ToString() + globalDepsSB.ToString() + dependenciesSB.ToString() + logicalDependenciesSB.ToString() + modsSB.ToString() + configsSB.ToString());
            ScriptLogOutput.AppendText(filesNotFoundSB.ToString() + globalDepsSB.ToString() + dependenciesSB.ToString() + logicalDependenciesSB.ToString() + packagesSB.ToString());
            Program.databaseUpdateOnline = false;
        }

        private void UpdateDatabaseStep3Advanced_Click(object sender, EventArgs e)
        {
            // check for database
            if (!File.Exists(DatabaseLocationTextBox.Text))
                return;
            //show file dialog
            if (addZipsDialog.ShowDialog() == DialogResult.Cancel)
                return;
            globalDepsSB.Clear();
            dependenciesSB.Clear();
            packagesSB.Clear();
            //load database
            globalDependencies = new List<Dependency>();
            parsedCatagoryList = new List<Category>();
            dependencies = new List<Dependency>();
            logicalDependencies = new List<LogicalDependency>();
            Settings.TanksVersion = XMLUtils.GetXMLElementAttributeFromFile(DatabaseLocationTextBox.Text, "//modInfoAlpha.xml/@version");
            Settings.TanksOnlineFolderVersion = XMLUtils.GetXMLElementAttributeFromFile(DatabaseLocationTextBox.Text, "//modInfoAlpha.xml/@onlineFolder");
            XMLUtils.CreateModStructure(DatabaseLocationTextBox.Text, globalDependencies, dependencies, logicalDependencies, parsedCatagoryList);
            int duplicatesCounter = 0;
            //check for duplicates
            if (Utils.Duplicates(parsedCatagoryList) && Utils.DuplicatesPackageName(parsedCatagoryList, ref duplicatesCounter))
            {
                MessageBox.Show(string.Format("{0} duplicates found !!!", duplicatesCounter));
                return;
            }
            ScriptLogOutput.Text = "Updating database...";
            Application.DoEvents();
            globalDepsSB.Append("Global Dependencies updated:\n");
            dependenciesSB.Append("Dependencies updated:\n");
            packagesSB.Append("Packages updated:\n");
            //foreach zip file name
            foreach (Dependency d in globalDependencies)
            {
                int index = this.getZipIndex(d.ZipFile);
                if (index == -1)
                {
                    continue;
                }
                if (d.CRC == null || d.CRC.Equals("") || d.CRC.Equals("f"))
                {
                    d.CRC = Utils.CreateMd5Hash(addZipsDialog.FileNames[index]);
                    globalDepsSB.Append(d.ZipFile + "\n");
                }
            }
            foreach (Dependency d in dependencies)
            {
                int index = this.getZipIndex(d.ZipFile);
                if (index == -1)
                {
                    continue;
                }
                if (d.CRC == null || d.CRC.Equals("") || d.CRC.Equals("f"))
                {
                    d.CRC = Utils.CreateMd5Hash(addZipsDialog.FileNames[index]);
                    dependenciesSB.Append(d.ZipFile + "\n");
                }
            }
            foreach (Category c in parsedCatagoryList)
            {
                foreach (SelectablePackage m in c.Packages)
                {
                    int index = this.getZipIndex(m.ZipFile);
                    if (index != -1)
                    {
                        m.Size = this.getFileSize(addZipsDialog.FileNames[index]);
                        if (m.CRC == null || m.CRC.Equals("") || m.CRC.Equals("f"))
                        {
                            m.CRC = Utils.CreateMd5Hash(addZipsDialog.FileNames[index]);

                            packagesSB.Append(m.ZipFile + "\n");
                        }
                    }
                    if (m.Packages.Count > 0)
                    {
                        this.processConfigsCRCUpdate_old(m.Packages);
                    }
                }
            }
            //update the CRC value
            //update the file size
            //save config file
            XMLUtils.SaveDatabase(DatabaseLocationTextBox.Text, Settings.TanksVersion, Settings.TanksOnlineFolderVersion, globalDependencies, dependencies, logicalDependencies, parsedCatagoryList);
            //MessageBox.Show(globalDepsSB.ToString() + dependenciesSB.ToString() + modsSB.ToString() + configsSB.ToString());
            ScriptLogOutput.Text = globalDepsSB.ToString() + dependenciesSB.ToString() + packagesSB.ToString();
        }


        private void UpdateDatabaseStep4_Click(object sender, EventArgs e)
        {
            //for the folder version: //modInfoAlpha.xml/@version
            Settings.TanksVersion = XMLUtils.GetXMLElementAttributeFromFile(DatabaseLocationTextBox.Text, "//modInfoAlpha.xml/@version");
            //for the onlineFolder version: //modInfoAlpha.xml/@onlineFolder
            Settings.TanksOnlineFolderVersion = XMLUtils.GetXMLElementAttributeFromFile(DatabaseLocationTextBox.Text, "//modInfoAlpha.xml/@onlineFolder");
            Text = String.Format("DatabaseUpdateUtility      TanksVersion: {0}    OnlineFolder: {1}", Settings.TanksVersion, Settings.TanksOnlineFolderVersion);
            using (downloader = new WebClient() { Credentials = credentials })
            {
                //check if folder exists first
                //get the list of all the folders based on game version
                ScriptLogOutput.AppendText("Checking if folder exists...");
                string[] folders = ListFilesFolders(backupFoldersLocation);
                if (!(folders.Contains(Settings.TanksVersion)))
                {
                    ScriptLogOutput.AppendText("Does NOT exist, creating\n");
                    //create the folder
                    MakeFTPFolder(backupFoldersLocation + Settings.TanksVersion);
                }
                else
                    ScriptLogOutput.AppendText("DOES exists\n");
                //check it for if multiple itterations in one day
                //rename it to format to the new date
                //MUST use EST timezone
                ReportProgress("Making new filename with EST timezone, checking if currently on server");
                int itteraton = 1;
                DateTime dt = DateTime.UtcNow;
                TimeZoneInfo EST = TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                DateTime realToday = TimeZoneInfo.ConvertTimeFromUtc(dt, EST);
                string newModInfoFileName = "modInfo_" + Settings.TanksVersion + "_";
                string dateTimeFormat = string.Format("{0:yyyy-MM-dd}", realToday);
                string realNewModInfoFileName = newModInfoFileName + dateTimeFormat + "_" + itteraton + ".xml";
                bool fileAlreadyExists = true;
                while(fileAlreadyExists)
                {
                    ReportProgress(string.Format("Checking if filename '{0}' exists in backup folder...", realNewModInfoFileName));
                    string[] modInfoBackups = ListFilesFolders(backupFoldersLocation + Settings.TanksVersion + "/");
                    if(modInfoBackups.Contains(realNewModInfoFileName))
                    {
                        ReportProgress("Exists, trying higher itteration");
                        realNewModInfoFileName = newModInfoFileName + dateTimeFormat + "_" + ++itteraton + ".xml";
                    }
                    else
                    {
                        ReportProgress("Does not exist!");
                        fileAlreadyExists = false;
                        savedDatabaseVersion = realNewModInfoFileName;
                    }
                }
                //download and rename the current one
                ReportProgress("Downloading latest database");
                string modInfoFileName = "modInfo_" + Settings.TanksVersion + ".xml";
                downloader.DownloadFile(ftpModpackRoot + "Resources/modInfo/" + modInfoFileName, modInfoFileName);
                ReportProgress("Reanimg file for backup...");
                File.Move(modInfoFileName, realNewModInfoFileName);
                //upload backup
                downloader.UploadFile(backupFoldersLocation + Settings.TanksVersion + "/" + realNewModInfoFileName, realNewModInfoFileName);
                File.Delete(realNewModInfoFileName);
                //delete current modInfo?

                //upload new modInfo
                downloader.UploadFile(modInfosLocation + modInfoFileName, DatabaseLocationTextBox.Text);
            }
        }

        private void UpdateDatabaseStep5_Click(object sender, EventArgs e)
        {
            if(string.IsNullOrWhiteSpace(Settings.TanksVersion))
            {
                ReportProgress("ERROR: Settings.TanksVersion is null or empty! (Did you run the previous processes first?)");
                return;
            }
            if(string.IsNullOrWhiteSpace(savedDatabaseVersion))
            {
                ReportProgress("ERROR: savedDatabaseVersion is null or empty! (Did you run the previous processes first?)");
                return;
            }
            string supportedClients = "supported_clients.xml";
            string managerVersion = "manager_version.xml";
            
            using (downloader = new WebClient() { Credentials = credentials })
            {
                //check if supported clients needs to be updated
                //download and check if update needed
                ReportProgress("Checking if supported_clients.xml already has entry for tanks version " + Settings.TanksVersion);
                if (File.Exists(supportedClients))
                    File.Delete(supportedClients);
                downloader.DownloadFile(ftpModpackRoot + "Resources/managerInfo/" + supportedClients, supportedClients);
                XmlDocument doc = new XmlDocument();
                doc.Load(supportedClients);
                XmlNodeList supported_clients = doc.SelectNodes("//versions/version");
                bool needsUpdate = true;
                foreach(XmlNode supported_client in supported_clients)
                {
                    if(supported_client.InnerText.Equals(Settings.TanksVersion))
                    {
                        needsUpdate = false;
                    }
                }
                if (needsUpdate)
                {
                    ReportProgress("Entry needed, creating and uploading...");
                    XmlNode versionRoot = doc.SelectSingleNode("//versions");
                    XmlElement supported_client = doc.CreateElement("version");
                    supported_client.InnerText = Settings.TanksVersion;
                    supported_client.SetAttribute("folder", Settings.TanksOnlineFolderVersion);
                    versionRoot.AppendChild(supported_client);
                    doc.Save(supportedClients);
                    downloader.UploadFile(ftpModpackRoot + "Resources/managerInfo/" + supportedClients, supportedClients);
                }
                else
                {
                    ReportProgress("Entry already exists");
                }
                File.Delete(supportedClients);
                //update manager_version.xml
                ReportProgress("Updating manager_version.xml");
                downloader.DownloadFile(ftpModpackRoot + "Resources/managerInfo/" + managerVersion, managerVersion);
                doc = new XmlDocument();
                doc.Load(managerVersion);
                XmlNode database_version_text = doc.SelectSingleNode("//version/database");
                string databaseString = savedDatabaseVersion.Substring(0, savedDatabaseVersion.Length - 4);
                databaseString = databaseString.Substring(6);
                database_version_text.InnerText = databaseString;
                doc.Save(managerVersion);
                downloader.UploadFile(ftpModpackRoot + "Resources/managerInfo/" + managerVersion, managerVersion);
                File.Delete(managerVersion);
            }
        }
        private void UpdateDatabaseStep6_Click(object sender, EventArgs e)
        {
            ScriptLogOutput.Text = "Running script CreateModInfo.php...";
            using (WebClient client = new WebClient())
            {
                //ScriptLogOutput.Text = client.DownloadString("http://wotmods.relhaxmodpack.com/scripts/CreateModInfo.php").Replace("<br />", "\n");
                client.DownloadStringAsync(new Uri("http://wotmods.relhaxmodpack.com/scripts/CreateModInfo.php"));
            }
        }

        private void UpdateDatabaseStep7_Click(object sender, EventArgs e)
        {
            ScriptLogOutput.Text = "Running script CreateServerInfo.php...";
            using (WebClient client = new WebClient())
            {
                //ScriptLogOutput.Text = client.DownloadString("http://wotmods.relhaxmodpack.com/scripts/CreateManagerInfo.php").Replace("<br />", "\n");
                client.DownloadStringAsync(new Uri("http://wotmods.relhaxmodpack.com/scripts/CreateManagerInfo.php"));
            }
        }

        /*
        private void UpdateDatabaseStep7_Click(object sender, EventArgs e)
        {
            ScriptLogOutput.Text = "Running script CreateOutDatesFilesList.php...";
            Application.DoEvents();
            using (WebClient client = new WebClient())
            {
                ScriptLogOutput.Text = client.DownloadString("http://wotmods.relhaxmodpack.com/scripts/CreateOutdatedFileList.php").Replace("<br />", "\n");
            }
            Application.DoEvents();
        }
        */
        #endregion

        #region Application Updating

        #endregion

        #region Online Folder Cleaning
        private void CleanFoldersStep1_Click(object sender, EventArgs e)
        {

        }

        private void CleanFoldersStep2_Click(object sender, EventArgs e)
        {

        }
        #endregion

        #region FTP methods
        private void MakeFTPFolder(string addressWithDirectory)
        {
            WebRequest folderRequest = WebRequest.Create(addressWithDirectory);
            folderRequest.Method = WebRequestMethods.Ftp.MakeDirectory;
            folderRequest.Credentials = credentials;
            using (FtpWebResponse response = (FtpWebResponse)folderRequest.GetResponse())
            { }
        }

        private string[] ListFilesFolders(string address)
        {
            WebRequest folderRequest = WebRequest.Create(address);
            folderRequest.Method = WebRequestMethods.Ftp.ListDirectory;
            folderRequest.Credentials = credentials;
            using (FtpWebResponse response = (FtpWebResponse)folderRequest.GetResponse())
            {
                Stream responseStream = response.GetResponseStream();
                StreamReader reader = new StreamReader(responseStream);
                string temp = reader.ReadToEnd();
                return temp.Split(new[] { "\r\n" }, StringSplitOptions.None);
            }
        }
        #endregion

        #region Authorization
        private void RequestL1AuthButton_Click(object sender, EventArgs e)
        {
            ScriptLogOutput.Text = "Verifying...";
            if (L1AuthPasswordAttempt.Text.Equals(level1Password))
            {
                MessageBox.Show("Success!");
                OnAuthLevelChange(AuthLevel.View);
            }
            else
            {
                MessageBox.Show("Denied!");
            }
            ScriptLogOutput.Text = "";
        }

        private void RequestL2AuthButton_Click(object sender, EventArgs e)
        {
            ScriptLogOutput.Text = "Verifying...";
            string downloadedKey = "";
            using (downloader = new WebClient())
            {
                downloadedKey = Utils.Base64Decode(downloader.DownloadString(Utils.Base64Decode(L2KeyAddress)));
            }
            if(downloadedKey.Equals(L2PasswordAttempt.Text))
            {
                MessageBox.Show("Success!");
                OnAuthLevelChange(AuthLevel.UpdateDatabase);
            }
            else
            {
                MessageBox.Show("Denied!");
            }
            ScriptLogOutput.Text = "";
        }

        private void RequestL3AuthButton_Click(object sender, EventArgs e)
        {
            ScriptLogOutput.Text = "Verifying...";
            string downloadedKey = "";
            using (downloader = new WebClient())
            {
                downloadedKey = Utils.Base64Decode(downloader.DownloadString(Utils.Base64Decode(L3KeyAddress)));
            }
            if (downloadedKey.Equals(L3PasswordAttempt.Text))
            {
                MessageBox.Show("Success!");
                OnAuthLevelChange(AuthLevel.Admin);
            }
            else
            {
                MessageBox.Show("Denied!");
            }
            ScriptLogOutput.Text = "";
        }

        private void GenerateL2Password_Click(object sender, EventArgs e)
        {
            if (File.Exists(L2keyFileName))
                File.Delete(L2keyFileName);
            File.WriteAllText(L2keyFileName, Utils.Base64Encode(L2TextPassword.Text));
            L2GenerateOutput.Text = "Password key file saved to" + L2keyFileName;
        }

        private void GenerateL3Password_Click(object sender, EventArgs e)
        {
            if (File.Exists(L3keyFileName))
                File.Delete(L3keyFileName);
            File.WriteAllText(L3keyFileName, Utils.Base64Encode(L3TextPassword.Text));
            L3GenerateOutput.Text = "Password key file saved to " + L3keyFileName;
        }

        private void ReadbackL2Password_Click(object sender, EventArgs e)
        {
            if(!File.Exists(L2keyFileName))
            {
                L2GenerateOutput.Text = L2keyFileName + " does not exist!";
            }
            else
            {
                L2GenerateOutput.Text = "L2 Key read as " + Utils.Base64Decode(File.ReadAllText(L2keyFileName));
            }
        }

        private void ReadbackL3Password_Click(object sender, EventArgs e)
        {
            if (!File.Exists(L3keyFileName))
            {
                L3GenerateOutput.Text = L3keyFileName + " does not exist!";
            }
            else
            {
                L3GenerateOutput.Text = "L3 Key read as " + Utils.Base64Decode(File.ReadAllText(L3keyFileName));
            }
        }
        #endregion

        #region Boring methods
        private void ReportProgress(string message)
        {
            //reports to the log file and the console otuptu
            Logging.Manager(message);
            ScriptLogOutput.AppendText(message + "\n");
        }

        private void DatabaseUpdateTabControl_Selected(object sender, TabControlEventArgs e)
        {
            if (CurrentAuthLevel == AuthLevel.None)
                DatabaseUpdateTabControl.SelectedTab = AuthStatus;
        }

        private void Generic_MouseLeave(object sender, EventArgs e)
        {
            InfoTB.Text = "";
        }

        private void Generic_MouseEnter(object sender, EventArgs e)
        {
            Control c = (Control)sender;
            InfoTB.Text = (string)HelpfullMessages[c.Name];
        }

        private void Client_DownloadStringCompleted(object sender, DownloadStringCompletedEventArgs e)
        {
            ScriptLogOutput.Text = e.Result.Replace("<br />", "\n");
        }

        private void processConfigsCRCUpdate_old(List<SelectablePackage> cfgList)
        {
            foreach (SelectablePackage cat in cfgList)
            {
                int cindex = this.getZipIndex(cat.ZipFile);
                if (cindex != -1)
                {
                    cat.Size = this.getFileSize(addZipsDialog.FileNames[cindex]);
                    if (cat.CRC == null || cat.CRC.Equals("") || cat.CRC.Equals("f"))
                    {
                        cat.CRC = Utils.CreateMd5Hash(addZipsDialog.FileNames[cindex]);

                        packagesSB.Append(cat.ZipFile + "\n");
                    }
                }
                if (cat.Packages.Count > 0)
                {
                    this.processConfigsCRCUpdate_old(cat.Packages);
                }
            }
        }

        private int getZipIndex(string zipFile)
        {
            for (int i = 0; i < addZipsDialog.FileNames.Count(); i++)
            {
                string fileName = Path.GetFileName(addZipsDialog.FileNames[i]);
                if (fileName.Equals(zipFile))
                    return i;
            }
            return -1;
        }

        private void CRCFileSizeUpdate_FormClosing(object sender, FormClosingEventArgs e)
        {
            Logging.Manager("|------------------------------------------------------------------------------------------------|");
        }

        private void processConfigsCRCUpdate(List<SelectablePackage> cfgList)
        {
            string hash;
            foreach (SelectablePackage cat in cfgList)
            {
                if (cat.ZipFile.Trim().Equals(""))
                {
                    cat.CRC = "";
                }
                else
                {
                    hash = XMLUtils.GetMd5Hash(cat.ZipFile);
                    cat.Size = this.getFileSize(cat.ZipFile);
                    if (cat.Size != 0)
                    {
                        if (!cat.CRC.Equals(hash))
                        {
                            cat.CRC = hash;
                            if (!hash.Equals("f"))
                            {
                                packagesSB.Append(cat.ZipFile + "\n");
                            }
                        }
                    }
                    else
                    {
                        cat.CRC = "f";
                    }
                    if (hash.Equals("f") | cat.CRC.Equals("f"))
                    {
                        filesNotFoundSB.Append(cat.ZipFile + "\n");
                    }

                }
                if (cat.Packages.Count > 0)
                {
                    this.processConfigsCRCUpdate(cat.Packages);
                }
            }
        }

        private Int64 getFileSize(string file)
        {
            Int64 fileSizeBytes = 0;
            if (Program.databaseUpdateOnline)
            {
                try
                {
                    XDocument doc = XDocument.Load(Settings.OnlineDatabaseXmlFile);
                    try
                    {
                        XElement element = doc.Descendants("file")
                           .Where(arg => arg.Attribute("name").Value == file)
                           .Single();
                        Int64.TryParse(element.Attribute("size").Value, out fileSizeBytes);
                    }
                    catch (InvalidOperationException)
                    {
                        // catch the Exception if no entry is found
                    }
                }
                catch (Exception ex)
                {
                    Utils.ExceptionLog("getFileSize", "read from onlineDatabaseXml: " + file, ex);
                }
            }
            else
            {
                try
                {
                    FileInfo fi = new FileInfo(file);
                    fileSizeBytes = fi.Length;
                }
                catch (Exception ex)
                {
                    Utils.ExceptionLog("getFileSize", "FileInfo from local file: " + file, ex);
                }
            }
            try
            {
                return fileSizeBytes;
            }
            catch (Exception ex)
            {
                Utils.ExceptionLog("getFileSize", "building format", ex);
            }
            return 0;
        }

        private void FillHashTable()
        {
            string serverInfo = "creating the manageInfo.dat file, containing the files: " +
            "\nmanager_version.xml\n" +
            "supported_clients.xml\n" +
            "databaseUpdate.txt\n" +
            "releaseNotes.txt\n" +
            "releaseNotes_beta.txt\n" +
            "default_checked.xml";
            string database = "creating the database.xml file at every online version folder of WoT, containing the filename, size and MD5Hash of " +
                "the current folder, the script \"CreateMD5List.php\" is a needed subscript of CreateDatabase.php, \"relhax_db.sqlite\" is the needed sqlite database to " +
                "be fast on parsing all files and only working on new or changed files";
            string modInfo = "creating the modInfo.dat file at every online version folder  of WoT, added the onlineFolder name to the root element, " +
                "added the \"selections\" (developerSelections) names, creation date and filenames to the modInfo.xml, adding all parsed develeoperSelection-Config " +
                "files to the modInfo.dat archive";
            HelpfullMessages.Add(UpdateDatabaseStep2.Name, database);
            HelpfullMessages.Add(UpdateDatabaseStep6.Name, modInfo);
            HelpfullMessages.Add(UpdateDatabaseStep7.Name, serverInfo);
        }
        #endregion
    }
}
