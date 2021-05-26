using GMTools;
using Ionic.Zip;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml;

namespace DownloadModules
{
    public partial class downloadForm : Form
    {
        public downloadForm()
        {
            InitializeComponent();
        }

        public List<string> Urls = new List<string>();
        public List<string> Modules = new List<string>();
        public WebClient wc = new WebClient();
        public string CurrentFile;
        public string DownloadLocation;
        int fileCount;
        int filesToDownload;
        public static Dictionary<string, string> ConvertPlistToDictionary(string _file)
        {
            XmlDocument xmlDocument = new XmlDocument();
            xmlDocument.XmlResolver = null;
            xmlDocument.LoadXml(_file);
            Dictionary<string, string> dictionary = new Dictionary<string, string>();
            XmlNode xmlNode = xmlDocument.SelectSingleNode("plist/dict");
            for (int i = 0; i < xmlNode.ChildNodes.Count; i += 2)
            {
                if (xmlNode.ChildNodes[i].Name == "key")
                {
                    dictionary.Add(xmlNode.ChildNodes[i].InnerText, xmlNode.ChildNodes[i + 1].InnerText);
                }
            }
            return dictionary;
        }

        
        private string pop()
        {
            fileCount++;
            string url = Urls[0];
            Urls.RemoveAt(0);
            return url;
        }

        private void downloadForm_Load(object sender, EventArgs e)
        {

            string licenseFile = Path.Combine(Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"), "GameMaker-Studio"), "license.plist");

            if(File.Exists(licenseFile))
            {
                byte[] plistFileBytes = File.ReadAllBytes(licenseFile);
                string plistFile = Encoding.UTF8.GetString(plistFileBytes);
                Dictionary<string, string> dictionary = ConvertPlistToDictionary(plistFile);

                string[] addonsArray = dictionary["Addons"].Split('.');
                foreach (string addon in addonsArray)
                {

                    switch (addon.ToLowerInvariant())
                    {
                        case "winphone8":
                            Modules.Add("windowsphone");
                            continue;
                        case "free":
                        case "windows":
                        case "androidtest":
                        case "windows8rt":
                        case "yycompiler":
                            continue;
                        default:
                            Modules.Add(addon.ToLowerInvariant());
                            break;
                    }

                    
                }

                Version version = Assembly.LoadFile(Path.Combine(Path.Combine(Environment.GetEnvironmentVariable("APPDATA"), "GameMaker-Studio"), "GameMaker-Studio.exe")).GetName().Version;
                string cdnVersion = string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build);

                CDN.UseCDN(CDNUrls.GMS14);
                foreach(string module in Modules)
                {
                    string moduleUrl = CDN.GetModuleForVersion(cdnVersion, module);
                    if (moduleUrl == "NF")
                        continue;

                    Urls.Add(moduleUrl);
                }

                if(Urls.Count > 0)
                {
                    filesToDownload = Urls.Count;
                    CurrentFile = pop();
                    DownloadLocation = Path.Combine(Path.Combine(Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"), "GameMaker-Studio"), "UpgradeZip"), Path.GetFileName(CurrentFile));
                    Directory.CreateDirectory(Path.GetDirectoryName(DownloadLocation));

                    wc.DownloadProgressChanged += Wc_DownloadProgressChanged;
                    wc.DownloadFileCompleted += Wc_DownloadFileCompleted;
                    wc.DownloadFileAsync(new Uri(CurrentFile), DownloadLocation);
                    
                }
            }
            else
            {
                MessageBox.Show("Cannot find license.plist.", "ERROR");
                Application.Exit();
            }

        }

        private void Wc_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            string zipPassword = CDN.GetPassword(Path.GetFileName(CurrentFile));
            string gmsFolder = Path.Combine(Environment.GetEnvironmentVariable("APPDATA"), "GameMaker-Studio");
            downloadStatus.Text = "Extracting: " + Path.GetFileName(CurrentFile);
            Thread extractThread = new Thread(() =>
            {
                using (ZipFile archive = new ZipFile(DownloadLocation))
                {
                    archive.Password = zipPassword;
                    archive.Encryption = EncryptionAlgorithm.PkzipWeak;
                    archive.ExtractAll(gmsFolder, ExtractExistingFileAction.OverwriteSilently);
                    archive.Dispose();
                }
                try
                {
                    File.Delete(DownloadLocation);
                }
                catch (Exception) { };

            });

            extractThread.Start();

            while (extractThread.IsAlive)
                Application.DoEvents();


            if (Urls.Count > 0)
            {
                CurrentFile = pop();
                DownloadLocation = Path.Combine(Path.Combine(Path.Combine(Environment.GetEnvironmentVariable("LOCALAPPDATA"), "GameMaker-Studio"), "UpgradeZip"), Path.GetFileName(CurrentFile));

                wc.DownloadFileAsync(new Uri(CurrentFile), DownloadLocation);
            }
            else
            {
                Application.Exit();
            }
        }
        private void Wc_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            downloadProgress.Value = e.ProgressPercentage;
            downloadStatus.Text = "Downloading: (File "+fileCount.ToString()+"/"+filesToDownload.ToString()+") "+Path.GetFileName(CurrentFile)+" "+e.BytesReceived.ToString()+"/"+e.TotalBytesToReceive.ToString()+" "+e.ProgressPercentage.ToString()+"%";
            Application.DoEvents();
        }
    }
}
