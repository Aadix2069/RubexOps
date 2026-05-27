using Microsoft.Win32;
using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;

namespace RubexOps
{
    public partial class SettingsPage : Page
    {
        // =====================================================
        // APP DATA FOLDER
        // =====================================================

        private readonly string appDataFolder =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                "RubexOps"
            );



        // =====================================================
        // CONFIG FILE PATH
        // =====================================================

        private readonly string configPath =
            Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.MyDocuments),
                "RubexOps",
                "database_config.json"
            );



        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public SettingsPage()
        {
            InitializeComponent();

            EnsureAppFolderExists();

            EnsureConfigFileExists();

            LoadCurrentDatabasePath();
        }



        // =====================================================
        // ENSURE APP FOLDER EXISTS
        // =====================================================

        private void EnsureAppFolderExists()
        {
            try
            {
                if (!Directory.Exists(appDataFolder))
                {
                    Directory.CreateDirectory(
                        appDataFolder
                    );
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Folder Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }



        // =====================================================
        // ENSURE CONFIG FILE EXISTS
        // =====================================================

        private void EnsureConfigFileExists()
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    DatabaseConfig defaultConfig =
                        new DatabaseConfig
                        {
                            database_path = ""
                        };



                    string json =
                        JsonSerializer.Serialize(
                            defaultConfig,
                            new JsonSerializerOptions
                            {
                                WriteIndented = true
                            }
                        );



                    File.WriteAllText(
                        configPath,
                        json
                    );
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Config Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }



        // =====================================================
        // LOAD CURRENT DATABASE PATH
        // =====================================================

        private void LoadCurrentDatabasePath()
        {
            try
            {
                if (!File.Exists(configPath))
                {
                    return;
                }



                string json =
                    File.ReadAllText(configPath);



                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }



                DatabaseConfig config =
                    JsonSerializer.Deserialize<DatabaseConfig>(
                        json
                    );



                if (config != null)
                {
                    DatabasePathBox.Text =
                        config.database_path ?? "";
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Settings Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }



        // =====================================================
        // BROWSE DATABASE
        // =====================================================

        private void BrowseDatabase_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                OpenFileDialog dialog =
                    new OpenFileDialog
                    {
                        Title = "Select Database File",

                        Filter =
                            "Excel Files (*.xlsx)|*.xlsx"
                    };



                bool? result =
                    dialog.ShowDialog();



                if (result == true)
                {
                    DatabasePathBox.Text =
                        dialog.FileName;
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Browse Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }



        // =====================================================
        // SAVE SETTINGS
        // =====================================================

        private void SaveSettings_Click(
            object sender,
            RoutedEventArgs e)
        {
            try
            {
                string databasePath =
                    DatabasePathBox.Text.Trim();



                // =============================================
                // VALIDATION
                // =============================================

                if (string.IsNullOrWhiteSpace(databasePath))
                {
                    MessageBox.Show(
                        "Please select a database file.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    return;
                }



                if (!File.Exists(databasePath))
                {
                    MessageBox.Show(
                        "Selected database file does not exist.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    return;
                }



                // =============================================
                // CREATE CONFIG OBJECT
                // =============================================

                DatabaseConfig config =
                    new DatabaseConfig
                    {
                        database_path = databasePath
                    };



                // =============================================
                // SERIALIZE JSON
                // =============================================

                string json =
                    JsonSerializer.Serialize(
                        config,
                        new JsonSerializerOptions
                        {
                            WriteIndented = true
                        }
                    );



                // =============================================
                // SAVE CONFIG FILE
                // =============================================

                File.WriteAllText(
                    configPath,
                    json
                );



                // =============================================
                // SUCCESS
                // =============================================

                MessageBox.Show(
                    "Database path saved successfully.\n\n" +
                    "RubexOps will now remember this path permanently.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }

            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(
                    "Permission denied while saving settings.\n\n" +
                    "Try running the application as Administrator.",
                    "Permission Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }

            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Save Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }
    }



    // =====================================================
    // CONFIG MODEL
    // =====================================================

    public class DatabaseConfig
    {
        public string database_path { get; set; } = "";
    }
}