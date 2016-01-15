using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PowerPlanToggler
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new CustomApplicationContext());
        }


    }

    public class CustomApplicationContext : ApplicationContext
    {
        private NotifyIcon trayIcon;

        public CustomApplicationContext()
        {
            // Create tray icon.
            trayIcon = new NotifyIcon();
            trayIcon.Icon = PowerPlanToggler.Properties.Resources.Icon1;
            trayIcon.ContextMenu = IntializeContextMenu();
            trayIcon.Visible = true;
            trayIcon.MouseClick += TrayIcon_MouseClick;
        }

        // Use reflection to open the icon's context menu with left click.
        private void TrayIcon_MouseClick(object sender, MouseEventArgs e)
        {
            MethodInfo mi = typeof(NotifyIcon).GetMethod("ShowContextMenu", BindingFlags.Instance | BindingFlags.NonPublic);
            mi.Invoke((sender as NotifyIcon), null);
        }

        // Exits the application.
        void Exit(object sender, EventArgs e)
        {
            trayIcon.Visible = false;
            Application.Exit();
        }

        // Builds and returns a ContextMenu for the tray icon, including menu items for each power plan, one to allow the program to run at startup, and one to exit the program.
        ContextMenu IntializeContextMenu()
        {
            ContextMenu ret = new ContextMenu();

            // get power plans
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.FileName = "cmd";
            startInfo.Arguments = "/C powercfg -l";
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;
            process.StartInfo = startInfo;
            process.Start();

            System.IO.StreamReader output = process.StandardOutput;

            process.WaitForExit();

            // read first three lines of powercfg command to get to the list of power plans
            output.ReadLine();
            output.ReadLine();
            output.ReadLine();

            // read all power plans
            while (!output.EndOfStream)
            {
                String line = output.ReadLine();
                PowerMenuItem pmi = new PowerMenuItem();
                pmi.Guid = line.Split(' ')[3];
                pmi.PlanName = line.Substring(line.IndexOf('(') + 1, line.IndexOf(')') - line.IndexOf('(') - 1);
                pmi.Name = pmi.PlanName;
                pmi.Text = pmi.PlanName;
                pmi.Checked = line.EndsWith("*"); // * indicates that this is the active power plan
                pmi.Click += Pmi_Click;
                ret.MenuItems.Add(pmi);
            }

            // Menu item for running at startup.
            MenuItem runAtStartupItem = new MenuItem("Run At Startup");

            RegistryKey rkApp = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            runAtStartupItem.Click += RunAtStartupItem_Click;
            //runAtStartupItem.Checked = IsStartupItem(rkApp); does not properly update the Checked property for some unkown reason
            runAtStartupItem.PerformClick(); // hacky way to accomplish properly updating the Checked property at startup
            runAtStartupItem.PerformClick();
            ret.MenuItems.Add(runAtStartupItem);

            ret.MenuItems.Add(new MenuItem("Exit", Exit)); // add Exit MenuItem

            return ret;
        }

        // toggles whether the program will run at startup or not
        private void RunAtStartupItem_Click(object sender, EventArgs e)
        {
            RegistryKey rkApp = Registry.CurrentUser.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run", true);
            if (IsStartupItem(rkApp))
            {
                rkApp.DeleteValue("PowerPlanToggler", false);
                (sender as MenuItem).Checked = false;
            }
            else
            {
                rkApp.SetValue("PowerPlanToggler", Application.ExecutablePath.ToString());
                (sender as MenuItem).Checked = true;
            }
        }

        private static bool IsStartupItem(RegistryKey rkApp)
        {
            if (rkApp.GetValue("PowerPlanToggler") == null)
                // The value doesn't exist, the application is not set to run at startup
                return false;
            else
                // The value exists, the application is set to run at startup
                return true;
        }

        private void Pmi_Click(object sender, EventArgs e)
        {
            PowerMenuItem pmi = sender as PowerMenuItem;

            foreach (MenuItem item in trayIcon.ContextMenu.MenuItems)
                item.Checked = false;
           
            trayIcon.ContextMenu.MenuItems.Find(pmi.PlanName, false)[0].Checked = true;

            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/C powercfg -s " + pmi.Guid;
            process.StartInfo = startInfo;
            process.Start();

        }
    }

    public class PowerMenuItem : MenuItem
    {
        public String Guid
        {
            get; set;
        }
        public String PlanName
        {
            get; set;
        }
    }
}
