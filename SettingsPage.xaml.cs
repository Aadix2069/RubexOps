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
        // APPLICATION BASE DIRECTORY
        // =====================================================

        private readonly string baseDirectory =
            AppDomain.CurrentDomain.BaseDirectory;



        // =====================================================
        // APPLICATION FOLDERS
        // =====================================================

        private readonly string databaseFolder;

        private readonly string configFolder;

        private readonly string backupFolder;

        private readonly string exportFolder;



        // =====================================================
        // DATABASE + CONFIG PATHS
        // =====================================================

        private readonly string databasePath;

        private readonly string templateDatabasePath;

        private readonly string configPath;



        // =====================================================
        // CONSTRUCTOR
        // =====================================================

        public SettingsPage()
        {
            InitializeComponent();



            // =============================================
            // INITIALIZE FOLDER PATHS
            // =============================================

            databaseFolder =
                Path.Combine(
                    baseDirectory,
                    "Database"
                );



            configFolder =
                Path.Combine(
                    baseDirectory,
                    "Config"
                );



            backupFolder =
                Path.Combine(
                    baseDirectory,
                    "Backups"
                );



            exportFolder =
                Path.Combine(
                    baseDirectory,
                    "Exports"
                );



            // =============================================
            // DATABASE FILES
            // =============================================

            databasePath =
                Path.Combine(
                    databaseFolder,
                    "masterdata.xlsx"
                );



            templateDatabasePath =
                Path.Combine(
                    databaseFolder,
                    "master_template.xlsx"
                );



            // =============================================
            // CONFIG FILE
            // =============================================

            configPath =
                Path.Combine(
                    configFolder,
                    "database_config.json"
                );



            // =============================================
            // INITIALIZATION
            // =============================================

            EnsureApplicationFoldersExist();

            EnsureDatabaseExists();

            EnsureConfigFileExists();

            LoadCurrentDatabasePath();
        }



        // =====================================================
        // ENSURE FOLDERS EXIST
        // =====================================================

        private void EnsureApplicationFoldersExist()
        {
            try
            {
                Directory.CreateDirectory(databaseFolder);

                Directory.CreateDirectory(configFolder);

                Directory.CreateDirectory(backupFolder);

                Directory.CreateDirectory(exportFolder);
            }

            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Folder Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }



        // =====================================================
        // ENSURE DATABASE EXISTS
        // =====================================================

        private void EnsureDatabaseExists()
        {
            try
            {
                // =========================================
                // IF WORKING DATABASE DOESN'T EXIST
                // =========================================

                if (!File.Exists(databasePath))
                {
                    // =====================================
                    // COPY FROM TEMPLATE
                    // =====================================

                    if (File.Exists(templateDatabasePath))
                    {
                        File.Copy(
                            templateDatabasePath,
                            databasePath
                        );
                    }
                }
            }

            catch (Exception ex)
            {
                MessageBox.Show(
                    ex.Message,
                    "Database Initialization Error",
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
                            database_path = databasePath
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
                    "Config Initialization Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }



        // =====================================================
        // LOAD DATABASE PATH
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



                DatabaseConfig? config =
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
                    "Load Settings Error",
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
                        Title = "Select Excel Database",

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
                string selectedPath =
                    DatabasePathBox.Text.Trim();



                // =============================================
                // VALIDATION
                // =============================================

                if (string.IsNullOrWhiteSpace(selectedPath))
                {
                    MessageBox.Show(
                        "Please select a database file.",
                        "Validation Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning
                    );

                    return;
                }



                if (!File.Exists(selectedPath))
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
                // COPY DATABASE INTO APPLICATION
                // =============================================

                File.Copy(
                    selectedPath,
                    databasePath,
                    true
                );



                // =============================================
                // CREATE CONFIG OBJECT
                // =============================================

                DatabaseConfig config =
                    new DatabaseConfig
                    {
                        database_path = databasePath
                    };



                // =============================================
                // SERIALIZE CONFIG
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
                // UPDATE UI
                // =============================================

                DatabasePathBox.Text =
                    databasePath;



                // =============================================
                // SUCCESS MESSAGE
                // =============================================

                MessageBox.Show(
                    "Database imported successfully into RubexOps.\n\n" +
                    "The database is now fully integrated with the application.",
                    "Success",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
            }

            catch (UnauthorizedAccessException)
            {
                MessageBox.Show(
                    "Permission denied while saving settings.\n\n" +
                    "Try running RubexOps as Administrator.",
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
    // DATABASE CONFIG MODEL
    // =====================================================

    public class DatabaseConfig
    {
        public string database_path { get; set; } = "";
    }
}