﻿using Rectify11Installer.Core;
using Rectify11Installer.Win32;
using Rectify11Installer.Win32.Rectify11;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Rectify11Installer
{
    public class RectifyInstaller : IRectifyInstaller
    {
        private IRectifyInstallerWizard? _Wizard;
        public void Install(IRectifyInstalllerOptions options)
        {
            if (_Wizard == null)
            {
                throw new Exception("SetParentWizard() in IRectifyInstaller was not called!");
            }

            try
            {
                _Wizard.SetProgressText("Copying files");

                #region Setup
                if (Directory.Exists("tmp"))
                    Directory.Delete("tmp", true);
                if (Directory.Exists(@"C:/Windows/Rectify11/Tmp/"))
                    Directory.Delete(@"C:/Windows/Rectify11/Tmp/", true);

                Directory.CreateDirectory("C:/Windows/Rectify11/Tmp/");
                Directory.CreateDirectory("C:/Windows/Rectify11/Tmp/Wow64");
                Directory.CreateDirectory("C:/Windows/Rectify11/Tmp/Amd64");
                Directory.CreateDirectory(@"C:\Windows\Rectify11\");
                Directory.CreateDirectory(@"C:\Windows\Rectify11\Backup");
                var backupDir = @"C:\Windows\Rectify11\Backup";
                _Wizard.SetProgressText("Taking ownership of system files");
                _Wizard.SetProgress(1);
                TakeOwnership(@"C:\Windows\SystemResources\", true);

                Directory.CreateDirectory(@"C:/Windows/Rectify11/Tmp/");
                #endregion

                var patches = Patches.GetAll();


                int i = 0;
                foreach (var item in patches)
                {
                    if (item.DisableOnSafeMode && options.DoSafeInstall)
                    {

                    }
                    else
                    {
                        //get the package

                        var usr = GetAMD64Package(item.WinSxSPackageName);
                        if (usr == null)
                        {
                            Logger.Warn("Cannot find package: " + item.WinSxSPackageName + ", which is needed to patch " + item.DllName);
                            continue;
                        }

                        _Wizard.SetProgressText("Patching file: " + item.DllName);
                        _Wizard.SetProgress(i * 100 / patches.Length);

                        var WinSxSFilePath = usr.Path + @"\" + item.DllName;
                        string WinsxsDir = Path.GetFileName(usr.Path);
                        string file = WinsxsDir + "/" + item.DllName;

                        string fileProper = "C:/Windows/Rectify11/Tmp/" + file; //relative path to the file location
                        string backupDirW = backupDir + "/" + WinsxsDir; //backup dir where the file is located at

                        if (!File.Exists(WinSxSFilePath))
                        {
                            Logger.Warn("Cannot find path in package: " + WinSxSFilePath + ", which is needed to patch " + item.DllName);
                            continue;
                        }

                        if (!File.Exists(item.Systempath))
                        {
                            Logger.Warn("Hardlink target in package: " + item.WinSxSPackageName + ", which is not found at" + item.Systempath);
                            continue;
                        }

                        Directory.CreateDirectory("C:/Windows/Rectify11/Tmp/" + WinsxsDir);
                        File.Copy(WinSxSFilePath, fileProper, true);

                        Directory.CreateDirectory(backupDirW);

                        if (!File.Exists(backupDirW + "/" + item.DllName))
                        {
                            File.Copy(WinSxSFilePath, backupDirW + "/" + item.DllName, true);

                            //for now: we will only patch files that don't exist in the backup directory
                            //this is to save time during developent and avoid overwriting orginal files with modified ones

                            foreach (var patch in item.PatchInstructions)
                            {
                                var r = Application.StartupPath + @"\files\" + patch.Resource;
                                if (string.IsNullOrEmpty(patch.Resource))
                                    r = null;

                                //This is where we mod the file
                                if (!PatcherHelper.ReshackAddRes(@"files/ResourceHacker.exe",
                                    fileProper,
                                    fileProper,
                                    patch.Action, //"addoverwrite",
                                    r,
                                    patch.GroupAndLocation))//ICONGROUP,1,0
                                {
                                    _Wizard.CompleteInstaller(RectifyInstallerWizardCompleteInstallerEnum.Fail, $"Resource hacker failed at DLL: {item.DllName}\nCommand line:\n" + PatcherHelper.LastCmd + "\nSee installer.log for more information");
                                    return;
                                }
                            }

                            ReplaceFileInPackage(usr, item.Systempath, fileProper);
                            i++;
                        }
                    }
                }



                _Wizard.CompleteInstaller(RectifyInstallerWizardCompleteInstallerEnum.Success, "");
                return;
            }
            catch (Exception ex)
            {
                _Wizard.CompleteInstaller(RectifyInstallerWizardCompleteInstallerEnum.Fail, ex.ToString());
            }

            //Thread.Sleep(5000);
            ////_Wizard.CompleteInstaller(RectifyInstallerWizardCompleteInstallerEnum.Fail, "not implemented!");
        }

        private void ReplaceFileInPackage(Package usr, string hardlinkTarget, string source)
        {
            string dllName = Path.GetFileName(source);
            var WinSxSFilePath = usr.Path + @"\" + dllName;
            string WinsxsDir = Path.GetFileName(usr.Path);
            string file = WinsxsDir + "/" + dllName;

            string fileProper = "C:/Windows/Rectify11/Tmp/" + file; //relative path to the file location



            //Take ownership of orginal file
            TakeOwnership(usr.Path, true);
            //TakeOwnership(WinSxSFilePath, false);
            //TakeOwnership(fileProper, false); //path to temp file
            TakeOwnership(hardlinkTarget, false);

            //Rename old hardlink
            File.Move(hardlinkTarget, hardlinkTarget + ".bak");

            //Delete old hardlink
            ScheduleForDeletion(hardlinkTarget + ".bak");

            //rename old file
            File.Move(WinSxSFilePath, WinSxSFilePath + ".bak");

            //copy new file over
            File.Move(fileProper, WinSxSFilePath, true);

            //cleanup tmp folder
            Directory.Delete("C:/Windows/Rectify11/Tmp/" + WinsxsDir + "/", true);

            //create hardlink
            if (!Pinvoke.CreateHardLinkA(hardlinkTarget, WinSxSFilePath, IntPtr.Zero))
            {
                if (_Wizard != null)
                    _Wizard.CompleteInstaller(RectifyInstallerWizardCompleteInstallerEnum.Fail, "CreateHardLinkW() failed: " + new Win32Exception().Message);
                throw new Exception("failure while calling MoveFileEx()");
            }

            ScheduleForDeletion(WinSxSFilePath + ".bak");
        }

        private void ScheduleForDeletion(string path)
        {

            //schedule .bak for deletion
            try
            {
                File.Delete(path);
            }
            catch
            {
                //delete it first
                if (!Pinvoke.MoveFileEx(path, null, Pinvoke.MoveFileFlags.MOVEFILE_DELAY_UNTIL_REBOOT))
                {
                    if (_Wizard != null)
                        _Wizard.CompleteInstaller(RectifyInstallerWizardCompleteInstallerEnum.Fail, "MoveFileEx() failed: " + new Win32Exception().Message);
                    throw new Exception("failure while calling MoveFileEx()");
                }
            }
        }

        private Package? GetAMD64Package(string name)
        {
            var usercpl = FindPackage(name);
            if (usercpl.Count == 0)
            {
                return null;
            }
            foreach (var item in usercpl)
            {
                if (item.Arch == PackageArch.Amd64)
                {
                    return item;
                }
            }
            return null;
        }

        private void TakeOwnership(string path, bool recursive)
        {
            if (path.ToLower().StartsWith(@"c:\windows\systemresources"))
            {
                ;
            }
            _ = PatcherHelper.TakeOwnership(path, recursive);
            _ = PatcherHelper.GrantFullControl(path, "Administrators", recursive);
            _ = PatcherHelper.GrantFullControl(path, "SYSTEM", recursive);
            // _ = PatcherHelper.GrantFullControl(path, "Everyone");
        }
        public void SetParentWizard(IRectifyInstallerWizard wiz)
        {
            _Wizard = wiz;
        }

        private List<Package> FindPackage(string name)
        {
            List<Package> p = new List<Package>();
            var build = Environment.OSVersion.Version.Build.ToString();
            foreach (var item in Directory.GetDirectories(@"C:\Windows\WinSxS\"))
            {
                if (item.Contains(build) && item.Contains(name + "_"))
                {
                    var path = item.Replace(@"C:\Windows\WinSxS\", "");
                    if (path.StartsWith("amd64_"))
                    {
                        p.Add(new Package(item, PackageArch.Amd64));
                    }
                    else if (path.StartsWith("wow64_"))
                    {
                        p.Add(new Package(item, PackageArch.Wow64));
                    }
                }
            }

            return p;
        }


        private class Package
        {
            public string Path { get; set; }
            public PackageArch Arch { get; set; }
            public Package(string Path, PackageArch Arch)
            {
                this.Path = Path;
                this.Arch = Arch;
            }
        }
    }

    public enum PackageArch
    {
        Amd64,
        Wow64
    }
    public class PatchDef
    {
        /// <summary>
        /// The package name. Ex: microsoft-windows-usercpl
        /// </summary>
        public string WinSxSPackageName { get; set; }
        /// <summary>
        /// The package arch
        /// </summary>
        public PackageArch WinSxSPackageArch { get; set; }
        /// <summary>
        /// The name of the DLL in the package to be patched. Example: usercpl.dll.mun
        /// </summary>
        public string DllName { get; set; }
        /// <summary>
        /// The path where the hardlink by default points to. Example: C:\windows\systemresources\usercpl.dll.mun
        /// </summary>
        public string Systempath { get; set; }
        /// <summary>
        /// What the patcher should do
        /// </summary>
        public PatchInstruction[] PatchInstructions { get; set; }
        /// <summary>
        /// Skip this patch when safe mode option is selected?
        /// </summary>
        public bool DisableOnSafeMode { get; set; }

        /// <summary>
        /// Represents a patch
        /// </summary>
        /// <param name="packageName">The package name. Ex: microsoft-windows-usercpl</param>
        /// <param name="packageArch">The package arch</param>
        /// <param name="dllToPatch">The name of the DLL in the package to be patched. Example: usercpl.dll.mun</param>
        public PatchDef(string packageName, PackageArch packageArch, string SystemPath, PatchInstruction[] instructions, bool DisableOnSafeMode)
        {
            this.WinSxSPackageName = packageName;
            this.WinSxSPackageArch = packageArch;
            this.DllName = Path.GetFileName(SystemPath);
            this.Systempath = SystemPath;
            this.PatchInstructions = instructions;
            this.DisableOnSafeMode = DisableOnSafeMode;
        }
    }
    public class PatchInstruction
    {
        /// <summary>
        /// ResourceHacker action
        /// </summary>
        public string Action { get; set; }
        /// <summary>
        /// ResourceHacker resource ico
        /// </summary>
        public string Resource { get; set; }
        public string GroupAndLocation { get; set; }
        public PatchInstruction(string action, string resource, string type)
        {
            this.Action = action;
            this.Resource = resource;
            this.GroupAndLocation = type;
        }
    }
}
