using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Renci.SshNet;
using System.IO;
using System.IO.Compression;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
using MSUtil;

namespace LogViewer
{


    //Data grid class
    public class LogData
    {
        public int id { get; set; }
        public string file { get; set; }
        public string time { get; set; }
        public string type { get; set; }
        public string info { get; set; }
        public string error { get; set; }
        public string stacktrace { get; set; }
    }

    public class SettingsData
    {        
        public string sUrl { get; set; }
        public string sUser { get; set; }
        public string sPassword { get; set; }
        public string sPath { get; set; }        
    }
    
    /// <summary>
    /// Логика взаимодействия для MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        string curFolder = System.IO.Directory.GetCurrentDirectory();
        string downloadFolder = "";

        string logFolder = "";
        
        List<LogData> logDataList = new List<LogData>();
        List<LogData> gridDataList = new List<LogData>();
        string host = "";
        string username = "";
        string password = "";
        //string remoteDirectory = "/r01/apache-tomcat/APC/logs/";
        string remoteDirectory = "";
        int logPeriod = 6;



        public MainWindow()
        {
            string curFolder = System.IO.Directory.GetCurrentDirectory();
            downloadFolder = curFolder+ @"\Download\";
            logFolder = curFolder + @"\Logs\";            
            InitializeComponent();
            folderPath.Text = curFolder;
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Hello World");
        }


        public TextBox txt1,txt2;
        public Grid grd1;
        
        private void button1_Click(object sender, RoutedEventArgs e)
        {
                        
            host = serverURL.Text;            
            username = userName.Text;
            password = userPassword.Text;
            remoteDirectory=logPath.Text;
            MessageBox.Show("Settings saved");
            curFolder = folderPath.Text;
            logPeriod= Int32.Parse( logDays.Text);

        }

        private void button2_Click(object sender, RoutedEventArgs e)
        {
            updateStatus("Starting processing.");
            var threadGetFiles = new Thread(getFiles);
            threadGetFiles.Start();
            
        }

        private void loadSettingsClick(object sender, RoutedEventArgs e)
        {
            String fname;
            SettingsData settingsObj = new SettingsData()
            {
                sPassword = password,
                sUrl = host,
                sUser = username,
                sPath = remoteDirectory
            };
            


            // Create SaveFileDialog 

            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.InitialDirectory = curFolder;
            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".txt";
            dlg.Filter = "Text Files (*.txt)|*.txt";


            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();


            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                // Open document 
                fname = dlg.FileName;
            
                using (StreamReader r = new StreamReader(fname))
                {
                    string json = r.ReadToEnd();
                    
                    var deserializer = new JavaScriptSerializer();
                    settingsObj = deserializer.Deserialize<SettingsData>(json);
                }

            
                host = settingsObj.sUrl;
                serverURL.Text = host;
                username = settingsObj.sUser;
                userName.Text = username;
                password = settingsObj.sPassword;
                userPassword.Text = password;
                remoteDirectory = settingsObj.sPath;
                logPath.Text = remoteDirectory;
                MessageBox.Show("Settings loaded");
            }
            
        }


        private void getFiles()
        {
            //Get logs from server
            try
            {
                //Create download and log folders if not exists
                System.IO.Directory.CreateDirectory(downloadFolder);
                System.IO.Directory.CreateDirectory(logFolder);            
                //Clear download folder
                string[] filePaths = Directory.GetFiles(downloadFolder);
            foreach (string filePath in filePaths)
                File.Delete(filePath);

               //Clear log folder
                filePaths = Directory.GetFiles(logFolder);
                foreach (string filePath in filePaths)
                    File.Delete(filePath);

                using (var sftp = new SftpClient(host, username, password))
            {
                sftp.Connect();
                var files = sftp.ListDirectory(remoteDirectory);
                
                foreach (var file in files)
                {
                    TimeSpan fileAge = DateTime.Now - file.LastWriteTime;
                    if ((!file.Name.StartsWith(".")) && (file.IsDirectory) && (fileAge.Days < logPeriod))
                    {
                        Dispatcher.BeginInvoke((Action)(() => updateStatus("\nShould look in folder:" + file.Name + "\ndate:" + DateTime.Now.Date + "    writetime:" + file.LastWriteTime.Date + "   span:" + TimeSpan.FromDays(30).Seconds + "age" + fileAge.Days)));

                        //Get archived folder contents.
                        var filesInFolder = sftp.ListDirectory(remoteDirectory + file.Name);
                        foreach (var fileF in filesInFolder)
                        {
                            string remFName = fileF.Name;
                            string internalPath = remoteDirectory + file.Name + "/";
                            fileAge = DateTime.Now - fileF.LastWriteTime;                            
                            if ( (fileF.Name.StartsWith("apc-payment-ui.")) && (fileF.Name.EndsWith(".zip")) && (fileAge.Days < logPeriod) )
                            {
                                //Check if file already downloaded:                                
                                using (Stream file1 = File.OpenWrite(downloadFolder +remFName))
                                {
                                    if (file1.Length.Equals(fileF.Attributes.Size))
                                        Dispatcher.BeginInvoke((Action)(() => updateStatus("\nSkipping file:  " + fileF.Name + "-------already downloaded-------")));
                                    else
                                    {
                                        Dispatcher.BeginInvoke((Action)(() => updateStatus("\nDownloading file:  " + fileF.Name)));
                                        Dispatcher.BeginInvoke((Action)(() => updateStatus("\nFile size:  " + fileF.Attributes.Size)));
                                        
                                        sftp.DownloadFile(internalPath + remFName, file1);
                                        
                                    }
                                }
                            }
                        }

                    }
                        //Get root folder files

                    string remoteFileName = file.Name;                    
                    
                    if ((file.Name.StartsWith("apc-payment-ui.") && (file.Name.EndsWith(".zip")) ))
                    {
                       
                        
                        using (Stream file1 = File.OpenWrite(downloadFolder + remoteFileName))
                        {
                            if (file1.Length.Equals(file.Attributes.Size))
                                Dispatcher.BeginInvoke((Action)(() => updateStatus("\nSkipping file:  " + file.Name + "-------already downloaded-------")));
                            else
                            {
                                Dispatcher.BeginInvoke((Action)(() => updateStatus("\nDownloading file:  " + file.Name)));
                                Dispatcher.BeginInvoke((Action)(() => updateStatus("\nFile size:  " + file.Attributes.Size)));
                                sftp.DownloadFile(remoteDirectory + remoteFileName, file1);
                            }
                        }
                    }else
                    if ((file.Name.StartsWith("apc-payment-ui.") && (file.Name.EndsWith(".log"))))
                    {


                        using (Stream file1 = File.OpenWrite(logFolder + remoteFileName))
                        {
                            if (file1.Length.Equals(file.Attributes.Size))
                                Dispatcher.BeginInvoke((Action)(() => updateStatus("\nSkipping file:  " + file.Name + "-------already downloaded-------")));
                            else
                            {
                                Dispatcher.BeginInvoke((Action)(() => updateStatus("\nDownloading file:  " + file.Name)));
                                Dispatcher.BeginInvoke((Action)(() => updateStatus("\nFile size:  " + file.Attributes.Size)));
                                sftp.DownloadFile(remoteDirectory + remoteFileName, file1);
                            }
                        }
                    }
                    
                }
                Dispatcher.BeginInvoke((Action)(() => updateStatus("\nFinished getting files.")));

            }

            //unzip downloaded files
            DirectoryInfo directorySelected = new DirectoryInfo(downloadFolder);
            
            foreach (FileInfo fileToDecompress in directorySelected.GetFiles("*.zip"))
            {                
                Dispatcher.BeginInvoke( (Action)(() => updateStatus("\nUnzipping file:  " + fileToDecompress.Name) ));
                ZipFile.ExtractToDirectory(fileToDecompress.FullName, logFolder);
            }

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }



        private void parseFiles()
        {
            //catalina.out
            //Regex parts = new Regex(@"(.{23}) ([A-Z]{1,}) {1,2}(\[([^\]]*)]) (\[([^\]]*)]) (.*)");
            //apc-payment-ui.log
            Regex parts = new Regex(@"(.{12}) (\[([^\]]*)]{1,2})(\[([^\]]*)]) {1,}([A-Z]{1,}) {1,}([A-Za-z0-9.]{1,}) - (.*)");

            logDataList.Clear();

            //parse log files
            DirectoryInfo directorySelected = new DirectoryInfo(logFolder);
            int i = 0;
            foreach (FileInfo fileToParse in directorySelected.GetFiles("*.log"))
            {
                Dispatcher.BeginInvoke((Action)(() => updateStatus("\nParse file:  " + fileToParse.Name)));
                StreamReader reader = new StreamReader(logFolder + fileToParse.Name);
                string line;
                
                Boolean flag = false;
               

                while ((line = reader.ReadLine()) != null)
                {
                    Match match = parts.Match(line);
                    if (match.Success)
                    {
                        

                        string gr1 = match.Groups[1].Value;
                        string gr2 = match.Groups[6].Value;
                        string gr3 = match.Groups[7].Value;
                        string gr4 = match.Groups[8].Value;
                        

                        // At this point, `number` and `path` contain the values we want
                        // for the current line. We can then store those values or print them,
                        // or anything else we like.
                        //if (gr2.Equals("ERROR"))
                        //tcmCase.Text = tcmCase.Text + "\n Group1:" + gr1 + "\nGroup2:" + gr2 + "\nGroup3:" + gr3 + "\nGroup5:" + gr5 + "\nGroup7:" + gr7;
                        if (gr2.Equals("ERROR")|| gr2.Equals("WARN"))
                        {
                            flag = true;
                            i++;
                            logDataList.Add(new LogData()
                            {
                                id = i,
                                time = gr1,
                                file = fileToParse.Name,
                                type = gr2,
                                info = gr3,
                                error = gr4
                            });
                        }
                        else
                            flag = false;
                    }
                    else
                    if (flag)
                    {
                        int idd = logDataList.Count;
                        logDataList[idd - 1].stacktrace = logDataList[idd - 1].stacktrace + line;
                        

                    }


                }
                
            }
            fillGrid();
        }


        private void fillGrid()
        {
            
            
            gridDataList.Clear();
            
                {
            
                    foreach (var dataItem in logDataList)
                    {
                    if (filterType.Text.Length == 0 || dataItem.type.Contains(filterType.Text) )
                        if (filterError.Text.Length==0 || dataItem.error.Contains(filterError.Text))
                            gridDataList.Add(dataItem);
                    }
                }
            

                gridList.UnselectAll();
                gridList.ItemsSource = null;
                gridList.Items.Refresh();
                gridList.ItemsSource = gridDataList;
                
            

        }




        public static Type[] types = new Type[]
{
    Type.GetType("System.Int32"),
    Type.GetType("System.Single"),
    Type.GetType("System.String"),
    Type.GetType("System.DateTime"),
    Type.GetType("System.Nullable")
};

        public static DataTable ParseLog<T>(string query) where T : new()
        {
            LogQueryClass log = new LogQueryClass();
            ILogRecordset recordset = log.Execute(query, new T());
            ILogRecord record = null;

            DataTable dt = new DataTable();
            Int32 columnCount = recordset.getColumnCount();

            for (int i = 0; i < columnCount; i++)
            {
                dt.Columns.Add(recordset.getColumnName(i), types[recordset.getColumnType(i) - 1]);
            }

            for (; !recordset.atEnd(); recordset.moveNext())
            {
                DataRow dr = dt.NewRow();

                record = recordset.getRecord();

                for (int i = 0; i < columnCount; i++)
                {
                    dr[i] = record.getValue(i);
                }
                dt.Rows.Add(dr);
            }
            return dt;
        }

        private void buttonParse_Click(object sender, RoutedEventArgs e)
        {
            parseFiles();
        }

        private void updateStatus(string status)
        {
            statusBox.AppendText(status);
            statusBox.ScrollToEnd();
        }

        private void applyFilter_Click(object sender, RoutedEventArgs e)
        {
            fillGrid();
        }

        private void testConn_Click(object sender, RoutedEventArgs e)
        {
             
            try
            {
                using (var sftp = new SftpClient(host, username, password))
                {
                    sftp.Connect();
                }
                MessageBox.Show("Connection Successfull");
            }
            catch (Exception ex) {
                MessageBox.Show(ex.ToString() );
            }


        }

        private void saveSettingsClick(object sender, RoutedEventArgs e)
        {
            string filename = "";
            SettingsData settingsObj = new SettingsData()            
            {                
                sPassword= password,
                sUrl =  host,
                sUser = username,
                sPath = remoteDirectory
            };
            var json = new JavaScriptSerializer().Serialize(settingsObj);
            // Create SaveFileDialog 
            
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            
            dlg.InitialDirectory = curFolder;
            // Set filter for file extension and default file extension 
            dlg.DefaultExt = ".txt";
            dlg.Filter = "Text Files (*.txt)|*.txt";


            // Display OpenFileDialog by calling ShowDialog method 
            Nullable<bool> result = dlg.ShowDialog();


            // Get the selected file name and display in a TextBox 
            if (result == true)
            {
                // Open document 
                filename = dlg.FileName;
                System.IO.File.WriteAllText(filename, json);
                //textBox1.Text = filename;
            }

            //System.IO.File.WriteAllText(settingsFolder+"path.txt", json);
            
        }

        private void updateError(object sender, RoutedEventArgs e)
        {
            try
            {
                if (gridList.SelectedCells.Count > 0)
                {
                    statusBox.AppendText("Row changed");
                    statusBox.ScrollToEnd();
                    LogData lData = (LogData)gridList.CurrentCell.Item;

                    errorBox.Text = lData.stacktrace;
                }
            }
            catch (Exception ex) {
                statusBox.AppendText("\nError occured:"+ex.Message);
                statusBox.ScrollToEnd();
            }

        }

    }



}
