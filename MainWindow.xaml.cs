using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace SyncDemo
{
    public partial class MainWindow : Window
    {
        
        private SemaphoreSlim fileSemaphore = new SemaphoreSlim(3, 3); 
        private ReaderWriterLockSlim fileLock = new ReaderWriterLockSlim();
        private string filePath = "DB_tipa.txt"; 
        private int fileTaskCounter = 0;

        

        private SemaphoreSlim dbSemaphore = new SemaphoreSlim(4, 4); 
        private ReaderWriterLockSlim configLock = new ReaderWriterLockSlim();
        private Mutex appMutex;
        private bool hasMutex = false;
        private int activeDbConnections = 0;
        private Dictionary<string, string> config = new Dictionary<string, string>();



        private List<string> FileThingLogsList = new List<string>();
        private List<string> DBLogsList = new List<string>();


        public MainWindow()
        {
            InitializeComponent();
            InitializeApp();
        }

        private void InitializeApp()
        {
            
            if (!File.Exists(filePath))
            {
                File.WriteAllText(filePath, "Ну типа тут че-то есть");
            }

            
            try
            {
                appMutex = new Mutex(true, "SyncDemoAppMutex", out hasMutex);
                UpdateMutexStatus();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка Mutex: {ex.Message}", "Ошибка!", MessageBoxButton.OK, MessageBoxImage.Error);
                this.Close();
            }

            
        }

     
        private async void StartFileTasks_Click(object sender, RoutedEventArgs e)
        {
            StartFileTasks.IsEnabled = false;
            GUI_FileStatus.Text = "Запуск 5 задач записи...";

            var tasks = new List<Task>();
            for (int i = 0; i < 5; i++)
            {
                int taskId = ++fileTaskCounter;
                tasks.Add(Task.Run(() => WriteToFileAsync(taskId)));
            }

            await Task.WhenAll(tasks);

            GUI_FileStatus.Text = "Все задачи завершены";
            StartFileTasks.IsEnabled = true;
        }

        private async Task WriteToFileAsync(int taskId)
        {
            AddFileLog($"Задача {taskId}: ожидает семафор");

            await fileSemaphore.WaitAsync();
            try
            {
                AddFileLog($"Задача {taskId}: получила семафор. Активных операций: {3 - fileSemaphore.CurrentCount}");

                
                await Task.Delay(2000);

                fileLock.EnterWriteLock();
                try
                {
                    string content = $"Запись от задачи {taskId} в {DateTime.Now:HH:mm:ss}\n";
                    File.AppendAllText(filePath, content);
                    AddFileLog($"Задача {taskId}: записала в файл");
                }
                finally
                {
                    fileLock.ExitWriteLock();
                }
            }
            finally
            {
                fileSemaphore.Release();
                AddFileLog($"Задача {taskId}: освободила семафор");
            }
        }

        private void ClearFile_Click(object sender, RoutedEventArgs e)
        {
            fileLock.EnterWriteLock();
            try
            {
                File.WriteAllText(filePath, $"Файл очищен в {DateTime.Now:HH:mm:ss}\n");
                AddFileLog("Файл очищен с использованием WriterLock");
            }
            finally
            {
                fileLock.ExitWriteLock();
            }
        }

        private void ClearLogs_Click(object sender, RoutedEventArgs e)
        {
            GUI_FileLogs.Items.Clear();
        }

        private void AddFileLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                string log = $"{DateTime.Now:HH:mm:ss.fff} - {message}";
                GUI_FileLogs.Items.Add(log);
                GUI_FileLogs.ScrollIntoView(GUI_FileLogs.Items[GUI_FileLogs.Items.Count - 1]);
            });
        }

       

        private async void StartDbTasks_Click(object sender, RoutedEventArgs e)
        {
            StartDbTasks.IsEnabled = false;
            GUI_DBStatus.Text = "Запуск 6 подключений...";

            var tasks = new List<Task>();
            for (int i = 1; i <= 6; i++)
            {
                int taskId = i;
                tasks.Add(Task.Run(() => SimulateDatabaseConnectionAsync(taskId)));
            }

            await Task.WhenAll(tasks);

            GUI_DBStatus.Text = "Все подключения завершены";
            StartDbTasks.IsEnabled = true;
        }

        private async Task SimulateDatabaseConnectionAsync(int connectionId)
        {
            AddDBLog($"Подключение {connectionId}: ожидает семафор");

            await dbSemaphore.WaitAsync();
            try
            {
                Interlocked.Increment(ref activeDbConnections);
                UpdateConnectionStatus();

                AddDBLog($"Подключение {connectionId}: установлено. Активных подключений: {activeDbConnections}");

                
                await Task.Delay(3000);

                AddDBLog($"Подключение {connectionId}: работа завершена");
            }
            finally
            {
                Interlocked.Decrement(ref activeDbConnections);
                dbSemaphore.Release();
                UpdateConnectionStatus();
                AddDBLog($"Подключение {connectionId}: закрыто");
            }
        }

        private void ReadConfig_Click(object sender, RoutedEventArgs e)
        {
            configLock.EnterReadLock();
            try
            {
                string configText = "Текущая конфигурация:\n";
                foreach (var item in config)
                {
                    configText += $"{item.Key}: {item.Value}\n";
                }

                GUI_ConfigStatus.Text = configText;
                AddDBLog("Конфигурация прочитана (чрез ReaderLock)");
            }
            finally
            {
                configLock.ExitReadLock();
            }
        }

        private void WriteConfig_Click(object sender, RoutedEventArgs e)
        {
            configLock.EnterWriteLock();
            try
            {
                config["LastModified"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                config["ModifiedBy"] = "User";

                GUI_ConfigStatus.Text = $"Конфигурация обновлена в {DateTime.Now:HH:mm:ss}";
                AddDBLog("Конфигурация записана (WriterLock)");
            }
            finally
            {
                configLock.ExitWriteLock();
            }
        }

        private void UpdateConnectionStatus()
        {
            Dispatcher.Invoke(() =>
            {
                GUI_ActiveConnections.Text = $"Активных подключений: {activeDbConnections}/4";
            });
        }

        private void UpdateMutexStatus()
        {
            Dispatcher.Invoke(() =>
            {
                if (!hasMutex)
                {
                    throw new Exception("Обнаружено другое запущенное приложение");
                }
                else
                {
                    GUI_MutexStatus.Text = "Mutex: Приложение единственное в системе";
                }
                
            });
        }

        private void AddDBLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                string log = $"{DateTime.Now:HH:mm:ss.fff} - {message}";
                GUI_DbLogs.Items.Add(log);
                GUI_DbLogs.ScrollIntoView(GUI_DbLogs.Items[GUI_DbLogs.Items.Count - 1]);
            });
        }


        protected override void OnClosed(EventArgs e)
        {
            fileSemaphore?.Dispose();
            fileLock?.Dispose();
            dbSemaphore?.Dispose();
            configLock?.Dispose();

            if (hasMutex)
            {
                appMutex?.ReleaseMutex();
                appMutex?.Dispose();
            }

        }
    }
}