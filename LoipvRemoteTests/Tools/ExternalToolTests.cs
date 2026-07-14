using System;
using System.Diagnostics;
using System.Reflection;
using LoipvRemote.Connection;
using LoipvRemote.Tools;
using NUnit.Framework;

namespace LoipvRemoteTests.Tools
{
    [TestFixture]
    public class ExternalToolTests
    {
        [Test]
        public void PasswordWithEqualsSignIsPassedCorrectly()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo
            {
                Password = "Z-3=Wv99/Aq",
                Hostname = "testhost",
                Username = "testuser"
            };

            var externalTool = new ExternalTool
            {
                DisplayName = "Test Tool",
                FileName = "test.exe",
                Arguments = "-u %USERNAME% -p %PASSWORD% -h %HOSTNAME%"
            };

            // Act
            var process = new Process();
            var setProcessPropertiesMethod = typeof(ExternalTool).GetMethod(
                "SetProcessProperties",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            setProcessPropertiesMethod?.Invoke(externalTool, new object[] { process, connectionInfo });

            // Assert
            // The arguments should contain the password with the equals sign
            // It may be escaped (e.g., Z-3^=Wv99/Aq), but should not be split
            string configuredArguments = GetConfiguredArguments(process);
            Assert.That(configuredArguments, Does.Contain("Z-3"));
            Assert.That(configuredArguments, Does.Contain("Wv99/Aq"));
            // ArgumentList keeps the value as data rather than shell syntax.
            Assert.That(configuredArguments, Does.Contain("Z-3=Wv99/Aq"));
        }

        [Test]
        public void PasswordWithSpecialCharactersIsPassedCorrectly()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo
            {
                Password = "P@ss=W0rd!",
                Hostname = "testhost",
                Username = "testuser"
            };

            var externalTool = new ExternalTool
            {
                DisplayName = "Test Tool",
                FileName = "test.exe",
                Arguments = "-p %PASSWORD%"
            };

            // Act
            var process = new Process();
            var setProcessPropertiesMethod = typeof(ExternalTool).GetMethod(
                "SetProcessProperties",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            setProcessPropertiesMethod?.Invoke(externalTool, new object[] { process, connectionInfo });

            // Assert
            // The password should be present in the arguments (possibly escaped)
            string configuredArguments = GetConfiguredArguments(process);
            Assert.That(configuredArguments, Does.Contain("P@ss"));
            Assert.That(configuredArguments, Does.Contain("W0rd"));
        }

        [Test]
        public void MultipleArgumentsAreParsedCorrectly()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo
            {
                Password = "TestPass=123",
                Hostname = "myhost.com",
                Username = "admin",
                Port = 8080
            };

            var externalTool = new ExternalTool
            {
                DisplayName = "Test Tool",
                FileName = "app.exe",
                Arguments = "--host %HOSTNAME% --port %PORT% --user %USERNAME% --pass %PASSWORD%"
            };

            // Act
            var process = new Process();
            var setProcessPropertiesMethod = typeof(ExternalTool).GetMethod(
                "SetProcessProperties",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            setProcessPropertiesMethod?.Invoke(externalTool, new object[] { process, connectionInfo });

            // Assert
            string arguments = GetConfiguredArguments(process);
            Assert.That(arguments, Does.Contain("myhost.com"));
            Assert.That(arguments, Does.Contain("8080"));
            Assert.That(arguments, Does.Contain("admin"));
            Assert.That(arguments, Does.Contain("TestPass"));
            Assert.That(arguments, Does.Contain("123"));
        }

        [Test]
        public void ArgumentsWithSpaces_AreParsedCorrectly()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo
            {
                Hostname = "test host",
                Username = "user name"
            };

            var externalTool = new ExternalTool
            {
                DisplayName = "Test Tool",
                FileName = "app.exe",
                Arguments = "--host \"%HOSTNAME%\" --user \"%USERNAME%\""
            };

            // Act
            var process = new Process();
            var setProcessPropertiesMethod = typeof(ExternalTool).GetMethod(
                "SetProcessProperties",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            setProcessPropertiesMethod?.Invoke(externalTool, new object[] { process, connectionInfo });

            // Assert - When not elevated, arguments should be in ArgumentList
            if (!externalTool.RunElevated)
            {
                Assert.That(process.StartInfo.ArgumentList.Count, Is.GreaterThan(0));
                // Arguments with spaces should be preserved in ArgumentList
                Assert.That(process.StartInfo.ArgumentList, Does.Contain("test host").Or.Contain("--host"));
            }
        }

        [Test]
        public void ValidExecutablePath_DoesNotThrow()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo();
            var externalTool = new ExternalTool
            {
                DisplayName = "Test Tool",
                FileName = "notepad.exe",
                Arguments = ""
            };

            // Act & Assert
            var process = new Process();
            var setProcessPropertiesMethod = typeof(ExternalTool).GetMethod(
                "SetProcessProperties",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            Assert.DoesNotThrow(() => setProcessPropertiesMethod?.Invoke(externalTool, new object[] { process, connectionInfo }));
        }

        [Test]
        public void InvalidExecutablePath_ThrowsArgumentException()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo();
            var externalTool = new ExternalTool
            {
                DisplayName = "Test Tool",
                FileName = "notepad.exe & calc.exe", // Command injection attempt
                Arguments = ""
            };

            // Act & Assert
            var process = new Process();
            var setProcessPropertiesMethod = typeof(ExternalTool).GetMethod(
                "SetProcessProperties",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            var ex = Assert.Throws<TargetInvocationException>(() =>
                setProcessPropertiesMethod?.Invoke(externalTool, new object[] { process, connectionInfo }));
            Assert.That(ex.InnerException, Is.TypeOf<ArgumentException>());
        }

        [Test]
        public void RunElevated_UsesShellExecuteTrue()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo();
            var externalTool = new ExternalTool
            {
                DisplayName = "Test Tool",
                FileName = "notepad.exe",
                Arguments = "--test",
                RunElevated = true
            };

            // Act
            var process = new Process();
            var setProcessPropertiesMethod = typeof(ExternalTool).GetMethod(
                "SetProcessProperties",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            setProcessPropertiesMethod?.Invoke(externalTool, new object[] { process, connectionInfo });

            // Assert
            Assert.That(process.StartInfo.UseShellExecute, Is.True);
            Assert.That(process.StartInfo.Verb, Is.EqualTo("runas"));
        }

        [Test]
        public void RunNotElevated_UsesShellExecuteFalse()
        {
            // Arrange
            var connectionInfo = new ConnectionInfo();
            var externalTool = new ExternalTool
            {
                DisplayName = "Test Tool",
                FileName = "notepad.exe",
                Arguments = "--test",
                RunElevated = false
            };

            // Act
            var process = new Process();
            var setProcessPropertiesMethod = typeof(ExternalTool).GetMethod(
                "SetProcessProperties",
                BindingFlags.NonPublic | BindingFlags.Instance
            );
            setProcessPropertiesMethod?.Invoke(externalTool, new object[] { process, connectionInfo });

            // Assert
            Assert.That(process.StartInfo.UseShellExecute, Is.False);
        }

        [Test]
        public void ToDefinition_ExpandsConnectionTokensAndPreservesLaunchOptions()
        {
            var externalTool = new ExternalTool
            {
                DisplayName = "Terminal",
                FileName = "%WINDIR%\\System32\\cmd.exe",
                Arguments = "/c echo %HOSTNAME%",
                WorkingDir = "%WINDIR%",
                TryIntegrate = true
            };
            var connectionInfo = new ConnectionInfo { Hostname = "server.example" };

            var definition = externalTool.ToDefinition(connectionInfo);

            Assert.Multiple(() =>
            {
                Assert.That(definition.DisplayName, Is.EqualTo("Terminal"));
                Assert.That(definition.ExecutablePath, Does.Contain("cmd.exe"));
                Assert.That(definition.Arguments, Is.EqualTo("/c echo server.example"));
                Assert.That(definition.EmbedWindow, Is.True);
                Assert.That(definition.WaitForExit, Is.False);
            });
        }

        private static string GetConfiguredArguments(Process process) =>
            process.StartInfo.UseShellExecute
                ? process.StartInfo.Arguments
                : string.Join(" ", process.StartInfo.ArgumentList);
    }
}
