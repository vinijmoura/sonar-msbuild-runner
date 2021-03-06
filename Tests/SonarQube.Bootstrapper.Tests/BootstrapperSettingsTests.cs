﻿//-----------------------------------------------------------------------
// <copyright file="BootstrapperSettingsTests.cs" company="SonarSource SA and Microsoft Corporation">
//   Copyright (c) SonarSource SA and Microsoft Corporation.  All rights reserved.
//   Licensed under the MIT License. See License.txt in the project root for license information.
// </copyright>
//-----------------------------------------------------------------------
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;
using TestUtilities;

namespace SonarQube.Bootstrapper.Tests
{
    [TestClass]
    public class BootstrapperSettingsTests
    {
        public TestContext TestContext { get; set; }

        private static readonly string DownloadFolderRelativePath = Path.Combine(BootstrapperSettings.RelativePathToTempDir, BootstrapperSettings.RelativePathToDownloadDir);

        #region Tests

        [TestMethod]
        public void BootSettings_DownloadDirFromEnvVars()
        {
            // 0. Setup
            TestLogger logger = new TestLogger();

            // 1. Legacy TFS variable will be used if available
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(BootstrapperSettings.BuildDirectory_Legacy, "legacy tf build");
                scope.SetVariable(BootstrapperSettings.BuildDirectory_TFS2015, null);
                
                IBootstrapperSettings settings = new BootstrapperSettings(logger);
                AssertExpectedDownloadDir(Path.Combine("legacy tf build", DownloadFolderRelativePath), settings);
            }

            // 2. TFS2015 variable will be used if available
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(BootstrapperSettings.BuildDirectory_Legacy, null);
                scope.SetVariable(BootstrapperSettings.BuildDirectory_TFS2015, "tfs build");
                
                IBootstrapperSettings settings = new BootstrapperSettings(logger);
                AssertExpectedDownloadDir(Path.Combine("tfs build", DownloadFolderRelativePath), settings);
            }

            // 3. CWD has least precedence over env variables
            using (EnvironmentVariableScope scope = new EnvironmentVariableScope())
            {
                scope.SetVariable(BootstrapperSettings.BuildDirectory_Legacy, null);
                scope.SetVariable(BootstrapperSettings.BuildDirectory_TFS2015, null);

                IBootstrapperSettings settings = new BootstrapperSettings(logger);
                AssertExpectedDownloadDir(Path.Combine(Directory.GetCurrentDirectory(), DownloadFolderRelativePath), settings);
            }
        }

        [TestMethod]
        [Description("Check the default values and that relative paths are turned into absolute paths")]
        public void BootSettings_PreProcessorPath()
        {
            // 0. Setup
            TestLogger logger = new TestLogger();

            using (EnvironmentVariableScope envScope = new EnvironmentVariableScope())
            {
                AppConfigWrapper configScope = new AppConfigWrapper();

                envScope.SetVariable(BootstrapperSettings.BuildDirectory_Legacy, @"c:\temp");

                // 1. Default value -> relative to download dir
                IBootstrapperSettings settings = new BootstrapperSettings(logger, configScope.AppConfig);
                AssertExpectedPreProcessPath(Path.Combine(@"c:\temp", DownloadFolderRelativePath, "SonarQube.MSBuild.PreProcessor.exe"), settings);

                // 2. Relative exe set in config -> relative to download dir
                configScope.SetPreProcessExe(@"..\myCustomPreProcessor.exe");
                settings = new BootstrapperSettings(logger, configScope.AppConfig);
                AssertExpectedPreProcessPath(Path.Combine(@"c:\temp", BootstrapperSettings.RelativePathToTempDir, "myCustomPreProcessor.exe"), settings);

                // 3. Now set the config path to an absolute value
                configScope.SetPreProcessExe(@"d:\myCustomPreProcessor.exe");
                settings = new BootstrapperSettings(logger, configScope.AppConfig);
                AssertExpectedPreProcessPath(@"d:\myCustomPreProcessor.exe", settings);
            }
        }

        [TestMethod]
        [Description("Check the default values and that relative paths are turned into absolute paths")]
        public void BootSettings_PostProcessorPath()
        {
            // Check the default values, and that relative paths are turned into absolute paths

            // 0. Setup
            TestLogger logger = new TestLogger();

            using (EnvironmentVariableScope envScope = new EnvironmentVariableScope())
            {
                AppConfigWrapper configScope = new AppConfigWrapper();

                envScope.SetVariable(BootstrapperSettings.BuildDirectory_Legacy, @"c:\temp");

                // 1. Default value -> relative to download dir
                IBootstrapperSettings settings = new BootstrapperSettings(logger, configScope.AppConfig);
                AssertExpectedPostProcessPath(Path.Combine(@"c:\temp", DownloadFolderRelativePath, "SonarQube.MSBuild.PostProcessor.exe"), settings);

                // 2. Relative exe set in config -> relative to download dir
                configScope.SetPostProcessExe(@"..\foo\myCustomPreProcessor.exe");
                settings = new BootstrapperSettings(logger, configScope.AppConfig);
                AssertExpectedPostProcessPath(Path.Combine(@"c:\temp", BootstrapperSettings.RelativePathToTempDir, @"foo\myCustomPreProcessor.exe"), settings);

                // 3. Now set the config path to an absolute value
                configScope.SetPostProcessExe(@"d:\myCustomPostProcessor.exe");

                settings = new BootstrapperSettings(logger, configScope.AppConfig);
                AssertExpectedPostProcessPath(@"d:\myCustomPostProcessor.exe", settings);
            }
        }

        [TestMethod]
        [Description("Checks that the url is taken from the config file in preference to the sonar-runner.properties file")]
        public void BootSettings_ConfigOverridesPropertiesFile_Url()
        {
            // 0. Setup
            TestLogger logger = new TestLogger();

            using (EnvironmentVariableScope envScope = new EnvironmentVariableScope())
            {
                string runnerBinDir = CreateSonarRunnerFiles("http://envUrl");
                envScope.SetPath(runnerBinDir); // so the properties file can be found

                AppConfigWrapper configScope = new AppConfigWrapper();
                configScope.SetSonarQubeUrl("http://configUrl");

                // 1. Check the config scope takes precedence
                IBootstrapperSettings settings = new BootstrapperSettings(logger, configScope.AppConfig);
                AssertExpectedServerUrl(@"http://configUrl", settings);

                // 2. Now clear the config scope and check the env var is used
                configScope.Reset();
                settings = new BootstrapperSettings(logger);
                AssertExpectedServerUrl(@"http://envUrl", settings);
            }
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Creates the sonar runner file structure required for the
        /// product "FileLocator" code to work and create a sonar-runner properties
        /// file containing the specified host url setting
        /// </summary>
        /// <returns>Returns the path of the runner bin directory</returns>
        private string CreateSonarRunnerFiles(string serverUrl)
        {
            string runnerConfDir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "conf");
            string runnerBinDir = TestUtils.EnsureTestSpecificFolder(this.TestContext, "bin");

            // Create a sonar-runner.properties file
            string runnerExe = Path.Combine(runnerBinDir, "sonar-runner.bat");
            File.WriteAllText(runnerExe, "dummy content - only the existence of the file matters");
            string configFile = Path.Combine(runnerConfDir, "sonar-runner.properties");
            File.WriteAllText(configFile, "sonar.host.url=" + serverUrl);
            return runnerBinDir;
        }

        #endregion

        #region Checks

        private static void AssertExpectedDownloadDir(string expected, IBootstrapperSettings settings)
        {
            string actual = settings.DownloadDirectory;
            Assert.AreEqual(expected, actual, "Unexpected download dir", true /* ignore case */);
        }

        private static void AssertExpectedPreProcessPath(string expected, IBootstrapperSettings settings)
        {
            string actual = settings.PreProcessorFilePath;
            Assert.AreEqual(expected, actual, true /* ignore case */, "Unexpected PreProcessFilePath");
        }

        private static void AssertExpectedPostProcessPath(string expected, IBootstrapperSettings settings)
        {
            string actual = settings.PostProcessorFilePath;
            Assert.AreEqual(expected, actual, true /* ignore case */, "Unexpected PostProcessFilePath");
        }
        private static void AssertExpectedServerUrl(string expected, IBootstrapperSettings settings)
        {
            string actual = settings.SonarQubeUrl;
            Assert.AreEqual(expected, actual, true /* ignore case */, "Unexpected server url");
        }

        #endregion
    }
}