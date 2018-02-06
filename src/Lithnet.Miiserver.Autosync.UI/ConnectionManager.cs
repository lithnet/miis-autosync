using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.ServiceModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Lithnet.Miiserver.AutoSync.UI.ViewModels;
using Lithnet.Miiserver.AutoSync.UI.Windows;
using Misuzilla.Security;

namespace Lithnet.Miiserver.AutoSync.UI
{
    public static class ConnectionManager
    {
        private static NetworkCredential connectedCredential;

        internal static string ConnectedHost { get; set; }

        internal static int ConnectedPort { get; set; }

        internal static bool ConnectViaNetTcp { get; set; }

        internal static bool ConnectedToLocalHost { get; set; }

        internal static string ServerIdentityFormatString { get; set; }

        internal static EventClient GetDefaultEventClient(InstanceContext ctx)
        {
            return ConnectionManager.GetEventClient(ctx, ConnectionManager.ConnectedHost, ConnectionManager.ConnectedPort, ConnectionManager.connectedCredential, ConnectionManager.ServerIdentityFormatString);

        }

        internal static ConfigClient GetDefaultConfigClient()
        {
            return ConnectionManager.GetConfigClient(ConnectionManager.ConnectedHost, ConnectionManager.ConnectedPort, ConnectionManager.connectedCredential, ConnectionManager.ServerIdentityFormatString);
        }

        internal static bool TryConnectWithDialog(bool forceShowDialog, bool exitOnCancel, Window owner)
        {
            if (!forceShowDialog)
            {
                if (ConnectionManager.TryDefaultConnection(owner))
                {
                    ConnectionManager.ConnectedHost = UserSettings.AutoSyncServerHost;
                    ConnectionManager.ConnectedPort = UserSettings.AutoSyncServerPort;
                    ConnectionManager.ConnectedToLocalHost = ConnectionManager.IsLocalhost(UserSettings.AutoSyncServerHost);
                    return true;
                }
            }
     
            ConnectDialogViewModel vm = new ConnectDialogViewModel();

            bool dialogResult = Application.Current.Dispatcher.Invoke<bool>(() =>
            {
                ConnectDialog dialog = new ConnectDialog();
                dialog.Owner = owner;
                vm.Window = dialog;
                vm.HostnameRaw = UserSettings.AutoSyncServerHost;
                vm.AutoConnect = UserSettings.AutoConnect;

                if (UserSettings.AutoSyncServerPort != UserSettings.DefaultTcpPort)
                {
                    vm.HostnameRaw += $":{UserSettings.AutoSyncServerPort}";
                }

                dialog.DataContext = vm;

                return dialog.ShowDialog() ?? false;
             });

            if (!dialogResult)
            {
                if (exitOnCancel)
                {
                    Environment.Exit(0);
                }
                else
                {
                    return false;
                }
            }

            UserSettings.AutoSyncServerHost = vm.Hostname;
            UserSettings.AutoSyncServerPort = vm.Port;
            UserSettings.AutoConnect = vm.AutoConnect;

            ConnectionManager.ConnectedHost = vm.Hostname;
            ConnectionManager.ConnectedPort = vm.Port;
            ConnectionManager.ConnectedToLocalHost = ConnectionManager.IsLocalhost(vm.Hostname);

            return true;
        }

        internal static bool TryConnectWithProgress(string host, int port, NetworkCredential credential, Window owner)
        {
            ConnectingDialog connectingDialog = null;
            IntPtr windowHandle = IntPtr.Zero;

            try
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    connectingDialog = new ConnectingDialog();
                    connectingDialog.CaptionText = $"Connecting to {host}";
                    connectingDialog.Owner = owner;
                    connectingDialog.Show();
                    owner.IsEnabled = false;
                    connectingDialog.Activate();

                    WindowInteropHelper windowHelper = new WindowInteropHelper(connectingDialog);
                    windowHandle = windowHelper.Handle;
                    App.DoEvents();
                });

                return ConnectionManager.TryConnect(host, port, credential, connectingDialog.CancellationTokenSource.Token, windowHandle);
            }
            finally
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    connectingDialog?.Hide();
                    owner.IsEnabled = true;
                });
            }
        }

        private static bool TryDefaultConnection(Window owner)
        {
            if (!UserSettings.AutoConnect)
            {
                return false;
            }

            return ConnectionManager.TryConnectWithProgress(UserSettings.AutoSyncServerHost, UserSettings.AutoSyncServerPort, null, owner);
        }

        private static bool TryConnect(string host, int port, NetworkCredential credential, CancellationToken token, IntPtr owningWindowHandle)
        {
            bool attempted = false;

            try
            {
                CredentialUI.PromptForWindowsCredentialsOptions options = new CredentialUI.PromptForWindowsCredentialsOptions("Lithnet AutoSync", $"Enter credentials for {host}");
                options.HwndParent = owningWindowHandle;
                options.Flags = CredentialUI.PromptForWindowsCredentialsFlag.CREDUIWIN_CHECKBOX | CredentialUI.PromptForWindowsCredentialsFlag.CREDUIWIN_GENERIC;

                if (credential == null)
                {
                    credential = ConnectionManager.GetSavedCredentialsOrNull(host);

                    if (credential != null)
                    {
                        options.IsSaveChecked = true;
                    }
                }

                while (true)
                {
                    int errorCode;

                    try
                    {
                        ConnectionManager.ConnectViaNetTcp = UserSettings.UseNetTcpForLocalHost || !ConnectionManager.IsLocalhost(host);
                        ConnectionManager.ConnectWithServerIdentities(host, port, credential, token, UserSettings.AutoSyncServerIdentity, "host/{0}", "autosync/{0}");
                        ConnectionManager.connectedCredential = credential;

                        token.ThrowIfCancellationRequested();
                        return true;
                    }
                    catch (System.ServiceModel.Security.SecurityAccessDeniedException ex)
                    {
                        Trace.WriteLine(ex);
                        errorCode = 5;
                    }
                    catch (System.ServiceModel.Security.SecurityNegotiationException ex)
                    {
                        Trace.WriteLine(ex);
                        if (ex.InnerException is Win32Exception e)
                        {
                            Trace.WriteLine($"Attempt failed with native error code {e.NativeErrorCode}");
                            errorCode = e.NativeErrorCode;
                        }
                        else
                        {
                            errorCode = 1326;
                        }
                    }

                    if (attempted)
                    {
                        options.AuthErrorCode = errorCode;
                    }
                    else
                    {
                        attempted = true;
                    }

                    PromptCredentialsResult result = null;

                    token.ThrowIfCancellationRequested();

                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        if (credential != null)
                        {
                            result = CredentialUI.PromptForWindowsCredentials(options, credential.UserName, credential.Password);
                        }
                        else
                        {
                            result = CredentialUI.PromptForWindowsCredentials(options, null, null);
                        }
                    });

                    token.ThrowIfCancellationRequested();

                    if (result == null)
                    {
                        Trace.WriteLine("User canceled the credential prompt");
                        // User canceled the operation
                        return false;
                    }

                    credential = new NetworkCredential(result.UserName, result.Password);
                    options.IsSaveChecked = result.IsSaveChecked;

                    if (result.IsSaveChecked)
                    {
                        CredentialManager.Write(ConnectionManager.GetCredentialTargetName(host), CredentialType.Generic, CredentialPersistence.LocalMachine, result.UserName, result.Password);
                    }
                    else
                    {
                        CredentialManager.TryDelete(ConnectionManager.GetCredentialTargetName(host), CredentialType.Generic);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            catch (UnsupportedVersionException ex)
            {
                Trace.WriteLine(ex);
                if (token.IsCancellationRequested)
                {
                    return false;
                }

                Application.Current.Dispatcher.Invoke(() =>
                    MessageBox.Show(
                        ex.Message,
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error));
                return false;
            }
            catch (EndpointNotFoundException ex)
            {
                Trace.WriteLine(ex);
                if (token.IsCancellationRequested)
                {
                    return false;
                }

                Application.Current.Dispatcher.Invoke(() => MessageBox.Show(
                    $"Could not contact the AutoSync service. The specified endpoint was not found. Ensure the Lithnet AutoSync service is running on the host",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error));
                return false;
            }
            catch (TimeoutException ex)
            {
                Trace.WriteLine(ex);
                if (token.IsCancellationRequested)
                {
                    return false;
                }

                Application.Current.Dispatcher.Invoke(() => MessageBox.Show(
                    $"Could not contact the AutoSync service due to a connection timeout. Ensure the Lithnet AutoSync service is running on the host, and that the firewall is not blocking access",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error));
                return false;
            }
            catch (System.ServiceModel.Security.SecurityNegotiationException ex)
            {
                Trace.WriteLine(ex);
                if (token.IsCancellationRequested)
                {
                    return false;
                }

                Application.Current.Dispatcher.Invoke(() => MessageBox.Show($"There was an error trying to establish a secure session with the AutoSync server\n\n{ex.Message}", "Security error", MessageBoxButton.OK, MessageBoxImage.Error));
                return false;
            }
            catch (System.ServiceModel.Security.SecurityAccessDeniedException ex)
            {
                Trace.WriteLine(ex);
                if (token.IsCancellationRequested)
                {
                    return false;
                }

                Application.Current.Dispatcher.Invoke(() => MessageBox.Show("You do not have permission to manage the AutoSync service", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error));
                return false;
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                if (token.IsCancellationRequested)
                {
                    return false;
                }

                Application.Current.Dispatcher.Invoke(() => MessageBox.Show(
                    $"An unexpected error occurred communicating with the AutoSync service. Restart the AutoSync service and try again",
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error));
                return false;
            }
        }

        private static void ConnectWithServerIdentities(string host, int port, NetworkCredential credential, CancellationToken token, params string[] serverIdentities)
        {
            foreach (string serveridentity in serverIdentities)
            {
                if (serveridentity == null)
                {
                    continue;
                }

                try
                {
                    token.ThrowIfCancellationRequested();

                    ConfigClient c = ConnectionManager.GetConfigClient(host, port, credential, serveridentity);
                    Trace.WriteLine($"Attempting to connect to the AutoSync service at {c.Endpoint.Address} with expected identity {string.Format(serveridentity, host)}");

                    token.Register(() =>
                    {
                        try
                        {
                            c.Abort();
                        }
                        catch (Exception ex)
                        {
                            Trace.WriteLine(ex);
                        }
                    });

                    Task result = Task.Factory.StartNew(c.Open, token);
                    result.Wait(token);
                    if (result.IsCanceled)
                    {
                        token.ThrowIfCancellationRequested();
                    }

                    if (result.IsFaulted)
                    {
                        throw result.Exception?.InnerException ?? new Exception();
                    }

                    token.ThrowIfCancellationRequested();
                    c.ValidateServiceContractVersion();
                    token.ThrowIfCancellationRequested();
                    Trace.WriteLine($"Connected to the AutoSync service");
                    ConnectionManager.ServerIdentityFormatString = serveridentity;
                    return;
                }
                catch (AggregateException ex)
                {
                    throw ex.InnerException ?? ex;
                }
                catch (System.ServiceModel.Security.SecurityNegotiationException ex)
                {
                    if (ex.InnerException is Win32Exception e)
                    {
                        if (e.NativeErrorCode == -2146893022) // 0x80090322 - SEC_E_WRONG_PRINCIPAL
                        {
                            Trace.WriteLine("Principal name did not match");
                            continue;
                        }
                    }

                    throw;
                }
            }
        }

        private static string GetCredentialTargetName(string host)
        {
            return $"autosync/{host.ToLowerInvariant()}";
        }

        private static NetworkCredential GetSavedCredentialsOrNull(string host)
        {
            NetworkCredential credential = null;

            try
            {
                var storedCreds = CredentialManager.Read(ConnectionManager.GetCredentialTargetName(host), CredentialType.Generic);

                credential = new NetworkCredential();
                credential.UserName = storedCreds.UserName;
                credential.Password = storedCreds.Password;
            }
            catch (Win32Exception ex)
            {
                if (ex.NativeErrorCode != 1168)
                {
                    Trace.Write(ex.ToString());
                }
            }

            return credential;
        }

        private static ConfigClient GetConfigClient(string host, int port, NetworkCredential credential, string identity)
        {
            ConfigClient c;

            if (ConnectionManager.ConnectViaNetTcp)
            {
                c = ConfigClient.GetNetTcpClient(host, port, identity);
                c.ClientCredentials.SupportInteractive = false;
                c.ClientCredentials.Windows.ClientCredential = credential;
            }
            else
            {
                c = ConfigClient.GetNamedPipesClient();
            }

            return c;
        }

        private static EventClient GetEventClient(InstanceContext ctx, string host, int port, NetworkCredential credential, string identity)
        {
            EventClient c;

            if (ConnectionManager.ConnectViaNetTcp)
            {
                c = EventClient.GetNetTcpClient(ctx, host, port, identity);
                c.ClientCredentials.SupportInteractive = false;
                c.ClientCredentials.Windows.ClientCredential = credential;
            }
            else
            {
                c = EventClient.GetNamedPipesClient(ctx);
            }

            return c;
        }

        private static bool IsLocalhost(string incomingHostName)
        {
            try
            {
                IPHostEntry localMachineAddresses = Dns.GetHostEntry(Dns.GetHostName());

                if (IPAddress.TryParse(incomingHostName, out IPAddress incomingIPAddress))
                {
                    incomingIPAddress = new IPAddress(incomingIPAddress.GetAddressBytes()); // Removes the scope from link-local ipv6 addresses

                    if (IPAddress.IsLoopback(incomingIPAddress))
                    {
                        return true;
                    }

                    foreach (IPAddress entry in localMachineAddresses.AddressList)
                    {
                        IPAddress localMachineAddress = new IPAddress(entry.GetAddressBytes());

                        if (localMachineAddress.Equals(incomingIPAddress))
                        {
                            return true;
                        }
                    }
                }

                IPHostEntry incomingHostAddresses = Dns.GetHostEntry(incomingHostName);

                return localMachineAddresses.AddressList.Any(localMachineAddress => incomingHostAddresses.AddressList.Any(incomingHostAddress => IPAddress.IsLoopback(incomingHostAddress) || incomingHostAddress.Equals(localMachineAddress)));
            }
            catch (System.Net.Sockets.SocketException)
            {
                return false;
            }
        }
    }
}
