﻿using Rectify11Installer.Core;
using Rectify11Installer.Pages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rectify11Installer
{
    //
    //
    //   IRectifyInstaller Interface
    //
    //

    public interface IRectifyInstallerWizard
    {
        /// <summary>
        /// Sets progress bar value
        /// </summary>
        /// <param name="val"></param>
        void SetProgress(int val);
        /// <summary>
        /// Sets the text by the progress bar
        /// </summary>
        /// <param name="text"></param>
        void SetProgressText(string text);
        /// <summary>
        /// Tell the installer that it's work is completed.
        /// </summary>
        void CompleteInstaller(RectifyInstallerWizardCompleteInstallerEnum type, string ErrorDescription = "");
    }
    public enum RectifyInstallerWizardCompleteInstallerEnum
    {
        Success,
        Fail
    }

    //
    //
    //   RectifyInstallerWizard implementation
    //
    //

    internal class RectifyInstallerWizard : IRectifyInstallerWizard
    {
        private readonly FrmWizard Wizard;
        private readonly ProgressPage ProgressPage;
        internal RectifyInstallerWizard(FrmWizard wizard, ProgressPage pg)
        {
            this.Wizard = wizard;
            this.ProgressPage = pg;
        }

        public void CompleteInstaller(RectifyInstallerWizardCompleteInstallerEnum type, string ErrorDescription = "")
        {
            Logger.CloseLog();
            ProgressPage.Invoke((MethodInvoker)delegate ()
            {
                Wizard.Complete(type, ErrorDescription);
            });    
        }

        public void SetProgress(int val)
        {
            ProgressPage.Invoke((MethodInvoker)delegate ()
            {
                ProgressPage.ProgressBarDef.Value = val;
            });
        }

        public void SetProgressText(string text)
        {
            ProgressPage.Invoke((MethodInvoker)delegate ()
            {
                ProgressPage.CurrentProgressText.Text = text;
            });
        }
    }

    //
    //
    //   RectifyInstaller Interface
    //
    //
    /// <summary>
    /// The class implementing this interface is what installs Rectify11
    /// </summary>
    public interface IRectifyInstaller
    {
        /// <summary>
        /// Used for storing the IRectifyInstallerWizard instance
        /// </summary>
        /// <param name="wiz"></param>
        void SetParentWizard(IRectifyInstallerWizard wiz);
        /// <summary>
        /// Install Rectify11
        /// </summary>
        void Install(IRectifyInstalllerOptions options);
    }

    public interface IRectifyInstalllerOptions
    {
        public bool ShouldInstallExplorerPatcher { get; }
        public bool ShouldInstallThemes { get; }
        public bool ShouldInstallWallpaper { get; }
        public bool ShouldInstallWinver { get; }
        public bool DoSafeInstall { get; }
    }
}
