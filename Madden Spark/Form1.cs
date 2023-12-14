using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;


namespace Madden_Spark
{
    public partial class Form1 : Form
    {
        private string maddenPath;
        private const string MaddenPathFile = "MaddenPath.txt";
        private const string EAAntiCheatExe = "EAAntiCheat.GameServiceLauncher.exe";
        private const string EAAntiCheatExeOrig = "EAAntiCheat.GameServiceLauncher_Orig.exe";
        private const string MaddenExe = "Madden24.exe";
        private const string MaddenSparkExe = "Resources\\MaddenSpark.exe";
        private const string BypassMonitor = "BypassMonitor.exe";
        private const string DatapathFix = "DatapathFix.exe";
        private bool isActivationInProgress = false;
        [DllImport("gdi32.dll")]
        private static extern int AddFontResource(string lpFilename);

        [DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, uint Msg, int wParam, int lParam);

        private const int WM_FONTCHANGE = 0x001D;
        private const int HWND_BROADCAST = 0xffff;

        public Form1()
        {
            InitializeComponent();
            LoadMaddenPath();

            this.Icon = Properties.Resources.icon;
            ApplyCustomFont();
            UpdateModStatusLabel();
        }

        private void ApplyCustomFont()
        {
            string fontResourcePath = "Madden_Spark.Resources.NCAAMinnesotaGoldenGopher.otf";
            float fontSize = 15.75F;
            FontStyle fontStyle = FontStyle.Bold;

            Font customFont = LoadFont(fontResourcePath, fontSize, fontStyle);

            Label testLabel = new Label
            {
                Font = customFont,
                Text = "Test Label",
                Location = new Point(10, 10),
                AutoSize = true
            };
            this.Controls.Add(testLabel);

            UpdateModStatusLabel();
        }

        private void InstallCustomFont(string resourceFontPath)
        {
            using (Stream fontStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceFontPath))
            {
                if (fontStream != null)
                {
                    string tempFontPath = Path.Combine(Path.GetTempPath(), "NCAA Minnesota Golden Gopher.otf");

                    using (FileStream fileStream = new FileStream(tempFontPath, FileMode.Create, FileAccess.Write))
                    {
                        fontStream.CopyTo(fileStream);
                    }

                    AddFontResource(tempFontPath);
                    SendMessage(new IntPtr(HWND_BROADCAST), WM_FONTCHANGE, 0, 0);
                }
            }
        }

        private async Task<string> GetInstalledEaGames()
        {
            try
            {
                // The base registry key for EA Sports games
                string baseRegistryKey = @"SOFTWARE\EA Sports";

                // Open the registry key (note: this assumes a 64-bit OS)
                RegistryKey key = Registry.LocalMachine.OpenSubKey(baseRegistryKey, RegistryKeyPermissionCheck.ReadSubTree, System.Security.AccessControl.RegistryRights.ReadKey);

                if (key == null)
                {
                    return null;
                }

                // Get the subkeys (these are the individual game keys)
                string[] subKeys = key.GetSubKeyNames();

                foreach (string subKey in subKeys)
                {
                    RegistryKey gameKey = key.OpenSubKey(subKey);
                    if (gameKey != null)
                    {
                        // Get the name and installation directory of the game
                        string name = gameKey.GetValue("DisplayName") as string;
                        string installDir = gameKey.GetValue("Install Dir") as string;

                        // Trim the trailing backslash, if any
                        if (installDir != null && installDir.EndsWith("\\"))
                        {
                            installDir = installDir.Substring(0, installDir.Length - 1);
                        }

                        if (!string.IsNullOrEmpty(installDir))
                        {
                            // Check if Madden24.exe exists in this directory
                            string maddenExePath = Path.Combine(installDir, "Madden24.exe");
                            if (File.Exists(maddenExePath))
                            {
                                return maddenExePath;
                            }
                        }
                    }
                }

                return null;  // Return null if Madden24.exe is not found
            }
            catch (Exception ex)
            {
                // Handle any exceptions (e.g., registry key not found, access denied, etc.)
                Console.WriteLine($"An error occurred: {ex.Message}");
                return null;
            }
        }

        private void SelectMaddenFolder()
        {
            using (var folderBrowser = new OpenFileDialog())
            {
                folderBrowser.ValidateNames = false;
                folderBrowser.CheckFileExists = false;
                folderBrowser.CheckPathExists = true;
                folderBrowser.FileName = "Select your Madden NFL 24 base game folder.";
                folderBrowser.Title = "Select Madden 24 Game Folder";
                folderBrowser.Filter = "Folders|\n";

                if (folderBrowser.ShowDialog() == DialogResult.OK)
                {
                    maddenPath = Path.GetDirectoryName(folderBrowser.FileName);
                    File.WriteAllText(MaddenPathFile, maddenPath);
                }
            }
        }

        private async void LoadMaddenPath()
        {
            try
            {
                if (File.Exists(MaddenPathFile))
                {
                    maddenPath = File.ReadAllText(MaddenPathFile);
                }
                else
                {
                    string maddenExePath = await GetInstalledEaGames();
                    if (!string.IsNullOrEmpty(maddenExePath))
                    {
                        maddenPath = Path.GetDirectoryName(maddenExePath);
                        File.WriteAllText(MaddenPathFile, maddenPath);
                    }
                    else
                    {
                        MessageBox.Show("Madden24.exe was not found. Please select the folder with Madden24.exe in it.");
                        SelectMaddenFolder();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while reading the Madden path: {ex.Message}");
            }
        }

        private string FindMaddenExe()
        {
            // This will get all available drives
            DriveInfo[] allDrives = DriveInfo.GetDrives();

            // Loop through all drives
            foreach (DriveInfo drive in allDrives)
            {
                // We only want to search in drives that are ready to be read
                if (drive.IsReady)
                {
                    try
                    {
                        // Search for MaddenExe in all directories under the root path
                        string[] files = Directory.GetFiles(drive.RootDirectory.FullName, MaddenExe, SearchOption.AllDirectories);
                        if (files.Length > 0)
                        {
                            return files[0];
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // If we don't have permission to read the directory, we catch the exception and continue
                    }
                    catch (System.IO.IOException)
                    {
                        // Catch other I/O errors and continue
                    }
                }
            }
            return null;  // Return null if MaddenExe is not found in any of the drives
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            KillMaddenProcess();
            KillAllProcesses();

            base.OnFormClosing(e);
        }

        private void KillMaddenProcess()
        {
            Process[] maddenProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(MaddenExe));
            foreach (Process process in maddenProcesses)
            {
                process.Kill();
            }
        }

        private async Task MonitorMaddenProcess()
        {
            await Task.Run(() =>
            {
                bool isMaddenRunning = true;
                while (isMaddenRunning)
                {
                    Process[] maddenProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(MaddenExe));
                    if (maddenProcesses.Length == 0)
                    {
                        isMaddenRunning = false;
                    }
                    else
                    {
                        Thread.Sleep(1000); // Check every 1 second
                    }
                }

                ModStatusLabel.Invoke(new Action(() =>
                {
                    ModStatusLabel.Text = "Modding Inactive";
                    ModStatusLabel.ForeColor = System.Drawing.Color.Red;
                }));

                // Call the OriginRestore function after Madden24.exe has closed
                OriginRestore_Click(this, EventArgs.Empty);
            });
        }

        private void DragPanel_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ReleaseCapture();
                SendMessage(Handle, WM_NCLBUTTONDOWN, HT_CAPTION, 0);
            }
        }

        private const int WM_NCLBUTTONDOWN = 0xA1;
        private const int HT_CAPTION = 0x2;

        [DllImportAttribute("user32.dll")]
        public static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImportAttribute("user32.dll")]
        public static extern bool ReleaseCapture();

        public class SystemManager
        {
            [DllImport("advapi32.dll", SetLastError = true)]
            private static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern IntPtr GetCurrentProcess();

            [DllImport("advapi32.dll", SetLastError = true)]
            private static extern bool GetTokenInformation(IntPtr TokenHandle, TOKEN_INFORMATION_CLASS TokenInformationClass, IntPtr TokenInformation, int TokenInformationLength, out int ReturnLength);

            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool CloseHandle(IntPtr hObject);

            private enum TOKEN_INFORMATION_CLASS
            {
                TokenElevation = 20
            }

            private struct TOKEN_ELEVATION
            {
                public int TokenIsElevated;
            }

            public static bool IsElevated()
            {
                IntPtr tokenHandle;
                if (!OpenProcessToken(GetCurrentProcess(), 8 /*TOKEN_QUERY*/, out tokenHandle))
                {
                    return false;
                }

                TOKEN_ELEVATION elevation;
                int size = Marshal.SizeOf(typeof(TOKEN_ELEVATION));
                IntPtr elevationPtr = Marshal.AllocHGlobal(size);
                bool success = GetTokenInformation(tokenHandle, TOKEN_INFORMATION_CLASS.TokenElevation, elevationPtr, size, out size);
                elevation = (TOKEN_ELEVATION)Marshal.PtrToStructure(elevationPtr, typeof(TOKEN_ELEVATION));
                Marshal.FreeHGlobal(elevationPtr);

                CloseHandle(tokenHandle);

                return success && elevation.TokenIsElevated != 0;
            }

            public static void UnblockNetworkForApp(string appName, string appPath)
            {
                string command = $"netsh advfirewall firewall delete rule name=\"Block {appName}\" program=\"{appPath}\"";
                System.Diagnostics.Process.Start("cmd.exe", "/C " + command);
            }
        }

        private void KillEAAntiCheatProcess()
        {
            Process[] eaAntiCheatProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(EAAntiCheatExe));
            foreach (Process process in eaAntiCheatProcesses)
            {
                process.Kill();
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            if (isActivationInProgress)
            {
                Process[] maddenProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(MaddenExe));
                if (maddenProcesses.Length > 0)
                {
                    MessageBox.Show("Madden 24 is still running. Please wait for it to close before activating again.");
                    return;
                }
            }

            isActivationInProgress = true;

            if (string.IsNullOrEmpty(maddenPath))
            {
                using (var folderBrowser = new FolderBrowserDialog())
                {
                    if (folderBrowser.ShowDialog() == DialogResult.OK)
                    {
                        maddenPath = folderBrowser.SelectedPath;
                        File.WriteAllText(MaddenPathFile, maddenPath);
                    }
                }
            }

            OriginRestore_Click(sender, e);

            string antiCheatPath = Path.Combine(maddenPath, EAAntiCheatExe);
            string antiCheatOrigPath = Path.Combine(maddenPath, EAAntiCheatExeOrig);

            if (File.Exists(antiCheatPath))
            {
                Process[] maddenProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(MaddenExe));
                if (maddenProcesses.Length > 0)
                {
                    MessageBox.Show("Madden 24 is still running. Please wait for it to close before activating again.");
                    return;
                }

                try
                {
                    if (File.Exists(antiCheatOrigPath))
                    {
                        File.Delete(antiCheatPath);
                    }
                    else
                    {
                        File.Move(antiCheatPath, antiCheatOrigPath);
                    }
                }
                catch (UnauthorizedAccessException ex)
                {
                    MessageBox.Show("Madden 24 is still running. Please wait for it to close before activating again.");
                    return;
                }
            }

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string antiCheatResourcePath = Path.Combine(baseDirectory, "Resources", EAAntiCheatExe);
            File.Copy(antiCheatResourcePath, antiCheatPath);

            string maddenSparkPath = Path.Combine(baseDirectory, MaddenSparkExe);
            ProcessStartInfo startInfo = new ProcessStartInfo(maddenSparkPath)
            {
                WindowStyle = ProcessWindowStyle.Hidden
            };
            Process maddenSparkProcess = Process.Start(startInfo);

            ModStatusLabel.Invoke(new Action(() =>
            {
                ModStatusLabel.Text = "Waiting For User to Launch Madden";
                ModStatusLabel.ForeColor = System.Drawing.Color.Yellow;
            }));

            await Task.Run(() =>
            {
                bool isMaddenRunning = false;
                while (!isMaddenRunning)
                {
                    Process[] maddenProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(MaddenExe));
                    if (maddenProcesses.Length > 0)
                    {
                        isMaddenRunning = true;

                        // Kill the EA Anti-Cheat process here
                        KillEAAntiCheatProcess();
                    }
                    System.Threading.Thread.Sleep(1000);
                }
            });

            ModStatusLabel.Invoke(new Action(() =>
            {
                ModStatusLabel.Text = "Modding Activated";
                ModStatusLabel.ForeColor = System.Drawing.Color.Green;
            }));

            await MonitorMaddenProcess();

            maddenSparkProcess.WaitForExit();

            ModStatusLabel.Text = "Modding Inactive";
            ModStatusLabel.ForeColor = System.Drawing.Color.Red;
            isActivationInProgress = false;
        }

        private void OriginRestore_Click(object sender, EventArgs e)
        {
            //DialogResult result = MessageBox.Show("  Continuing will close Madden 24. (If Open)\n\n              Do you want to continue?", "Confirmation", MessageBoxButtons.YesNo);
            //    if (result == DialogResult.No)
            //    {
            //        return;
            //    }

            KillMaddenProcess();
            KillAllProcesses();

            bool isMaddenRunning = true;
            while (isMaddenRunning)
            {
                Process[] maddenProcesses = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(MaddenExe));
                if (maddenProcesses.Length == 0)
                {
                    isMaddenRunning = false;
                }
                else
                {
                    Thread.Sleep(1000);
                }
            }

            Thread.Sleep(5000);

            string antiCheatPath = Path.Combine(maddenPath, EAAntiCheatExe);
            string antiCheatOrigPath = Path.Combine(maddenPath, EAAntiCheatExeOrig);

            if (File.Exists(antiCheatOrigPath))
            {
                if (File.Exists(antiCheatPath))
                {
                    File.Delete(antiCheatPath);
                }

                File.Move(antiCheatOrigPath, antiCheatPath);
            }

            try
            {
                string[] filesToDelete = { "tmp", "CryptBase.dll", "Madden24.old", "dpapi.dll", "datapath.old" };
                foreach (string fileOrFolder in filesToDelete)
                {
                    string path = Path.Combine(maddenPath, fileOrFolder);
                    if (Directory.Exists(path))
                    {
                        Directory.Delete(path, true);
                    }
                    else if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }

                string maddenExePath = Path.Combine(maddenPath, MaddenExe);
                string maddenOrigExePath = Path.Combine(maddenPath, "Madden24.Orig.exe");

                if (File.Exists(maddenOrigExePath))
                {
                    if (File.Exists(maddenExePath))
                    {
                        File.Delete(maddenExePath);
                    }

                    File.Move(maddenOrigExePath, maddenExePath);
                }

                //  MessageBox.Show("Madden 24 has been restored to the Base Game");
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"An error occurred while restoring Madden 24: {ex.Message}");
                return;
            }

            if (!SystemManager.IsElevated())
            {
                MessageBox.Show("This application requires administrator privileges. Please run as administrator.", "Error");
                return;
            }

            SystemManager.UnblockNetworkForApp("EADesktop.exe", "C:\\Program Files\\Electronic Arts\\EA Desktop\\EA Desktop\\EADesktop.exe");
            SystemManager.UnblockNetworkForApp("Origin.exe", "C:\\Program Files (x86)\\Origin\\Origin.exe");

            UpdateModStatusLabel();
        }

        private Font LoadFont(string resourceFontPath, float size, FontStyle style)
        {
            Font font = null;
            using (Stream fontStream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceFontPath))
            {
                if (fontStream != null)
                {
                    byte[] fontData = new byte[fontStream.Length];
                    fontStream.Read(fontData, 0, (int)fontStream.Length);

                    IntPtr fontPtr = Marshal.AllocCoTaskMem(fontData.Length);
                    Marshal.Copy(fontData, 0, fontPtr, fontData.Length);

                    PrivateFontCollection fontCollection = new PrivateFontCollection();
                    fontCollection.AddMemoryFont(fontPtr, fontData.Length);
                    Marshal.FreeCoTaskMem(fontPtr);

                    font = new Font(fontCollection.Families[0], size, style);
                }
            }

            if (font == null)
            {
                throw new Exception("Font not found in resources.");
            }

            return font;
        }
        private void KillAllProcesses()
        {
            try
            {
                KillProcessByName(Path.GetFileNameWithoutExtension(MaddenExe));
                KillProcessByName("EAAntiCheat.GameServiceLauncher");
                KillProcessByName("MaddenSpark");
                KillProcessByName("OriginRestore");
                KillProcessByName("BypassMonitor");
                KillProcessByName("DatapathFix");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Madden 24 is still running. Please wait for it to close before activating again.");
            }
        }

        private void KillProcessByName(string processName)
        {
            Process[] processes = Process.GetProcessesByName(processName);
            foreach (Process process in processes)
            {
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show("Madden 24 is still running. Please wait for it to close before activating again.");
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    MessageBox.Show("Madden 24 is still running. Please wait for it to close before activating again.");
                }
            }
        }

        private void UpdateModStatusLabel()
        {
            string[] filesToCheck = { "tmp", "CryptBase.dll", "Madden24.old", "dpapi.dll", "datapath.old" };
            bool isBaseGame = true;

            foreach (string fileOrFolder in filesToCheck)
            {
                string path = Path.Combine(maddenPath, fileOrFolder);
                if (File.Exists(path) || Directory.Exists(path))
                {
                    isBaseGame = false;
                    break;
                }
            }

            if (isBaseGame)
            {
                ModStatusLabel.Text = "Base Game";
                ModStatusLabel.ForeColor = Color.Blue;
            }
        }

        private void ModStatusLabel_Click(object sender, EventArgs e) { }
        private void Form1_Load(object sender, EventArgs e) { }
    }
}