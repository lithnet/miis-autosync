using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.ServiceModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Lithnet.Common.ObjectModel;
using Lithnet.Common.Presentation;
using MahApps.Metro.Controls;
using Microsoft.Win32;
using System.Linq;

namespace Lithnet.Miiserver.AutoSync.UI.ViewModels
{

    internal class MainWindowViewModel : ViewModelBase
    {
        private const string HelpUrl = "https://bit.ly/autosync-help";

        private bool confirmedCloseOnDirtyViewModel;

        internal ExecutionMonitorsViewModel ExecutionMonitor { get; set; }

        public ConfigFileViewModel ConfigFile { get; set; }

        public string DisplayName => "Lithnet AutoSync for Microsoft Identity Manager" + (this.IsDirty ? "*" : string.Empty);

        public Cursor Cursor { get; set; }

        public override IEnumerable<ViewModelBase> ChildNodes
        {
            get
            {
                yield return this.ExecutionMonitor;
                yield return this.ConfigFile?.ManagementAgents;
                yield return this.ConfigFile?.Settings;
            }
        }

        public Visibility StatusBarVisibility { get; set; }

        public MainWindowViewModel()
        {
            this.AddDependentPropertyNotification("IsDirty", "DisplayName");

            this.Commands.AddItem("Reload", x => this.Reload());
            this.Commands.AddItem("Save", x => this.Save(), x => this.CanSave());
            this.Commands.AddItem("Revert", x => this.Revert(), x => this.CanRevert());
            this.Commands.AddItem("Export", x => this.Export(), x => this.CanExport());
            this.Commands.AddItem("Close", x => this.Close());
            this.Commands.AddItem("Help", x => this.Help());
            this.Commands.AddItem("About", x => this.About());
            this.Commands.AddItem("Import", x => this.Import(), x => this.CanImport());

            this.StatusBarVisibility = Visibility.Collapsed;

            ConfigClient c = new ConfigClient();
            this.ExecutionMonitor = new ExecutionMonitorsViewModel(c.GetManagementAgentNames());

            this.Cursor = Cursors.Arrow;
            Application.Current.MainWindow.Closing += this.MainWindow_Closing;

            ViewModelBase.ViewModelIsDirtySet += this.ViewModelBase_ViewModelIsDirtySet;
        }

        private void ViewModelBase_ViewModelIsDirtySet(object sender, PropertyChangedEventArgs e)
        {
            Debug.WriteLine($"A view model of type '{sender.GetType().Name}' changed its state to dirty based on a change of property '{e.PropertyName}'");

            if (!this.IsDirty)
            {
                this.IsDirty = true;
                Trace.WriteLine($"Setting view model state to dirty based on a change of property '{e.PropertyName}' on an object of type '{sender.GetType().Name}'");
            }
        }

        private void Help()
        {
            try
            {
                Process.Start(MainWindowViewModel.HelpUrl);
            }
            catch
            {
                MessageBox.Show(MainWindowViewModel.HelpUrl);
            }
        }

        private void About()
        {
            Windows.About w = new Windows.About();
            w.ShowDialog();
        }

        private void CheckPendingRestart()
        {
            ConfigClient c = new ConfigClient();

            if (c.IsPendingRestart())
            {
                this.StatusBarVisibility = Visibility.Visible;
            }
            else
            {
                this.StatusBarVisibility = Visibility.Collapsed;
            }
        }

        private void Reload(bool confirmRestart = true)
        {
            this.UpdateFocusedBindings();

            if (this.IsDirty)
            {
                if (MessageBox.Show("There are unsaved changes. Are you sure you want to reload the config?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }
            }

            if (confirmRestart)
            {
                if (MessageBox.Show("This will force the AutoSync service to stop and restart with the latest configuration. Are you sure you want to proceed?", "Confirm", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.Cancel)
                {
                    return;
                }
            }

            try
            {
                this.Cursor = Cursors.Wait;
                ConfigClient c = new ConfigClient();
                c.Reload();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while reloading the service config. Check the service log file for details\n{ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }

            this.ResetConfigViewModel();
        }

        private void Revert()
        {
            if (this.IsDirty)
            {
                if (MessageBox.Show("There are unsaved changes. Do you want to continue?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }
            }

            this.ResetConfigViewModel();
        }

        private bool CanRevert()
        {
            return this.IsDirty;
        }

        internal void ResetConfigViewModel()
        {
            try
            {
                this.Cursor = Cursors.Wait;

                ConfigClient c = new ConfigClient();
                ConfigFile file;

                try
                {
                    c.Open();
                    file = c.GetConfig();
                    this.CheckPendingRestart();
                }
                catch (EndpointNotFoundException ex)
                {
                    Trace.WriteLine(ex);
                    MessageBox.Show(
                        $"Could not contact the AutoSync service. Ensure the Lithnet MIIS AutoSync service is running",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
                catch (System.ServiceModel.Security.SecurityAccessDeniedException)
                {
                    MessageBox.Show("You do not have permission to manage the AutoSync service", "Access Denied", MessageBoxButton.OK, MessageBoxImage.Error);
                    Environment.Exit(5);
                    return;
                }
                catch (Exception ex)
                {
                    Trace.WriteLine(ex);
                    MessageBox.Show(
                        $"An error occurred communicating with the AutoSync service\n\n{ex.Message}",
                        "Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }

                this.ConfigFile = new ConfigFileViewModel(file);
                this.ConfigFile.ManagementAgents.IsExpanded = true;

                this.IsDirty = false;
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private void Import()
        {
            this.UpdateFocusedBindings();

            if (this.IsDirty)
            {
                if (MessageBox.Show("There are unsaved changes. Do you want to continue?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }
            }

            OpenFileDialog dialog = new OpenFileDialog();
            dialog.DefaultExt = ".xml";
            dialog.Filter = "Configuration Files (.xml)|*.xml|All Files|*.*";
            dialog.CheckFileExists = true;

            bool? result = dialog.ShowDialog();

            if (result != true)
            {
                return;
            }

            if (!System.IO.File.Exists(dialog.FileName))
            {
                return;
            }

            AutoSync.ConfigFile f;
            try
            {
                f = AutoSync.ConfigFile.Load(dialog.FileName);
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not open the file\n\n{ex.Message}", "File Open", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                this.Cursor = Cursors.Wait;
                ConfigClient c = new ConfigClient();
                c.PutConfig(f);
                this.AskToRestartService();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not import the file\n\n{ex.Message}", "File import operation", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }

            this.IsDirty = false;

            this.ResetConfigViewModel();
        }

        private void Save()
        {
            this.UpdateFocusedBindings();

            try
            {
                if (this.ConfigFile.HasErrors)
                {
                    MessageBox.Show("There are one or more errors in the config file that must be fixed before the file can be saved");
                    return;
                }

                this.MarkManagementAgentsAsConfigured();

                this.Cursor = Cursors.Wait;
                ConfigClient c = new ConfigClient();
                c.PutConfig(this.ConfigFile.Model);
                this.Commit();

                this.AskToRestartService();
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not save the configuration\n\n{ex.Message}", "Save operation", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private void MarkManagementAgentsAsConfigured()
        {
            foreach (MAConfigParametersViewModel p in this.ConfigFile.ManagementAgents)
            {
                p.IsNew = false;
            }
        }

        private void Commit()
        {
            this.IsDirty = false;
            this.ConfigFile.Commit();
        }

        private bool CanSave()
        {
            return this.ConfigFile != null;
        }

        private bool CanImport()
        {
            return this.ConfigFile != null;
        }

        private bool CanExport()
        {
            return this.ConfigFile != null;
        }

        private void AskToRestartService()
        {
            ConfigClient c = new ConfigClient();
            List<string> pendingRestartItems = c.GetManagementAgentsPendingRestart()?.ToList();

            if (pendingRestartItems != null && pendingRestartItems.Count > 0)
            {
                string pendingRestartItemList = string.Join("\r\n", pendingRestartItems);

                if (MessageBox.Show($"The configuration for the following management agents have changed\r\n\r\n{pendingRestartItemList}\r\n\r\nDo you want to restart them now?", "Restart management agents", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    c.RestartChangedExecutors();
                }
            }
        }

        private void Export()
        {
            this.UpdateFocusedBindings();

            if (this.HasErrors)
            {
                if (MessageBox.Show("There are one or more errors present in the configuration. Are you sure you want to save?", "Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.No)
                {
                    return;
                }
            }

            if (MessageBox.Show("Any passwords in the configuration will be exported in plain-text. Are you sure you want to continue?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Exclamation) == MessageBoxResult.No)
            {
                return;
            }

            try
            {
                SaveFileDialog dialog = new SaveFileDialog();
                dialog.DefaultExt = ".xml";
                dialog.Filter = "Configuration file backups (*.xml)|*.xml|All Files|*.*";

                bool? result = dialog.ShowDialog();

                if (result == true)
                {
                    this.Cursor = Cursors.Wait;
                    ProtectedString.EncryptOnWrite = false;
                    this.MarkManagementAgentsAsConfigured();
                    AutoSync.ConfigFile.Save(this.ConfigFile.Model, dialog.FileName);
                    this.IsDirty = false;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine(ex);
                MessageBox.Show($"Could not save the file\n\n{ex.Message}", "Save File", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                this.Cursor = Cursors.Arrow;
            }
        }

        private void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            this.UpdateFocusedBindings();

            if (this.IsDirty && !this.confirmedCloseOnDirtyViewModel)
            {
                if (MessageBox.Show("There are unsaved changes. Are you sure you want to exit?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    e.Cancel = true;
                }
            }
        }

        private void Close()
        {
            this.UpdateFocusedBindings();

            if (this.IsDirty)
            {
                if (MessageBox.Show("There are unsaved changes. Are you sure you want to exit?", "Unsaved Changes", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                {
                    return;
                }
            }

            this.confirmedCloseOnDirtyViewModel = true;
            Application.Current.Shutdown();
        }

        private void UpdateFocusedBindings()
        {
            object focusedItem = Keyboard.FocusedElement;

            if (focusedItem == null)
            {
                return;
            }

            BindingExpression expression = (focusedItem as TextBox)?.GetBindingExpression(TextBox.TextProperty);
            expression?.UpdateSource();

            expression = (focusedItem as ComboBox)?.GetBindingExpression(ComboBox.TextProperty);
            expression?.UpdateSource();

            expression = (focusedItem as PasswordBox)?.GetBindingExpression(PasswordBoxBindingHelper.PasswordProperty);
            expression?.UpdateSource();

            expression = (focusedItem as TimeSpanControl)?.GetBindingExpression(TimeSpanControl.ValueProperty);
            expression?.UpdateSource();

            expression = (focusedItem as DateTimePicker)?.GetBindingExpression(DateTimePicker.SelectedDateProperty);
            expression?.UpdateSource();
        }
    }
}
