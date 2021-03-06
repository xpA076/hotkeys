﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml.Linq;

namespace AhkSetter
{
    public partial class AhkSetter : Form
    {
        //private const string host = "http://server.hhcsdtc.com:9999";       // 结尾不能带"/"
        private const string host = "http://localhost:9999";       // 结尾不能带"/"

        private const string exeName = "AhkSetter.exe";

        private static string ConfigPath
        {
            get
            {
                return Application.StartupPath + "\\AhkSetter.config";
            }
        }

        public AhkSetter()
        {
            InitializeComponent();
        }
        
        

        private void buttonConfig_Click(object sender, EventArgs e)
        {
            AhkSetterConfigForm config = new AhkSetterConfigForm(this);
            config.Show();
        }

        private void buttonSet_Click(object sender, EventArgs e)
        {
            XDocument doc = XDocument.Load(ConfigPath);
            XElement root = doc.Root;
            root.Element("defaultIndex").SetElementValue("Executable", this.comboBox1.SelectedIndex);
            root.Element("defaultIndex").SetElementValue("Directory", this.comboBox2.SelectedIndex);
            root.Element("defaultIndex").SetElementValue("Webpage", this.comboBox3.SelectedIndex);
            root.Element("defaultIndex").SetElementValue("Wechat", this.comboBox4.SelectedIndex);
            string path = root.Element("ahkPath").Attribute("path").Value;
            if (!File.Exists(path + "\\init.ahk"))
            {
                MessageBox.Show("Ahk scripts path invalid. Select the directory with init.ahk");
                return;
            }
            root.Save(Application.StartupPath + "\\AhkSetter.config");
            string exeStr = postUrl("/Hotkey/AhkSetterExecutable");
            System.IO.File.WriteAllText(path + @"\json\executable.json", exeStr, Encoding.GetEncoding("GBK"));
            string dirStr = postUrl("/Hotkey/AhkSetterDirectory");
            System.IO.File.WriteAllText(path + @"\json\directory.json", dirStr, Encoding.GetEncoding("GBK"));
            string webStr = postUrl("/Hotkey/AhkSetterWebpage");
            System.IO.File.WriteAllText(path + @"\json\webpage.json", webStr, Encoding.GetEncoding("GBK"));

            // wechat script 替换
            string wechatStr = postUrl("/Hotkey/AhkSetterWechat");
            JavaScriptSerializer jss = new JavaScriptSerializer();
            string[] hotkeys = jss.Deserialize<string[]>(wechatStr);
            StreamReader sr = new StreamReader(path + @"\window_keys.ahk", Encoding.GetEncoding("GBK"));
            StreamWriter sw = new StreamWriter(path + @"\_window_keys.ahk", false, Encoding.GetEncoding("GBK"));
            string matchline = "#If WinActive(\"ahk_class WeChatMainWndForPC\")";
            for (string srline = sr.ReadLine(); srline != null; srline = sr.ReadLine())
            {
                if (srline != matchline)
                {
                    sw.WriteLine(srline);
                    continue;
                }
                else
                {
                    sw.WriteLine(matchline);
                    // write
                    for (int i = 0; i < 281; ++i)
                    {
                        if (string.IsNullOrWhiteSpace(hotkeys[i])) { continue; }
                        sw.WriteLine("    ::" + hotkeys[i] + "::");
                        sw.WriteLine("        WechatClickFace({0},{1})", i / 15 + 1, i % 15 + 1);
                        sw.WriteLine("        return");
                    }
                    while (srline != "#If" && srline != null) { srline = sr.ReadLine(); }
                    sw.WriteLine("#If");
                    for (srline = sr.ReadLine(); srline != null; srline = sr.ReadLine()) { sw.WriteLine(srline); }
                }
            }
            sr.Close();
            sw.Close();
            System.IO.File.Delete(path + @"\window_keys.ahk");
            System.IO.Directory.Move(path + @"\_window_keys.ahk", path + @"\window_keys.ahk");
            System.Diagnostics.Process.Start(path + @"\init.ahk");
            this.toolStripStatusLabel1.Text = "Setting ahk success - " + DateTime.Now.ToLongTimeString();
        }

        private void AhkSetter_Load(object sender, EventArgs e)
        {
            CheckUpdate();
            if (!File.Exists(ConfigPath))
            {
                XDocument doc = new XDocument();
                XElement root = new XElement("ahkSetterConfig");
                XElement hostUrl = new XElement("hostUrl");
                hostUrl.SetAttributeValue("url", host);
                XElement ahkPath = new XElement("ahkPath");
                ahkPath.SetAttributeValue("path", Application.StartupPath);
                XElement user = new XElement("user");
                user.SetElementValue("username", "");
                user.SetElementValue("password", "");
                XElement defaultIndex = new XElement("defaultIndex");
                defaultIndex.SetElementValue("Executable", 0);
                defaultIndex.SetElementValue("Directory", 0);
                defaultIndex.SetElementValue("Webpage", 0);
                defaultIndex.SetElementValue("Wechat", 0);
                root.Add(hostUrl);
                root.Add(ahkPath);
                root.Add(user);
                root.Add(defaultIndex);
                root.Save(ConfigPath);
                this.toolStripStatusLabel1.Text = "AhkSetter.config created";
            }
        }

        public void loadNames()
        {
            string nameJson = postUrl("/Hotkey/AhkSetterIndex");
            JavaScriptSerializer jss = new JavaScriptSerializer();
            string[][] names;
            try
            {
                names = jss.Deserialize<string[][]>(nameJson);
                if (names == null){ throw new Exception("null response");}
            }
            catch(Exception)
            {
                MessageBox.Show("Server response error: check your username & password (or reset your page content)");
                this.toolStripStatusLabel1.Text = "Loading error - " + DateTime.Now.ToLongTimeString();
                return;
            }
            
            ComboBox[] comboBoxes = new ComboBox[4]
            {
                this.comboBox1,
                this.comboBox2,
                this.comboBox3,
                this.comboBox4
            };
            for(int box = 0; box < 4; ++box)
            {
                comboBoxes[box].Items.Clear();
                for (int i = 0; i < names[box].Length; ++i)
                {
                    comboBoxes[box].Items.Add((i + 1).ToString() + ": " + names[box][i]);
                }
            }
            XDocument doc = XDocument.Load(ConfigPath);
            XElement root = doc.Root;
            this.comboBox1.SelectedIndex = int.Parse(root.Element("defaultIndex").Element("Executable").Value);
            this.comboBox2.SelectedIndex = int.Parse(root.Element("defaultIndex").Element("Directory").Value);
            this.comboBox3.SelectedIndex = int.Parse(root.Element("defaultIndex").Element("Webpage").Value);
            this.comboBox4.SelectedIndex = int.Parse(root.Element("defaultIndex").Element("Wechat").Value);
            this.toolStripStatusLabel1.Text = "Loading success - " + DateTime.Now.ToLongTimeString();
        }


        private string postUrl(string action)
        {
            AhkSetterPost ahkSetterPost = new AhkSetterPost();
            try
            {
                XDocument doc = XDocument.Load(Application.StartupPath + "\\AhkSetter.config");
                XElement root = doc.Root;
                ahkSetterPost.username = root.Element("user").Element("username").Value;
                ahkSetterPost.password = root.Element("user").Element("password").Value;
                ahkSetterPost.idxes = new int[4]
                {
                    int.Parse(root.Element("defaultIndex").Element("Executable").Value),
                    int.Parse(root.Element("defaultIndex").Element("Directory").Value),
                    int.Parse(root.Element("defaultIndex").Element("Webpage").Value),
                    int.Parse(root.Element("defaultIndex").Element("Wechat").Value),
                };
                string host = root.Element("hostUrl").Attribute("url").Value;
                host = (host[host.Length - 1] == '/') ? host.Substring(0, host.Length - 1) : host;
                var request = (HttpWebRequest)WebRequest.Create(host + action);
                //发送请求
                request.Method = "POST";
                request.ContentType = "application/json;charset=UTF-8";
                JavaScriptSerializer jss = new JavaScriptSerializer();
                var byteData = Encoding.UTF8.GetBytes(jss.Serialize(ahkSetterPost));
                var length = byteData.Length;
                request.ContentLength = length;
                var writer = request.GetRequestStream();
                writer.Write(byteData, 0, length);
                writer.Close();
                //接收数据
                var response = (HttpWebResponse)request.GetResponse();
                string responseString = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8")).ReadToEnd();
                //MessageBox.Show(responseString);
                return responseString;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return null;
            }
        }

        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            loadNames();
        }

        private void AhkSetter_Shown(object sender, EventArgs e)
        {
            XDocument doc = XDocument.Load(Application.StartupPath + "\\AhkSetter.config");
            if (string.IsNullOrEmpty(doc.Root.Element("user").Element("username").Value))
            {
                AhkSetterConfigForm config = new AhkSetterConfigForm(this);
                config.Show();
            }
            else
            {
                loadNames();
            }
        }

        private void CheckUpdate()
        {
            string updaterPath = Path.Combine(Application.StartupPath, "Updater.exe");
            try
            {
                if (File.Exists(updaterPath))
                {
                    File.Delete(updaterPath);
                }
            }
            catch (Exception) {; }
            try
            {
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(host + "/File/CheckVersion");
                // 发送请求
                request.Method = "POST";
                request.ContentType = "application/json;charset=UTF-8";
                var byteData = Encoding.UTF8.GetBytes(exeName);
                var length = byteData.Length;
                request.ContentLength = length;
                var writer = request.GetRequestStream();
                writer.Write(byteData, 0, length);
                writer.Close();
                // 接收数据
                var response = (HttpWebResponse)request.GetResponse();
                string responseString = new StreamReader(response.GetResponseStream(), Encoding.GetEncoding("utf-8")).ReadToEnd();
                // 获取版本号
                Version latestVersion = new Version(responseString);
                Version currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
                this.toolStripStatusLabel2.Text = "    Vers: " + currentVersion.ToString();
                if (latestVersion > currentVersion)
                {
                    // 下载Updater
                    FileStream fileStream = new FileStream(updaterPath, FileMode.Create);
                    HttpWebRequest fileRequest = (HttpWebRequest)WebRequest.Create(host + "/File/DownloadProgram/Updater.exe");
                    WebResponse fileResponse = fileRequest.GetResponse();
                    Stream fileResponseStream = fileResponse.GetResponseStream();
                    byte[] bytes = new byte[1024];
                    int size = fileResponseStream.Read(bytes, 0, bytes.Length);
                    while (size > 0)
                    {
                        fileStream.Write(bytes, 0, size);
                        size = fileResponseStream.Read(bytes, 0, bytes.Length);
                    }
                    fileStream.Close();
                    fileResponseStream.Close();
                    // Updater -name AhkSetter
                    Process proc = new Process();
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = updaterPath;
                    startInfo.Arguments = "-name " + exeName;
                    Process.Start(startInfo);
                    this.Close();
                }
            }
            catch (Exception) {; }
        }
    }
}
