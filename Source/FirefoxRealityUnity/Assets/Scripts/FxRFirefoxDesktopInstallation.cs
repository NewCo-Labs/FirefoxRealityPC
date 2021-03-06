// This Source Code Form is subject to the terms of the Mozilla Public License, v. 2.0.
// If a copy of the MPL was not distributed with this file, You can obtain one at https://mozilla.org/MPL/2.0/.
//
// Copyright (c) 2019-2020, Mozilla.

using System;
using System.Collections;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Microsoft.Win32;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

public class FxRFirefoxDesktopInstallation : MonoBehaviour
{
    public delegate void InstallationProcessComplete();

    public static InstallationProcessComplete OnInstallationProcessComplete;

    [SerializeField] protected Sprite FirefoxIcon;

    public enum InstallationType
    {
        EMBEDDED,
        DOWNLOADED
    }

    public const InstallationType FxRDesktopInstallationType = InstallationType.EMBEDDED;

    private static readonly string FXR_CONFIGURATION_DIRECTORY = "firefox";

    void Start()
    {
        // First determine whether there is a new version of FxR available, and prompt the user to install it, if so.
        // If no update of FxR is required, then continue, and ensure that we have the Firefox Desktop version required.
        FxRFirefoxRealityVersionChecker.Instance.CheckForNewFirefoxRealityPC(
            (newFirefoxRealityPCVersionAvailable, serverVersionInfo) =>
            {
                if (newFirefoxRealityPCVersionAvailable)
                {
                    // Prompt user if new version on server version of JSON file
                    // TODO: i18n and l10n
                    var dialogTitle =
                        FxRLocalizedStringsLoader.GetApplicationString("fxr_update_available_dialog_title");
                    var dialogMessage = string.Format(
                        FxRLocalizedStringsLoader.GetApplicationString("fxr_update_available_dialog_message"),
                        string.IsNullOrEmpty(serverVersionInfo.LATEST_FXR_PC_VERSION)
                            ? ""
                            : serverVersionInfo.LATEST_FXR_PC_VERSION
                        , string.IsNullOrEmpty(serverVersionInfo.LATEST_FXR_PC_RELEASE_NOTE_HIGHLIGHTS) ? "" : serverVersionInfo.LATEST_FXR_PC_RELEASE_NOTE_HIGHLIGHTS);
                    var dialogButtons = new FxRButton.ButtonConfig[2];
                    dialogButtons[0] = new FxRButton.ButtonConfig(
                        FxRLocalizedStringsLoader.GetApplicationString(
                            "fxr_update_available_dialog_update_later_button"),
                        () =>
                        {
                            var updateLaterDialog = FxRDialogController.Instance.CreateDialog();
                            updateLaterDialog.Show(
                                FxRLocalizedStringsLoader.GetApplicationString(
                                    "fxr_update_available_update_later_response_dialog_title"),
                                FxRLocalizedStringsLoader.GetApplicationString(
                                    "fxr_update_available_update_later_response_dialog_message"),
                                FirefoxIcon,
                                new FxRButton.ButtonConfig(
                                    FxRLocalizedStringsLoader.GetApplicationString("ok_button"),
                                    () => { EnsureFirefoxDesktopInstalled(); },
                                    FxRConfiguration.Instance.ColorPalette.NormalBrowsingSecondaryDialogButtonColors));
                        },
                        FxRConfiguration.Instance.ColorPalette.NormalBrowsingSecondaryDialogButtonColors);
                    dialogButtons[1] = new FxRButton.ButtonConfig(
                        FxRLocalizedStringsLoader.GetApplicationString("fxr_update_available_dialog_update_now_button"),
                        () =>
                        {
                            // Open up URL to download new version
                            Application.OpenURL(serverVersionInfo.LATEST_FXR_PC_URL);
                            var removeHeadsetPrompt = FxRDialogController.Instance.CreateDialog();
                            removeHeadsetPrompt.Show(
                                FxRLocalizedStringsLoader.GetApplicationString(
                                    "fxr_update_available_update_now_response_dialog_title"),
                                FxRLocalizedStringsLoader.GetApplicationString(
                                    "fxr_update_available_update_now_response_dialog_message"),
                                FirefoxIcon,
                                new FxRButton.ButtonConfig(FxRLocalizedStringsLoader.GetApplicationString("ok_button"),
                                    () =>
                                    {
                                        if (FxRDesktopInstallationType == InstallationType.EMBEDDED)
                                        {
                                            FxRController.Quit(0);
                                        }
                                        else
                                        {
                                            // TODO: what should we do when there is a new FxR version? Should we actually continue to ensure
                                            // that we have the desktop version installed, or exit the app while they install, or???
                                            EnsureFirefoxDesktopInstalled();
                                        }
                                    },
                                    FxRConfiguration.Instance.ColorPalette.NormalBrowsingSecondaryDialogButtonColors));
                        },
                        FxRConfiguration.Instance.ColorPalette.NormalBrowsingPrimaryDialogButtonColors);

                    FxRDialogController.Instance.CreateDialog()
                        .Show(dialogTitle, dialogMessage, FirefoxIcon, dialogButtons);
                }
                else
                {
                    EnsureFirefoxDesktopInstalled();
                }
            });
    }

    private void EnsureFirefoxDesktopInstalled()
    {
        // The embedded version of FxR already has FxR desktop installed locally, so we can just notify that we're complete
        if (FxRDesktopInstallationType == InstallationType.EMBEDDED)
        {
            NotifyInstallationComplete();
        }
        else
        {
            FxRFirefoxDesktopVersionChecker.Instance.CheckIfFirefoxInstallationOrConfigurationRequired(
                (installRequired, configurationRequired, firefoxInstallationRequirements) =>
                {
                    if (installRequired)
                    {
                        ContinueDesktopFirefoxInstall(firefoxInstallationRequirements.InstallationTypeRequired,
                            firefoxInstallationRequirements.DownloadType,
                            firefoxInstallationRequirements.InstallationScope);
                    }
                    else
                    {
                        DesktopInstallationComplete();
                    }
                });
        }
    }

    private int retryCount = 0;

    private void ContinueDesktopFirefoxInstall(
        FxRFirefoxDesktopVersionChecker.INSTALLATION_TYPE_REQUIRED installationTypeRequired,
        FxRFirefoxDesktopVersionChecker.DOWNLOAD_TYPE downloadType,
        FxRFirefoxDesktopVersionChecker.INSTALLATION_SCOPE installationScope)
    {
        FxRDialogBox downloadProgressDialog = FxRDialogController.Instance.CreateDialog();
        var dialogTitle = installationTypeRequired ==
                          FxRFirefoxDesktopVersionChecker.INSTALLATION_TYPE_REQUIRED.INSTALL_NEW
            ? FxRLocalizedStringsLoader.GetApplicationString("desktop_installation_install_prompt_dialog_title")
            : FxRLocalizedStringsLoader.GetApplicationString("desktop_installation_update_prompt_dialog_title");

        var dialogMessage = installationTypeRequired ==
                            FxRFirefoxDesktopVersionChecker.INSTALLATION_TYPE_REQUIRED.INSTALL_NEW
            ? FxRLocalizedStringsLoader.GetApplicationString("desktop_installation_install_prompt_dialog_message")
            : FxRLocalizedStringsLoader.GetApplicationString("desktop_installation_update_prompt_dialog_message");

        downloadProgressDialog.Show(dialogTitle, dialogMessage, FirefoxIcon, null
//                    , new FxRButton.ButtonConfig("Cancel",
//                        () => { downloadCancelled = true; }, FxRConfiguration.Instance.ColorPalette.NormalBrowsingSecondaryDialogButtonColors));
        );
        var progress =
            new Progress<float>(zeroToOne =>
            {
                if (downloadProgressDialog == null) return;
                if (!Mathf.Approximately(zeroToOne, 1f))
                {
                    downloadProgressDialog.ShowProgress(zeroToOne);
                }
                else
                {
                    downloadProgressDialog.Close();
                    downloadProgressDialog = null;
                    // var removeHeadsetPrompt = FxRDialogController.Instance.CreateDialog();
                    // removeHeadsetPrompt.Show("Firefox Desktop Installation Started",
                    //     "Please remove your headset to continue the Desktop Firefox install process",
                    //     FirefoxIcon,
                    //     new FxRButton.ButtonConfig(FxRLocalizedStringsLoader.GetApplicationString("ok_button"), null, FxRConfiguration.Instance.ColorPalette.NormalBrowsingSecondaryDialogButtonColors));
                }

                Debug.Log("Download progress: " + zeroToOne.ToString("P1"));
            });
        DownloadAndInstallDesktopFirefox(progress, installationTypeRequired, (wasSuccessful, error, wasCancelled) =>
        {
            if (wasCancelled)
            {
                Debug.Log("Firefox Desktop download cancelled");
                DesktopInstallationComplete();
                return;
            }

            if (wasSuccessful)
            {
                if (downloadProgressDialog != null)
                {
                    var downloadProgressDialogTitle =
                        installationTypeRequired ==
                        FxRFirefoxDesktopVersionChecker.INSTALLATION_TYPE_REQUIRED.INSTALL_NEW
                            ? FxRLocalizedStringsLoader.GetApplicationString(
                                "desktop_installation_install_finished_dialog_title")
                            : FxRLocalizedStringsLoader.GetApplicationString(
                                "desktop_installation_update_finished_dialog_title");

                    var downloadProgressDialogMessage =
                        installationTypeRequired ==
                        FxRFirefoxDesktopVersionChecker.INSTALLATION_TYPE_REQUIRED.INSTALL_NEW
                            ? FxRLocalizedStringsLoader.GetApplicationString(
                                "desktop_installation_install_finished_dialog_message")
                            : FxRLocalizedStringsLoader.GetApplicationString(
                                "desktop_installation_update_finished_dialog_message");

                    downloadProgressDialog.UpdateText(downloadProgressDialogTitle, downloadProgressDialogMessage);
                }

                DesktopInstallationComplete();
            }
            else
            {
                if (downloadProgressDialog != null)
                {
                    downloadProgressDialog.Close();
                    downloadProgressDialog = null;
                }

                var installationErrorDialog = FxRDialogController.Instance.CreateDialog();
                string installationErrorTitle =
                    installationTypeRequired == FxRFirefoxDesktopVersionChecker.INSTALLATION_TYPE_REQUIRED.INSTALL_NEW
                        ? FxRLocalizedStringsLoader.GetApplicationString(
                            "desktop_installation_install_error_dialog_title")
                        : FxRLocalizedStringsLoader.GetApplicationString(
                            "desktop_installation_update_error_dialog_title");
                if (retryCount > 0)
                {
                    var okButton = new FxRButton.ButtonConfig(
                        FxRLocalizedStringsLoader.GetApplicationString("ok_button"),
                        () => { FxRController.Quit(1); },
                        FxRConfiguration.Instance.ColorPalette.NormalBrowsingSecondaryDialogButtonColors);
                    installationErrorDialog.Show(installationErrorTitle,
                        error + "\n" +
                        FxRLocalizedStringsLoader.GetApplicationString(
                            "desktop_installation_configuration_failed_dialog_message")
                        , FirefoxIcon, okButton);
                }
                else
                {
                    var okButton = new FxRButton.ButtonConfig(
                        FxRLocalizedStringsLoader.GetApplicationString("ok_button"),
                        () => { DesktopInstallationComplete(); },
                        FxRConfiguration.Instance.ColorPalette.NormalBrowsingSecondaryDialogButtonColors);
                    var retryButton = new FxRButton.ButtonConfig(
                        FxRLocalizedStringsLoader.GetApplicationString("desktop_installation_retry_button"),
                        () =>
                        {
                            retryCount++;
                            ContinueDesktopFirefoxInstall(installationTypeRequired, downloadType,
                                installationScope);
                        },
                        FxRConfiguration.Instance.ColorPalette.NormalBrowsingSecondaryDialogButtonColors);
                    installationErrorDialog.Show(installationErrorTitle, error, FirefoxIcon, retryButton,
                        okButton);
                }
            }
        }, downloadType, installationScope);
    }

    private bool downloadCancelled;
    private long lastDownloadResponseCode;

    private static readonly string STUB_INSTALLER_BASE_URL =
        "https://download.mozilla.org/?product=firefox-nightly-stub&os=win&lang=";
//        "https://download.mozilla.org/?product=partner-firefox-release-firefoxreality-ffreality-htc-001-stub&os=win64&lang=";

    private static readonly string UPGRADE_INSTALLER_BASE_URL =
        "https://download.mozilla.org/?product=firefox-nightly-latest-ssl&os=win64&lang=";
//        "https://download.mozilla.org/?product=partner-firefox-release-firefoxreality-ffreality-htc-up-001-latest&os=win64&lang=";

// Download the Firefox stub installer
    private IEnumerator DownloadFirefox(IProgress<float> percentDownloaded,
        Action<bool, string, bool> successCallback // <was successful, error, was cancelled>
        , FxRFirefoxDesktopVersionChecker.DOWNLOAD_TYPE downloadType = FxRFirefoxDesktopVersionChecker.DOWNLOAD_TYPE.STUB)
    {
        string downloadURL = null;

        switch (downloadType)
        {
            case FxRFirefoxDesktopVersionChecker.DOWNLOAD_TYPE.STUB:
                downloadURL = STUB_INSTALLER_BASE_URL + CultureStringTwoSegmentsOnly;
                break;
            case FxRFirefoxDesktopVersionChecker.DOWNLOAD_TYPE.RELEASE:
                downloadURL = UPGRADE_INSTALLER_BASE_URL + CultureStringTwoSegmentsOnly;
                break;
//            case DOWNLOAD_TYPE.NIGHTLY:
//                downloadURL = "https://download.mozilla.org/?product=firefox-nightly-latest-l10n-ssl&os=win64&lang=" +
//                             CultureInfo.CurrentCulture.Name;
//                break;
        }

        yield return AttemptDownload(percentDownloaded, successCallback, downloadURL);

        if (!downloadCancelled && lastDownloadResponseCode == 404)
        {
            switch (downloadType)
            {
                case FxRFirefoxDesktopVersionChecker.DOWNLOAD_TYPE.STUB:
                    downloadURL = STUB_INSTALLER_BASE_URL + "en-US";
                    break;
                case FxRFirefoxDesktopVersionChecker.DOWNLOAD_TYPE.RELEASE:
                    downloadURL = UPGRADE_INSTALLER_BASE_URL + "en-US";
                    break;
            }

            yield return AttemptDownload(percentDownloaded, successCallback, downloadURL);
            // This should never happen for en-US version, but just in case...
            if (lastDownloadResponseCode == 404)
            {
                Debug.LogError("Received a 404 attempting to download.");
                successCallback?.Invoke(false, "Download failed.", false);
            }
        }
    }

    private IEnumerator AttemptDownload(IProgress<float> percentDownloaded, Action<bool, string, bool> successCallback,
        string downloadURL)
    {
        var webRequest = new UnityWebRequest(downloadURL);
        webRequest.downloadHandler = new DownloadHandlerFile(FirefoxInstallerDownloadPath, false);
        downloadCancelled = false;
        var downloadOperation = webRequest.SendWebRequest();
        while (!downloadOperation.isDone && !downloadCancelled)
        {
            yield return new WaitForSeconds(.25f);
            percentDownloaded.Report(downloadOperation.progress);
        }

        lastDownloadResponseCode = downloadOperation.webRequest.responseCode;
        if (downloadCancelled)
        {
            webRequest.Abort();
            successCallback?.Invoke(true, "", true);
        }
        else
        {
            if (lastDownloadResponseCode != 404)
            {
                successCallback?.Invoke(string.IsNullOrEmpty(webRequest.error),
                    !string.IsNullOrEmpty(webRequest.error) ? "Download failed." : "", false);
            }
        }
    }

    private void DownloadAndInstallDesktopFirefox(IProgress<float> percentDownloaded,
        FxRFirefoxDesktopVersionChecker.INSTALLATION_TYPE_REQUIRED installationTypeRequired,
        Action<bool, string, bool> successCallback,
        FxRFirefoxDesktopVersionChecker.DOWNLOAD_TYPE downloadType = FxRFirefoxDesktopVersionChecker.DOWNLOAD_TYPE.STUB,
        FxRFirefoxDesktopVersionChecker.INSTALLATION_SCOPE installationScope =
            FxRFirefoxDesktopVersionChecker.INSTALLATION_SCOPE.LOCAL_MACHINE)
    {
        StartCoroutine(DownloadFirefox(percentDownloaded, (wasSuccessful, error, wasCancelled) =>
        {
            if (!wasCancelled && wasSuccessful)
            {
                FxRDialogBox installUpdatePromptDialog = FxRDialogController.Instance.CreateDialog();
                var dialogTitle = installationTypeRequired ==
                                  FxRFirefoxDesktopVersionChecker.INSTALLATION_TYPE_REQUIRED.INSTALL_NEW
                    ? FxRLocalizedStringsLoader.GetApplicationString(
                        "desktop_installation_install_prompt_dialog_title")
                    : FxRLocalizedStringsLoader.GetApplicationString(
                        "desktop_installation_update_prompt_dialog_title");

                var dialogMessage = installationTypeRequired ==
                                    FxRFirefoxDesktopVersionChecker.INSTALLATION_TYPE_REQUIRED.INSTALL_NEW
                    ? FxRLocalizedStringsLoader.GetApplicationString(
                        "desktop_installation_install_prompt_dialog_message")
                    : FxRLocalizedStringsLoader.GetApplicationString(
                        "desktop_installation_update_prompt_dialog_message");

                installUpdatePromptDialog.Show(dialogTitle, dialogMessage, FirefoxIcon
                    , new FxRButton.ButtonConfig("OK",
                        () =>
                        {
                            try
                            {
                                Process installProcess = new Process();
                                installProcess.StartInfo.FileName = FirefoxInstallerDownloadPath;

                                var registryKey = installationScope ==
                                                  FxRFirefoxDesktopVersionChecker.INSTALLATION_SCOPE.LOCAL_USER
                                    ? Registry.CurrentUser
                                    : Registry.LocalMachine;
                                var installPath = FxRFirefoxDesktopVersionChecker.GetFirefoxDesktopInstallationPath();

                                if (!string.IsNullOrEmpty(installPath))
                                {
                                    installProcess.StartInfo.Arguments = "/InstallDirectoryPath=" + installPath;
                                }

                                // Run firefox installation with admin privileges
                                StartCoroutine(LaunchPrivilegedProcess(installProcess, successCallback));
                            }
                            catch (Exception e)
                            {
                                Debug.LogException(e, this);
                                successCallback?.Invoke(false, "Installation failed.", false);
                            }
                        }, FxRConfiguration.Instance.ColorPalette.NormalBrowsingSecondaryDialogButtonColors));
            }
            else if (wasCancelled)
            {
                successCallback?.Invoke(true, "", true);
            }
            else
            {
                successCallback?.Invoke(false, error, false);
            }
        }, downloadType));
    }

    private IEnumerator LaunchPrivilegedProcess(Process installProcess, Action<bool, string, bool> successCallback)
    {
        yield return new WaitForEndOfFrame();
        installProcess.StartInfo.Verb = "runas";
        try
        {
            installProcess.Start();
        }
        catch (Exception e)
        {
            successCallback?.Invoke(false, "There was an issue starting privileged process", false);
            yield break;
        }

        while (!installProcess.HasExited)
        {
            yield return new WaitForEndOfFrame();
        }

        successCallback?.Invoke(installProcess.ExitCode == 0,
            installProcess.ExitCode != 0 ? "'" + installProcess.StartInfo.FileName + "'" + " failed." : "", false);
    }

    private string FirefoxInstallerDownloadPath =>
        Path.Combine(Application.streamingAssetsPath, "Firefox Installer.exe");

    private string CultureStringTwoSegmentsOnly
    {
        get
        {
            var cultureSegments = CultureInfo.CurrentCulture.Name.Split('-');
            if (cultureSegments.Length > 1)
            {
                return cultureSegments[0] + "-" + cultureSegments[1];
            }

            return CultureInfo.CurrentCulture.Name;
        }
    }

    private bool installationCompleteNotificationSent;

    private void DesktopInstallationComplete()
    {
        if (FxRDesktopInstallationType == InstallationType.EMBEDDED)
        {
            NotifyInstallationComplete();
        }
        else
        {
            CopyFxRConfiguration((wasSuccessful, error) =>
            {
                if (wasSuccessful)
                {
                    NotifyInstallationComplete();
                }
                else
                {
                    ShowConfigurationError();
                }
            });
        }
    }


    private void CopyFxRConfiguration(Action<bool, string> successCallback)
    {
        try
        {
            var configurationSourceDirectory =
                Path.Combine(Application.streamingAssetsPath, FXR_CONFIGURATION_DIRECTORY);
            var firefoxDesktopInstallationPath = FxRFirefoxDesktopVersionChecker.GetFirefoxDesktopInstallationPath();
            if (FxRUtilityFunctions.DoAllFilesExist(configurationSourceDirectory, firefoxDesktopInstallationPath))
            {
                // No need to copy anything
                successCallback?.Invoke(true, "");
                return;
            }

            FxRDialogBox configurationStartedDialog = FxRDialogController.Instance.CreateDialog();
            var dialogTitle =
                FxRLocalizedStringsLoader.GetApplicationString(
                    "desktop_installation_configuration_started_dialog_title");

            var dialogMessage =
                FxRLocalizedStringsLoader.GetApplicationString(
                    "desktop_installation_configuration_started_dialog_message");

            configurationStartedDialog.Show(dialogTitle, dialogMessage, FirefoxIcon
                , new FxRButton.ButtonConfig(FxRLocalizedStringsLoader.GetApplicationString("ok_button")
                    , () => { }
                    , FxRConfiguration.Instance.ColorPalette.NormalBrowsingSecondaryDialogButtonColors));

            Process configurationInjectionProcess = new Process();
            configurationInjectionProcess.StartInfo.FileName = "XCOPY";

            // Pass the batch file, configuration overlay directory, and the firefox installation path to "cmd.exe"
            // Arguments are triple-quoted to ensure the quotes are passed to the command line properly.
            configurationInjectionProcess.StartInfo.Arguments =
                string.Format("\"\"\"{0}\"\"\" \"\"\"{1}\"\"\" /F /R /C /Y /S"
                    // , Path.Combine(Application.streamingAssetsPath, FXR_CONFIGURATION_INJECTION_BATCH_FILE)
                    , configurationSourceDirectory
                    , firefoxDesktopInstallationPath);

            StartCoroutine(LaunchPrivilegedProcess(configurationInjectionProcess,
                (wasSuccessful, errorString, wasCancelled) =>
                {
                    if (configurationStartedDialog != null)
                    {
                        configurationStartedDialog.Close();
                    }

                    if (wasSuccessful)
                    {
                        Debug.Log("Successfully configured FxR!");
                        successCallback?.Invoke(true, "");
                    }
                    else
                    {
                        Debug.LogError(
                            "There was a problem configuring Firefox Desktop for use with Firefox Reality: " +
                            errorString);

                        ShowConfigurationError();
                        successCallback?.Invoke(false, errorString);
                    }
                }));
        }
        catch (Exception e)
        {
            // TODO: Determine if there is any more to do in the event the injection fails
            ShowConfigurationError();

            Debug.LogError("There was a problem configuring Firefox Desktop for use with Firefox Reality: " +
                           e.Message);
            Debug.LogException(e, this);
            successCallback?.Invoke(false, e.Message);
        }
    }

    private void ShowConfigurationError()
    {
        FxRDialogBox configurationStartedDialog = FxRDialogController.Instance.CreateDialog();

        var dialogTitle =
            FxRLocalizedStringsLoader.GetApplicationString("desktop_installation_configuration_failed_dialog_title");

        var dialogMessage =
            FxRLocalizedStringsLoader.GetApplicationString("desktop_installation_configuration_failed_dialog_message");

        configurationStartedDialog.Show(dialogTitle, dialogMessage, FirefoxIcon
            , new FxRButton.ButtonConfig(FxRLocalizedStringsLoader.GetApplicationString("ok_button")
                , () => { FxRController.Quit(1); }
                , FxRConfiguration.Instance.ColorPalette.NormalBrowsingSecondaryDialogButtonColors));
    }

    private void NotifyInstallationComplete()
    {
        if (!installationCompleteNotificationSent)
        {
            OnInstallationProcessComplete?.Invoke();
        }

        installationCompleteNotificationSent = true;
    }
}